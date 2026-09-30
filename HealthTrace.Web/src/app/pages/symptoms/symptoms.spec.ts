import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Symptoms } from './symptoms';
import { environment } from '../../../environments/environment';

describe('Symptoms', () => {
  const apiUrl = `${environment.apiUrl}/api/Symptom`;
  let component: Symptoms;
  let fixture: ComponentFixture<Symptoms>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Symptoms],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Symptoms);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the symptoms, most recent first', async () => {
    http.expectOne(apiUrl).flush([
      { id: 1, userId: 7, eventName: 'Headache', eventDate: '2026-09-12T08:30:00' },
      { id: 2, userId: 7, eventName: 'Fever', eventDate: '2026-09-13T21:00:00' },
    ]);
    await fixture.whenStable();

    expect(component.symptoms().map((symptom) => symptom.id)).toEqual([2, 1]);
    expect(fixture.nativeElement.textContent).toContain('Fever');
  });

  it('refuses date and name together without calling the API', () => {
    http.expectOne(apiUrl).flush([]);
    component.filterForm.setValue({ date: '2026-09-12', name: 'Fever' });
    component.load();

    http.expectNone(() => true);
    expect(component.errorMessage()).toContain('not both');
  });

  it('removes a symptom from the list after the delete', () => {
    http.expectOne(apiUrl).flush([{ id: 1, userId: 7, eventName: 'Headache', eventDate: '2026-09-12T08:30:00' }]);
    component.confirmDelete(1);
    http.expectOne(`${apiUrl}/1`).flush(null, { status: 204, statusText: 'No Content' });

    expect(component.symptoms()).toEqual([]);
  });
});
