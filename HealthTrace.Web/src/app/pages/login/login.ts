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
  //signals rather than plain fields: the app is zoneless, so a field changed inside
  //subscribe would not update the view
  errorMessage = signal<string | null>(null);
  submitting = signal(false);
  //coming from a just-completed registration
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
    this.errorMessage.set(null); //reset the error before sending the request
    if (this.loginForm.valid) {
      const loginRequest: LoginRequest = this.loginForm.getRawValue(); //all the form values
      this.submitting.set(true);
      this.authService.login(loginRequest).subscribe({
        next: () => {
          this.submitting.set(false);
          //go back to the page that required the login, otherwise to the diary
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
      this.loginForm.markAllAsTouched(); //if the form is invalid, mark all fields as touched to show the validation errors
    }
  }
}
