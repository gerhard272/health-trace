import { Component, signal } from '@angular/core';
import { KeyValuePipe } from '@angular/common';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, map } from 'rxjs';
import { SymptomService } from '../../services/symptom.service';
import { SymptomRequest } from '../../models/symptom.models';
import { getErrorMessage, getValidationErrors } from '../../utils/http-error';

//the API does not validate symptoms with FluentValidation: these checks are the only filter
//before the database (EventName required, max 100; Description max 500)
const notBlankValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  typeof control.value === 'string' && control.value.trim().length === 0 ? { blank: true } : null;

const validDateValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  control.value && Number.isNaN(Date.parse(control.value)) ? { invalidDate: true } : null;

//single form for create (symptoms/new) and edit (symptoms/:id/edit)
@Component({
  imports: [ReactiveFormsModule, RouterLink, KeyValuePipe],
  selector: 'app-symptom-form',
  styleUrl: './symptom-form.css',
  templateUrl: './symptom-form.html',
})
export class SymptomForm {
  symptomForm: FormGroup;
  readonly editId: number | null;
  loading = signal(false);
  notFound = signal(false);
  submitting = signal(false);
  errorMessage = signal<string | null>(null);
  validationErrors = signal<Record<string, string[]>>({});

  constructor(
    private formBuilder: FormBuilder,
    private symptomService: SymptomService,
    private router: Router,
    route: ActivatedRoute
  ) {
    this.symptomForm = this.formBuilder.group({
      eventName: ['', [Validators.required, notBlankValidator, Validators.maxLength(100)]],
      description: ['', [Validators.maxLength(500)]],
      eventDate: [toDateTimeLocal(new Date()), [Validators.required, validDateValidator]],
    });

    const idParam = route.snapshot.paramMap.get('id');
    this.editId = idParam === null ? null : Number(idParam);
    if (this.editId !== null) {
      this.loadForEdit(this.editId);
    }
  }

  get isEdit(): boolean {
    return this.editId !== null;
  }

  private loadForEdit(id: number): void {
    if (!Number.isInteger(id) || id <= 0) {
      this.notFound.set(true);
      return;
    }
    this.loading.set(true);
    this.symptomService.getById(id).subscribe({
      next: (symptom) => {
        this.symptomForm.patchValue({
          eventName: symptom.eventName,
          description: symptom.description ?? '',
          eventDate: symptom.eventDate.slice(0, 16), //format of <input type="datetime-local">
        });
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        if (error.status === 404) {
          this.notFound.set(true);
        } else {
          this.errorMessage.set(getErrorMessage(error, 'Unable to load the symptom. Please try again.'));
        }
      }
    });
  }

  onSubmit(): void {
    this.errorMessage.set(null);
    this.validationErrors.set({});
    if (this.symptomForm.invalid) {
      this.symptomForm.markAllAsTouched();
      return;
    }

    const value = this.symptomForm.getRawValue();
    const request: SymptomRequest = {
      eventName: value.eventName.trim(),
      description: value.description?.trim() || null,
      eventDate: value.eventDate,
    };

    this.submitting.set(true);
    const editId = this.editId;
    //both paths return the id of the saved symptom, to open its details
    const save$: Observable<number> = editId === null
      ? this.symptomService.create(request).pipe(map((created) => created.id))
      : this.symptomService.update(editId, request).pipe(map(() => editId));

    save$.subscribe({
      next: (id) => {
        this.submitting.set(false);
        this.router.navigate(['/symptoms', id]);
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        if (error.status === 404) {
          this.notFound.set(true);
          return;
        }
        const errors = getValidationErrors(error);
        if (Object.keys(errors).length > 0) {
          this.validationErrors.set(errors);
          this.errorMessage.set('Please correct the validation errors.');
        } else {
          this.errorMessage.set(getErrorMessage(error, 'Unable to save the symptom. Please try again.'));
        }
      }
    });
  }
}

//local date in the yyyy-MM-ddTHH:mm format required by <input type="datetime-local">
function toDateTimeLocal(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
