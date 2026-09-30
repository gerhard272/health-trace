import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

import { LoginRequest, RegisterRequest, UserResponse } from '../models/auth.models';

const CREDENTIALS_KEY = 'basicAuthCredentials'; // sessionStorage key for the Authorization header
const USER_KEY = 'currentUser'; // profile of the logged-in user, to show their name

@Injectable({ providedIn: 'root' })

export class AuthService {
  private readonly apiUrl = environment.apiUrl + '/api/Auth';

  //current user as a signal: the navbar updates itself on login and logout
  readonly currentUser = signal<UserResponse | null>(readStoredUser());

  constructor(private http: HttpClient) { }

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/login`, request).pipe(tap((user) => {
      //credentials are saved only after the API has accepted them
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

//btoa only accepts Latin-1 characters: encode to UTF-8 first, the same encoding
//BasicAuthenticationHandler uses to decode the header, so passwords with accents work too
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
