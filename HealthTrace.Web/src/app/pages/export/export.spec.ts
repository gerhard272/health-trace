import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Export } from './export';
import { environment } from '../../../environments/environment';

describe('Export', () => {
  const exportsUrl = `${environment.apiUrl}/api/exports`;
  let component: Export;
  let fixture: ComponentFixture<Export>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Export],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Export);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/api/Symptom`).flush([]);
    http.expectOne(exportsUrl).flush([]);
  });

  afterEach(() => fixture.destroy());

  it('requests an export of all symptoms with an empty body', () => {
    component.onRequest();

    const req = http.expectOne(`${exportsUrl}/request`);
    expect(req.request.body).toEqual({});
    req.flush({ id: 1, status: 'Pending', createdAt: '2026-09-29T10:00:00Z', symptomIds: [] });
    expect(component.history().map((item) => item.id)).toEqual([1]);
  });

  it('refuses a date range with the start after the end', () => {
    component.exportForm.setValue({ scope: 'range', fromDate: '2026-09-30', toDate: '2026-09-01' });
    component.onRequest();

    http.expectNone(`${exportsUrl}/request`);
    expect(component.requestError()).toContain('cannot be later');
  });

  it('requires at least one symptom when choosing symptoms', () => {
    component.exportForm.patchValue({ scope: 'selection' });
    component.onRequest();

    http.expectNone(`${exportsUrl}/request`);
    expect(component.requestError()).toContain('at least one');
  });

  it('sends only the selected symptom ids', () => {
    component.exportForm.patchValue({ scope: 'selection' });
    component.toggleSymptom(4);
    component.toggleSymptom(7);
    component.onRequest();

    const req = http.expectOne(`${exportsUrl}/request`);
    expect(req.request.body).toEqual({ symptomIds: [4, 7] });
    req.flush({ id: 2, status: 'Pending', createdAt: '2026-09-29T10:00:00Z', symptomIds: [4, 7] });
  });

  it('describes the export criteria', () => {
    const base = { id: 1, status: 'Completed' as const, createdAt: '', symptomIds: [] as number[] };
    expect(component.describeCriteria(base)).toBe('All symptoms');
    expect(component.describeCriteria({ ...base, symptomIds: [1, 2] })).toBe('2 selected symptoms');
    expect(component.describeCriteria({ ...base, fromDate: '2026-09-01T00:00:00', toDate: '2026-09-30T00:00:00' })).toBe('From 01/09/2026 to 30/09/2026');
  });
});
