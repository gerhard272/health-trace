import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExportService } from './export.service';
import { environment } from '../../environments/environment';
import { ExportRequest } from '../models/export.models';

describe('ExportService', () => {
  const apiUrl = `${environment.apiUrl}/api/exports`;
  let service: ExportService;
  let http: HttpTestingController;

  const exportRequest = (createdAt: string): ExportRequest => ({ id: 3, status: 'Pending', createdAt, symptomIds: [] });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ExportService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requestExport posts the criteria to /request', () => {
    service.requestExport({ fromDate: '2026-09-01' }).subscribe();
    const req = http.expectOne(`${apiUrl}/request`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ fromDate: '2026-09-01' });
    req.flush(exportRequest('2026-09-29T10:00:00Z'));
  });

  it('marks createdAt without a time zone as UTC', () => {
    let history: ExportRequest[] = [];
    service.getHistory().subscribe((result) => (history = result));
    http.expectOne(apiUrl).flush([exportRequest('2026-09-29T10:00:00'), exportRequest('2026-09-29T10:00:00Z')]);

    expect(history.map((item) => item.createdAt)).toEqual(['2026-09-29T10:00:00Z', '2026-09-29T10:00:00Z']);
  });

  it('download asks for a blob', () => {
    service.download(3).subscribe();
    const req = http.expectOne(`${apiUrl}/3/download`);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['%PDF']));
  });
});
