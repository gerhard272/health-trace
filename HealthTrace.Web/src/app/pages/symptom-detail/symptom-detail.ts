import { Component, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SymptomService } from '../../services/symptom.service';
import { Symptom } from '../../models/symptom.models';
import { getErrorMessage } from '../../utils/http-error';

//details of a symptom event (GET /api/Symptom/{id}) with edit and delete
@Component({
  imports: [RouterLink, DatePipe],
  selector: 'app-symptom-detail',
  styleUrl: './symptom-detail.css',
  templateUrl: './symptom-detail.html',
})
export class SymptomDetail {
  symptom = signal<Symptom | null>(null);
  loading = signal(true);
  notFound = signal(false);
  errorMessage = signal<string | null>(null);
  confirmingDelete = signal(false);
  deleting = signal(false);

  private readonly id: number;

  constructor(route: ActivatedRoute, private router: Router, private symptomService: SymptomService) {
    this.id = Number(route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(this.id) || this.id <= 0) {
      this.loading.set(false);
      this.notFound.set(true);
      return;
    }
    this.load();
  }

  private load(): void {
    this.symptomService.getById(this.id).subscribe({
      next: (symptom) => {
        this.symptom.set(symptom);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        //404 also for other users' symptoms: the API does not reveal that they exist
        if (error.status === 404) {
          this.notFound.set(true);
        } else {
          this.errorMessage.set(getErrorMessage(error, 'Unable to load the symptom. Please try again.'));
        }
      }
    });
  }

  confirmDelete(): void {
    this.deleting.set(true);
    this.errorMessage.set(null);
    this.symptomService.delete(this.id).subscribe({
      next: () => this.router.navigate(['/symptoms']),
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.router.navigate(['/symptoms']);
          return;
        }
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        this.errorMessage.set(getErrorMessage(error, 'Unable to delete the symptom. Please try again.'));
      }
    });
  }
}
