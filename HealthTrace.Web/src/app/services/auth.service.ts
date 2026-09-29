import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

import { LoginRequest, RegisterRequest, UserResponse } from '../models/auth.models';

const CREDENTIALS_KEY = 'basicAuthCredentials'; // chiave utilizzata in sessionStorage per l'header Authorization
const USER_KEY = 'currentUser'; // profilo dell'utente loggato, per mostrarne il nome

@Injectable({ providedIn: 'root' })

export class AuthService {
  private readonly apiUrl = environment.apiUrl + '/api/Auth';

  //utente corrente come signal: la navbar si aggiorna da sola a login e logout
  readonly currentUser = signal<UserResponse | null>(readStoredUser());

  constructor(private http: HttpClient) { }

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/login`, request).pipe(tap((user) => {
      //le credenziali si salvano solo dopo che l'API le ha accettate
      sessionStorage.setItem(CREDENTIALS_KEY, encodeCredentials(request.username, request.password));
      sessionStorage.setItem(USER_KEY, JSON.stringify(user));
      this.currentUser.set(user);
    })
    );
  }

  register(request: RegisterRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/register`, request);
  }

  logout(): void {
    sessionStorage.removeItem(CREDENTIALS_KEY);
    sessionStorage.removeItem(USER_KEY);
    this.currentUser.set(null);
  }

  isAuthenticated(): boolean {
    return this.getCredentials() !== null;
  }

  getCredentials(): string | null {
    return sessionStorage.getItem(CREDENTIALS_KEY);
  }
}

//btoa accetta solo caratteri Latin-1: si codifica prima in UTF-8, lo stesso encoding con cui
//BasicAuthenticationHandler decodifica l'header, così funzionano anche password con accenti
function encodeCredentials(username: string, password: string): string {
  const bytes = new TextEncoder().encode(`${username}:${password}`);
  let binary = '';
  bytes.forEach((byte) => (binary += String.fromCharCode(byte)));
  return btoa(binary);
}

function readStoredUser(): UserResponse | null {
  try {
    const stored = sessionStorage.getItem(USER_KEY);
    return stored ? (JSON.parse(stored) as UserResponse) : null;
  } catch {
    return null;
  }
}
