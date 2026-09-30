//questo script serve per intercettare le richieste HTTP e aggiungere l'intestazione di
//autorizzazione Basic Auth se le credenziali sono presenti nella sessionStorage.
//Se le credenziali non sono presenti, la richiesta viene inviata senza modifiche.
//Un 401 su una chiamata protetta significa credenziali non più valide: si fa logout
//e si torna al login.

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

  //l'header va solo verso la nostra API: le credenziali non devono finire su altri host
  const isApiRequest = req.url.startsWith(environment.apiUrl);
  const isAuthRequest = req.url.startsWith(`${environment.apiUrl}/api/Auth/`);

  const request = credentials && isApiRequest
    ? req.clone({ setHeaders: { Authorization: `Basic ${credentials}` } })
    : req; //credenziali non presenti o host esterno: invia la richiesta senza modifiche

  return next(request).pipe(
    catchError((error: unknown) => {
      //il 401 di login resta al componente, che mostra "credenziali errate"
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiRequest && !isAuthRequest) {
        authService.logout();
        router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    })
  );
};
