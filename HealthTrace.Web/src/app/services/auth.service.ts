import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

import { LoginRequest, RegisterRequest, UserResponse } from '../models/auth.models';


@Injectable({ providedIn: 'root' })

export class AuthService {
  private readonly apiUrl = environment.apiUrl + '/api/Auth';
  constructor(private http: HttpClient) { }

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/login`, request).pipe(tap(() => {
      const credentials = request.username + ':' + request.password;
      const encodedCredentials = btoa(credentials); // Base64 encode delle credenziali per l'header Authorization, formatta stringhe 
      sessionStorage.setItem('basicAuthCredentials', encodedCredentials); // chiave utilizzata in sessionStorage
    })
    );
  }

  register(request: RegisterRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/register`, request);
  }
}
