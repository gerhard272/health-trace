import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { SymptomForm } from './symptom-form';
import { environment } from '../../../environments/environment';

describe('SymptomForm', () => {
  const apiUrl = `${environment.apiUrl}/api/Symptom`;
  let http: HttpTestingController;

  async function create(params: Record<string, string>): Promise<ComponentFixture<SymptomForm>> {
    await TestBed.configureTestingModule({
      imports: [SymptomForm],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(params) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    return TestBed.createComponent(SymptomForm);
  }

  it('rejects a blank event name', async () => {
    const component = (await create({})).componentInstance;
    component.symptomForm.patchValue({ eventName: '   ' });

    expect(component.symptomForm.get('eventName')!.hasError('blank')).toBe(true);
  });

  it('creates a symptom with trimmed values', async () => {
    const component = (await create({})).componentInstance;
    component.symptomForm.setValue({ eventName: ' Headache ', description: '  ', eventDate: '2026-09-12T08:30' });
    component.onSubmit();

    const req = http.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ eventName: 'Headache', description: null, eventDate: '2026-09-12T08:30' });
    req.flush({ id: 9, userId: 7, eventName: 'Headache', eventDate: '2026-09-12T08:30:00' });
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/symptoms', 9]);
  });

  it('loads the symptom and saves it with PUT in edit mode', async () => {
    const component = (await create({ id: '5' })).componentInstance;
    http.expectOne(`${apiUrl}/5`).flush({ id: 5, userId: 7, eventName: 'Fever', description: null, eventDate: '2026-09-13T21:00:00' });

    expect(component.symptomForm.get('eventDate')!.value).toBe('2026-09-13T21:00');
    component.onSubmit();

    const req = http.expectOne(`${apiUrl}/5`);
    expect(req.request.method).toBe('PUT');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
