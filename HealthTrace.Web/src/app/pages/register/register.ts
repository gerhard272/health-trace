import { Component } from '@angular/core';
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
import { AuthService } from '../../services/auth.service';
import { RegisterRequest } from '../../models/auth.models';
import { HttpErrorResponse } from '@angular/common/http';

const passwordMatchValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const password = control.get('password')?.value;
  const passwordConfirmation = control.get('passwordConfirmation')?.value;

  if (!passwordConfirmation) {
    return null;
  }

  return password === passwordConfirmation ? null : { passwordMismatch: true };
};

@Component({
  imports: [ReactiveFormsModule, CommonModule],
  selector: 'app-register',
  styleUrl: './register.css',
  templateUrl: './register.html',
})


export class Register {
  registerForm: FormGroup;
  errorMessage: string | null = null;
  validationErrors: Record<string, string[]> = {};  
  constructor(private formBuilder: FormBuilder, private authService: AuthService) {
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
      birthDate: [''],
      birthPlace: [''],
    }, { validators: passwordMatchValidator }); //init nel costruttore
  
  }
  onSubmit(): void {
    this.errorMessage = null; //reset dell'errore prima di inviare la richiesta
    this.validationErrors = {}; //reset degli errori di validazione prima di inviare la richiesta
    if (this.registerForm.valid) {
      const registerRequest: RegisterRequest = this.registerForm.getRawValue(); //restituzione completa dei valori del form
      this.authService.register(registerRequest).subscribe({
        next: () => { //callback per gestire la risposta positiva della registrazione
          // Gestione del successo della registrazione
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 400 && error.error?.errors) {
            this.validationErrors = error.error.errors;
            this.errorMessage = 'Please correct the validation errors.';
          } else {
            this.errorMessage = 'An error occurred while registering. Please try again.';
          }
        }
      });
    } else {
      this.registerForm.markAllAsTouched(); //se il form non è valido, segna tutti i campi come toccati per mostrare gli errori di validazione
    }
  }
}
