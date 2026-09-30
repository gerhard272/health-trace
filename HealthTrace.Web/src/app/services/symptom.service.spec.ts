import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SymptomService } from './symptom.service';
import { environment } from '../../environments/environment';

describe('SymptomService', () => {
  const apiUrl = `${environment.apiUrl}/api/Symptom`;
  let service: SymptomService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SymptomService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll without filters sends no query parameters', () => {
    service.getAll().subscribe();
    const req = http.expectOne(apiUrl);
    expect(req.request.params.keys()).toEqual([]);
    req.flush([]);
  });

  it('getAll passes the date filter', () => {
    service.getAll({ date: '2026-09-23' }).subscribe();
    http.expectOne(`${apiUrl}?date=2026-09-23`).flush([]);
  });

  it('getAll passes the name filter', () => {
    service.getAll({ name: 'Headache' }).subscribe();
    http.expectOne(`${apiUrl}?name=Headache`).flush([]);
  });

  it('create posts the symptom', () => {
    const request = { eventName: 'Headache', description: null, eventDate: '2026-09-23T08:30' };
    service.create(request).subscribe();
    const req = http.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ id: 1, userId: 7, ...request });
  });

  it('update puts to /{id} with the same id in the body', () => {
    service.update(5, { eventName: 'Fever', eventDate: '2026-09-23T21:00' }).subscribe();
    const req = http.expectOne(`${apiUrl}/5`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.id).toBe(5);
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('delete calls DELETE /{id}', () => {
    service.delete(5).subscribe();
    const req = http.expectOne(`${apiUrl}/5`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
