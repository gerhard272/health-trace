//gli errori dell'API arrivano come ProblemDetails (RFC 7807): title e detail per gli
//errori generici, errors (campo -> messaggi) per quelli di validazione.
//Queste funzioni li trasformano in testo da mostrare nei componenti.

import { HttpErrorResponse } from '@angular/common/http';

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export function getProblem(error: HttpErrorResponse): ProblemDetails | null {
  const body = error.error;
  return body && typeof body === 'object' && !(body instanceof Blob) ? (body as ProblemDetails) : null;
}

export function getErrorMessage(error: HttpErrorResponse, fallback: string): string {
  if (error.status === 0) {
    return 'Cannot reach the server. Check your connection and try again.';
  }
  //il 500 non porta dettagli utili per l'utente: meglio il messaggio del componente
  if (error.status >= 500) {
    return fallback;
  }
  const problem = getProblem(error);
  return problem?.detail || problem?.title || fallback;
}

export function getValidationErrors(error: HttpErrorResponse): Record<string, string[]> {
  return error.status === 400 ? (getProblem(error)?.errors ?? {}) : {};
}

//con responseType 'blob' anche il corpo d'errore arriva come Blob: va letto e riconvertito
export async function readBlobProblem(error: HttpErrorResponse): Promise<ProblemDetails | null> {
  if (!(error.error instanceof Blob)) {
    return getProblem(error);
  }
  try {
    return JSON.parse(await error.error.text()) as ProblemDetails;
  } catch {
    return null;
  }
}
