import { Component, DestroyRef, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, take, takeWhile, timer } from 'rxjs';
import { ExportService } from '../../services/export.service';
import { SymptomService } from '../../services/symptom.service';
import { ExportRequest, ExportRequestCreate, ExportStatus } from '../../models/export.models';
import { Symptom } from '../../models/symptom.models';
import { getErrorMessage, readBlobProblem } from '../../utils/http-error';

type ExportScope = 'all' | 'range' | 'selection';

//l'export è asincrono: la richiesta risponde subito, poi si interroga lo stato finché
//la Function non ha generato il PDF (Completed) o non è fallita (Failed)
const POLL_INTERVAL_MS = 2000;
const MAX_POLLS = 60; //circa due minuti, poi si aggiorna a mano con "Refresh"

@Component({
  imports: [ReactiveFormsModule, DatePipe],
  selector: 'app-export',
  styleUrl: './export.css',
  templateUrl: './export.html',
})
export class Export {
  exportForm: FormGroup;
  symptoms = signal<Symptom[]>([]);
  selectedIds = signal<ReadonlySet<number>>(new Set());
  history = signal<ExportRequest[]>([]);

  loadingSymptoms = signal(false);
  loadingHistory = signal(false);
  requesting = signal(false);
  downloadingId = signal<number | null>(null);

  requestError = signal<string | null>(null);
  requestInfo = signal<string | null>(null);
  historyError = signal<string | null>(null);
  downloadError = signal<string | null>(null);

  private readonly polled = new Set<number>(); //export già seguiti, per non duplicare il polling

  constructor(
    private formBuilder: FormBuilder,
    private exportService: ExportService,
    private symptomService: SymptomService,
    private destroyRef: DestroyRef
  ) {
    this.exportForm = this.formBuilder.group({
      scope: ['all' as ExportScope],
      fromDate: [''],
      toDate: [''],
    });
    this.loadSymptoms();
    this.loadHistory();
  }

  get scope(): ExportScope {
    return this.exportForm.get('scope')?.value;
  }

  loadSymptoms(): void {
    this.loadingSymptoms.set(true);
    this.symptomService.getAll().subscribe({
      next: (symptoms) => {
        this.symptoms.set([...symptoms].sort((a, b) => b.eventDate.localeCompare(a.eventDate)));
        this.loadingSymptoms.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loadingSymptoms.set(false);
        this.requestError.set(getErrorMessage(error, 'Unable to load your symptoms.'));
      }
    });
  }

  loadHistory(): void {
    this.loadingHistory.set(true);
    this.historyError.set(null);
    this.exportService.getHistory().subscribe({
      next: (history) => {
        this.history.set(history);
        this.loadingHistory.set(false);
        //riprende a seguire gli export ancora in lavorazione (es. dopo un refresh della pagina)
        history.filter((item) => !isFinal(item.status)).forEach((item) => this.poll(item.id));
      },
      error: (error: HttpErrorResponse) => {
        this.loadingHistory.set(false);
        this.historyError.set(getErrorMessage(error, 'Unable to load your exports.'));
      }
    });
  }

