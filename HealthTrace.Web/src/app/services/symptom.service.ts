import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

import { Symptom, SymptomFilter, SymptomRequest } from '../models/symptom.models';

//wrapper per le 5 chiamate CRUD di /api/Symptom; l'header Basic Auth lo aggiunge l'interceptor
@Injectable({ providedIn: 'root' })
export class SymptomService {
  private readonly apiUrl = environment.apiUrl + '/api/Symptom';

  constructor(private http: HttpClient) { }

  //date e name sono mutuamente esclusivi: con entrambi l'API risponde 400
  getAll(filter: SymptomFilter = {}): Observable<Symptom[]> {
    let params = new HttpParams();
    if (filter.date) {
      params = params.set('date', filter.date);
    }
    if (filter.name) {
      params = params.set('name', filter.name);
    }
    return this.http.get<Symptom[]>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Symptom> {
    return this.http.get<Symptom>(`${this.apiUrl}/${id}`);
  }

  create(request: SymptomRequest): Observable<Symptom> {
    return this.http.post<Symptom>(this.apiUrl, request);
  }

  //l'API controlla che l'id della rotta coincida con quello del body
  update(id: number, request: SymptomRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, { ...request, id });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
