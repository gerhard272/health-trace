import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';

import { ExportRequest, ExportRequestCreate } from '../models/export.models';

//calls to /api/exports: the request returns 202 immediately, the PDF is generated
//in the background by the Azure Function and can be downloaded once Completed
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

  //the file goes through the API (not a direct Blob URL), so the Basic Auth header is needed
  download(id: number): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/download`, { responseType: 'blob' });
  }
}

//createdAt is stored in UTC but comes back from the database without "Z":
//without this fix the browser would read it as local time, shifted by the time zone offset
function withUtcCreatedAt(request: ExportRequest): ExportRequest {
  const hasTimeZone = /(Z|[+-]\d{2}:\d{2})$/.test(request.createdAt);
  return hasTimeZone ? request : { ...request, createdAt: request.createdAt + 'Z' };
}
