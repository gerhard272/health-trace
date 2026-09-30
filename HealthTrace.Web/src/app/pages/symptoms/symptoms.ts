import { Component, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { SymptomService } from '../../services/symptom.service';
import { Symptom } from '../../models/symptom.models';
import { getErrorMessage } from '../../utils/http-error';

//diary list: filters by date or by name and inline delete with confirmation
@Component({
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  selector: 'app-symptoms',
  styleUrl: './symptoms.css',
  templateUrl: './symptoms.html',
})
export class Symptoms {
  filterForm: FormGroup;
  symptoms = signal<Symptom[]>([]);
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  hasActiveFilter = signal(false);
  pendingDeleteId = signal<number | null>(null); //row waiting for confirmation
  deletingId = signal<number | null>(null);

  constructor(private formBuilder: FormBuilder, private symptomService: SymptomService) {
    this.filterForm = this.formBuilder.group({
      date: [''],
      name: [''],
    });
    this.load();
  }

  load(): void {
    const date: string = this.filterForm.get('date')?.value ?? '';
    const name: string = (this.filterForm.get('name')?.value ?? '').trim();
    this.errorMessage.set(null);

    //the API accepts only one filter at a time: better to say it here than to get a 400
    if (date && name) {
      this.errorMessage.set('Filter by date or by name, not both.');
      return;
    }

    this.loading.set(true);
    this.hasActiveFilter.set(!!date || !!name);
    this.symptomService.getAll({ date: date || undefined, name: name || undefined }).subscribe({
      next: (symptoms) => {
        //most recent first, as in a diary
        this.symptoms.set([...symptoms].sort((a, b) => b.eventDate.localeCompare(a.eventDate)));
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(getErrorMessage(error, 'Unable to load your symptoms. Please try again.'));
      }
    });
  }

  clearFilters(): void {
    this.filterForm.reset({ date: '', name: '' });
    this.load();
  }

  askDelete(id: number): void {
    this.pendingDeleteId.set(id);
  }

  cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  confirmDelete(id: number): void {
    this.deletingId.set(id);
    this.errorMessage.set(null);
    this.symptomService.delete(id).subscribe({
      next: () => this.removeFromList(id),
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          //already deleted (e.g. from another tab): just remove it from the list
          this.removeFromList(id);
          return;
        }
        this.deletingId.set(null);
        this.pendingDeleteId.set(null);
        this.errorMessage.set(getErrorMessage(error, 'Unable to delete the symptom. Please try again.'));
      }
    });
  }

  private removeFromList(id: number): void {
    this.symptoms.update((symptoms) => symptoms.filter((symptom) => symptom.id !== id));
    this.deletingId.set(null);
    this.pendingDeleteId.set(null);
  }
}
