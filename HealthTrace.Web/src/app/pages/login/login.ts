import { Component } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth.service';
import { LoginRequest } from '../../models/auth.models';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  imports: [ReactiveFormsModule, CommonModule],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  loginForm: FormGroup;
  errorMessage: string | null = null;

  constructor(private formBuilder: FormBuilder, private authService: AuthService) {
    this.loginForm = this.formBuilder.group({
      username: ['', [Validators.required, Validators.maxLength(50)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(72)]],
    });
  }

  onSubmit(): void {
    this.errorMessage = null; //reset dell'errore prima di inviare la richiesta
    if (this.loginForm.valid) {
      const loginRequest: LoginRequest = this.loginForm.getRawValue(); //restituzione completa dei valori del form
      this.authService.login(loginRequest).subscribe({
        next: (user) => {
          console.log('Login successful', user);
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) {
            this.errorMessage = 'Username or password is incorrect.';
          }
          console.error('Login failed', error);
          
        }
      });
    } else {
      this.loginForm.markAllAsTouched(); //se il form non è valido, segna tutti i campi come toccati per mostrare gli errori di validazione
    }
  }
}