  toggleSymptom(id: number): void {
    this.selectedIds.update((selected) => {
      const next = new Set(selected);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  selectAll(): void {
    this.selectedIds.set(new Set(this.symptoms().map((symptom) => symptom.id)));
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  onRequest(): void {
    this.requestError.set(null);
    this.requestInfo.set(null);

    const request = this.buildRequest();
    if (request === null) {
      return;
    }

    this.requesting.set(true);
    this.exportService.requestExport(request).subscribe({
      next: (created) => {
        this.requesting.set(false);
        this.upsert(created);
        this.requestInfo.set(isFinal(created.status)
          ? 'Export ready.'
          : 'Export requested: the PDF is being generated, it will appear below when ready.');
        this.poll(created.id);
      },
      error: (error: HttpErrorResponse) => {
        this.requesting.set(false);
        this.requestError.set(getErrorMessage(error, 'Unable to request the export. Please try again.'));
      }
    });
  }

  download(item: ExportRequest): void {
    this.downloadError.set(null);
    this.downloadingId.set(item.id);
    this.exportService.download(item.id).subscribe({
      next: (blob) => {
        this.downloadingId.set(null);
        saveFile(blob, item.fileName || `healthtrace-export-${item.id}.pdf`);
      },
      error: async (error: HttpErrorResponse) => {
        this.downloadingId.set(null);
        //con responseType 'blob' il ProblemDetails arriva come Blob da rileggere
        const problem = await readBlobProblem(error);
        if (error.status === 409) {
          this.downloadError.set(problem?.detail || 'The export is not ready yet.');
        } else if (error.status === 404) {
          this.downloadError.set('The file of this export is no longer available.');
        } else {
          this.downloadError.set(getErrorMessage(error, 'Unable to download the file. Please try again.'));
        }
      }
    });
  }

  describeCriteria(item: ExportRequest): string {
    if (item.symptomIds.length > 0) {
      return item.symptomIds.length === 1 ? '1 selected symptom' : `${item.symptomIds.length} selected symptoms`;
    }
    const from = item.fromDate ? formatDay(item.fromDate) : null;
    const to = item.toDate ? formatDay(item.toDate) : null;
    if (from && to) {
      return `From ${from} to ${to}`;
    }
    if (from) {
      return `From ${from}`;
    }
    if (to) {
      return `Until ${to}`;
    }
    return 'All symptoms';
  }

  statusClass(status: ExportStatus): string {
    switch (status) {
      case 'Completed': return 'bg-green-100 text-green-800';
      case 'Failed': return 'bg-red-100 text-red-800';
      case 'Processing': return 'bg-blue-100 text-blue-800';
      default: return 'bg-gray-100 text-gray-700';
    }
  }

  isFinal(status: ExportStatus): boolean {
    return isFinal(status);
  }

  private buildRequest(): ExportRequestCreate | null {
    const { scope, fromDate, toDate } = this.exportForm.getRawValue();

    if (scope === 'range') {
      if (!fromDate && !toDate) {
        this.requestError.set('Choose a start date, an end date or both.');
        return null;
      }
      if (fromDate && toDate && fromDate > toDate) {
        this.requestError.set('The start date cannot be later than the end date.');
        return null;
      }
      return { fromDate: fromDate || undefined, toDate: toDate || undefined };
    }

    if (scope === 'selection') {
      if (this.selectedIds().size === 0) {
        this.requestError.set('Select at least one symptom.');
        return null;
      }
      return { symptomIds: [...this.selectedIds()] };
    }

    return {}; //nessun criterio: l'API esporta tutti i sintomi dell'utente
  }

  private poll(id: number): void {
    if (this.polled.has(id)) {
      return;
    }
    this.polled.add(id);
    timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS).pipe(
      switchMap(() => this.exportService.getById(id)),
      takeWhile((item) => !isFinal(item.status), true), //include l'ultimo stato, quello finale
      take(MAX_POLLS),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (item) => this.upsert(item),
      //un errore interrompe solo l'aggiornamento automatico: resta il pulsante "Refresh"
      error: () => this.polled.delete(id),
      complete: () => this.polled.delete(id),
    });
  }

  private upsert(item: ExportRequest): void {
    this.history.update((history) => {
      const exists = history.some((existing) => existing.id === item.id);
      return exists
        ? history.map((existing) => (existing.id === item.id ? item : existing))
        : [item, ...history];
    });
  }
}

function isFinal(status: ExportStatus): boolean {
  return status === 'Completed' || status === 'Failed';
}

//"2026-09-01T00:00:00" -> "01/09/2026": solo la parte di data, senza conversioni di fuso
function formatDay(value: string): string {
  const [year, month, day] = value.slice(0, 10).split('-');
  return `${day}/${month}/${year}`;
}

function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  //il link temporaneo ha già avviato il download: l'URL si può rilasciare
  setTimeout(() => URL.revokeObjectURL(url));
}
