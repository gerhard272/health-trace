import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

import { LoginRequest, RegisterRequest, UserResponse } from '../models/auth.models';


@Injectable({ providedIn: 'root' })

export class AuthService {
  private readonly apiUrl = environment.apiUrl + '/api/Auth';
  constructor(private http: HttpClient) { }

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/login`, request);
  }

  register(request: RegisterRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(`${this.apiUrl}/register`, request);
  }
}
