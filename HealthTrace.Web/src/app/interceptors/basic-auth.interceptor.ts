//questo script serve per intercettare le richieste HTTP e aggiungere l'intestazione di
//autorizzazione Basic Auth se le credenziali sono presenti nella sessionStorage.
//Se le credenziali non sono presenti, la richiesta viene inviata senza modifiche.

import { HttpInterceptorFn } from '@angular/common/http';

export const basicAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const credentials = sessionStorage.getItem('basicAuthCredentials');

  //credenziali non presenti? 
  if (!credentials) {
    return next(req); //invia la richiesta senza modifiche 
  }

  const authRequest = req.clone({
    setHeaders: {
      Authorization: `Basic ${credentials}`,
    }
  });

  return next(authRequest); //invia la richiesta con l'intestazione di autorizzazione Basic Auth
};
