import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';

import { ExportRequest, ExportRequestCreate } from '../models/export.models';

//chiamate a /api/exports: la richiesta risponde subito 202, il PDF viene generato
//in background dalla Azure Function e si scarica quando lo stato è Completed
@Injectable({ providedIn: 'root' })
export class ExportService {
  private readonly apiUrl = environment.apiUrl + '/api/exports';

  constructor(private http: HttpClient) { }

  requestExport(request: ExportRequestCreate): Observable<ExportRequest> {
    return this.http.post<ExportRequest>(`${this.apiUrl}/request`, request).pipe(map(withUtcCreatedAt));
  }

  getHistory(): Observable<ExportRequest[]> {
    return this.http.get<ExportRequest[]>(this.apiUrl).pipe(map((exports) => exports.map(withUtcCreatedAt)));
  }

  getById(id: number): Observable<ExportRequest> {
    return this.http.get<ExportRequest>(`${this.apiUrl}/${id}`).pipe(map(withUtcCreatedAt));
  }

  //il file passa dall'API (non da un URL diretto al Blob), quindi serve l'header Basic Auth
  download(id: number): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/download`, { responseType: 'blob' });
  }
}

//createdAt è salvato in UTC, ma quando torna dal database arriva senza "Z":
//senza correzione il browser lo leggerebbe come ora locale, sfasata del fuso orario
function withUtcCreatedAt(request: ExportRequest): ExportRequest {
  const hasTimeZone = /(Z|[+-]\d{2}:\d{2})$/.test(request.createdAt);
  return hasTimeZone ? request : { ...request, createdAt: request.createdAt + 'Z' };
}
