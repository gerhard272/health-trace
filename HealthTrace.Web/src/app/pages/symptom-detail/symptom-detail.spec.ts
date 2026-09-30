import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { SymptomDetail } from './symptom-detail';
import { environment } from '../../../environments/environment';

describe('SymptomDetail', () => {
  let component: SymptomDetail;
  let fixture: ComponentFixture<SymptomDetail>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SymptomDetail],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '5' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SymptomDetail);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  it('shows the symptom', async () => {
    http.expectOne(`${environment.apiUrl}/api/Symptom/5`).flush({ id: 5, userId: 7, eventName: 'Fever', description: '38.2', eventDate: '2026-09-13T21:00:00' });
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Fever');
  });

  it('shows a not-found message on 404', async () => {
    http.expectOne(`${environment.apiUrl}/api/Symptom/5`).flush({ status: 404, title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(component.notFound()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('does not exist');
  });
});
