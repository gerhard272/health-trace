//API errors arrive as ProblemDetails (RFC 7807): title and detail for
//generic errors, errors (field -> messages) for validation errors.
//These functions turn them into text to show in the components.

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
  //a 500 carries no useful details for the user: the component's message is better
  if (error.status >= 500) {
    return fallback;
  }
  const problem = getProblem(error);
  return problem?.detail || problem?.title || fallback;
}

export function getValidationErrors(error: HttpErrorResponse): Record<string, string[]> {
  return error.status === 400 ? (getProblem(error)?.errors ?? {}) : {};
}

//with responseType 'blob' the error body also arrives as a Blob: it must be read and converted back
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
