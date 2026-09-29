import { Component, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { LoginRequest } from '../../models/auth.models';
import { HttpErrorResponse } from '@angular/common/http';
import { getErrorMessage } from '../../utils/http-error';

@Component({
  imports: [ReactiveFormsModule, CommonModule, RouterLink],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  loginForm: FormGroup;
  //signal e non campi semplici: l'app è zoneless, quindi un campo cambiato dentro la
  //subscribe non aggiornerebbe la vista
  errorMessage = signal<string | null>(null);
  submitting = signal(false);
  //arrivo dalla registrazione appena completata
  readonly registered: boolean;

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.loginForm = this.formBuilder.group({
      username: ['', [Validators.required, Validators.maxLength(50)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(72)]],
    });
    this.registered = this.route.snapshot.queryParamMap.get('registered') === 'true';
  }

  onSubmit(): void {
    this.errorMessage.set(null); //reset dell'errore prima di inviare la richiesta
    if (this.loginForm.valid) {
      const loginRequest: LoginRequest = this.loginForm.getRawValue(); //restituzione completa dei valori del form
      this.submitting.set(true);
      this.authService.login(loginRequest).subscribe({
        next: () => {
          this.submitting.set(false);
          //torna alla pagina che aveva chiesto il login, altrimenti al diario
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
          this.router.navigateByUrl(returnUrl?.startsWith('/') ? returnUrl : '/symptoms');
        },
        error: (error: HttpErrorResponse) => {
          this.submitting.set(false);
          if (error.status === 401) {
            this.errorMessage.set('Username or password is incorrect.');
          } else {
            this.errorMessage.set(getErrorMessage(error, 'An error occurred while logging in. Please try again.'));
          }
        }
      });
    } else {
      this.loginForm.markAllAsTouched(); //se il form non è valido, segna tutti i campi come toccati per mostrare gli errori di validazione
    }
  }
}
