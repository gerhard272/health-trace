import { Component, signal } from '@angular/core';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  AbstractControl,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';

import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { RegisterRequest } from '../../models/auth.models';
import { HttpErrorResponse } from '@angular/common/http';
import { getErrorMessage, getValidationErrors } from '../../utils/http-error';

const passwordMatchValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const password = control.get('password')?.value;
  const passwordConfirmation = control.get('passwordConfirmation')?.value;

  if (!passwordConfirmation) {
    return null;
  }

  return password === passwordConfirmation ? null : { passwordMismatch: true };
};

//stessa regola del RegisterModelValidator: la data di nascita non può essere futura
const notInFutureValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  if (!control.value) {
    return null;
  }
  const today = new Date().toISOString().slice(0, 10);
  return control.value > today ? { futureDate: true } : null;
};

@Component({
  imports: [ReactiveFormsModule, CommonModule, RouterLink],
  selector: 'app-register',
  styleUrl: './register.css',
  templateUrl: './register.html',
})


export class Register {
  registerForm: FormGroup;
  //signal e non campi semplici: l'app è zoneless, quindi un campo cambiato dentro la
  //subscribe non aggiornerebbe la vista
  errorMessage = signal<string | null>(null);
  validationErrors = signal<Record<string, string[]>>({});
  submitting = signal(false);
  constructor(private formBuilder: FormBuilder, private authService: AuthService, private router: Router) {
    this.registerForm = this.formBuilder.group({
      username: ['', [Validators.required, Validators.maxLength(50)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(72)]],
      passwordConfirmation: ['', [Validators.required]],
      firstName: ['', [Validators.required, Validators.maxLength(50)]],
      lastName: ['', [Validators.required, Validators.maxLength(50)]],
      cf: ['', [
        Validators.required,
        Validators.minLength(16),
        Validators.maxLength(16),
        Validators.pattern(/^[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]$/)
      ] ],
      birthDate: ['', [notInFutureValidator]],
      birthPlace: [''],
    }, { validators: passwordMatchValidator }); //init nel costruttore

    //il codice fiscale è validato in maiuscolo (qui e nell'API): lo si converte mentre si scrive
    const cfControl = this.registerForm.get('cf')!;
    cfControl.valueChanges.subscribe((value: string | null) => {
      const upper = value?.toUpperCase() ?? '';
      if (upper !== value) {
        cfControl.setValue(upper, { emitEvent: false });
      }
    });
  }
  onSubmit(): void {
    this.errorMessage.set(null); //reset dell'errore prima di inviare la richiesta
    this.validationErrors.set({}); //reset degli errori di validazione prima di inviare la richiesta
    if (this.registerForm.valid) {
      const formValue = this.registerForm.getRawValue(); //restituzione completa dei valori del form
      //i campi facoltativi vuoti vanno inviati come null: una stringa vuota non è una
      //data valida per il DateOnly dell'API e la richiesta verrebbe rifiutata con 400
      const registerRequest: RegisterRequest = {
        ...formValue,
        birthDate: formValue.birthDate || undefined,
        birthPlace: formValue.birthPlace?.trim() || undefined,
      };
      this.submitting.set(true);
      this.authService.register(registerRequest).subscribe({
        next: () => { //callback per gestire la risposta positiva della registrazione
          this.submitting.set(false);
          this.router.navigate(['/login'], { queryParams: { registered: true } });
        },
        error: (error: HttpErrorResponse) => {
          this.submitting.set(false);
          const errors = getValidationErrors(error);
          if (Object.keys(errors).length > 0) {
            this.validationErrors.set(errors);
            this.errorMessage.set('Please correct the validation errors.');
          } else {
            this.errorMessage.set(getErrorMessage(error, 'An error occurred while registering. Please try again.'));
          }
        }
      });
    } else {
      this.registerForm.markAllAsTouched(); //se il form non è valido, segna tutti i campi come toccati per mostrare gli errori di validazione
    }
  }
}
