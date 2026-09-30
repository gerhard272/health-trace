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

//same rule as RegisterModelValidator: the birth date cannot be in the future
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
  //signals rather than plain fields: the app is zoneless, so a field changed inside
  //subscribe would not update the view
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
    }, { validators: passwordMatchValidator }); //initialized in the constructor

    //the fiscal code is validated in upper case (here and in the API): convert it while typing
    const cfControl = this.registerForm.get('cf')!;
    cfControl.valueChanges.subscribe((value: string | null) => {
      const upper = value?.toUpperCase() ?? '';
      if (upper !== value) {
        cfControl.setValue(upper, { emitEvent: false });
      }
    });
  }
  onSubmit(): void {
    this.errorMessage.set(null); //reset the error before sending the request
    this.validationErrors.set({}); //reset the validation errors before sending the request
    if (this.registerForm.valid) {
      const formValue = this.registerForm.getRawValue(); //all the form values
      //empty optional fields must be sent as null: an empty string is not a
      //valid date for the API's DateOnly and the request would be rejected with 400
      const registerRequest: RegisterRequest = {
        ...formValue,
        birthDate: formValue.birthDate || undefined,
        birthPlace: formValue.birthPlace?.trim() || undefined,
      };
      this.submitting.set(true);
      this.authService.register(registerRequest).subscribe({
        next: () => { //successful registration callback
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
      this.registerForm.markAllAsTouched(); //if the form is invalid, mark all fields as touched to show the validation errors
    }
  }
}
