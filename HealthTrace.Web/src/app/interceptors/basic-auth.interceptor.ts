//intercepts HTTP requests and adds the Basic Auth Authorization
//header if the credentials are present in sessionStorage.
//If the credentials are not present, the request is sent unchanged.
//A 401 on a protected call means the credentials are no longer valid: log out
//and go back to the login page.

import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '../services/auth.service';

export const basicAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const credentials = authService.getCredentials();

  //the header only goes to our API: credentials must never reach other hosts
  const isApiRequest = req.url.startsWith(environment.apiUrl);
  const isAuthRequest = req.url.startsWith(`${environment.apiUrl}/api/Auth/`);

  const request = credentials && isApiRequest
    ? req.clone({ setHeaders: { Authorization: `Basic ${credentials}` } })
    : req; //no credentials or external host: send the request unchanged

  return next(request).pipe(
    catchError((error: unknown) => {
      //the login 401 is left to the component, which shows "invalid credentials"
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiRequest && !isAuthRequest) {
        authService.logout();
        router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    })
  );
};
