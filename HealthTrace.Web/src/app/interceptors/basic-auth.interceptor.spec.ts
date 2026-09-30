import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { basicAuthInterceptor } from './basic-auth.interceptor';
import { environment } from '../../environments/environment';

describe('basicAuthInterceptor', () => {
  const credentials = btoa('mario:Password123!');
  let http: HttpClient;
  let controller: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([basicAuthInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => controller.verify());

  it('adds the Basic header to API requests when logged in', () => {
    sessionStorage.setItem('basicAuthCredentials', credentials);
    http.get(`${environment.apiUrl}/api/Symptom`).subscribe();

    const req = controller.expectOne(`${environment.apiUrl}/api/Symptom`);
    expect(req.request.headers.get('Authorization')).toBe(`Basic ${credentials}`);
    req.flush([]);
  });

  it('never sends the credentials to other hosts', () => {
    sessionStorage.setItem('basicAuthCredentials', credentials);
    http.get('https://example.com/data').subscribe();

    const req = controller.expectOne('https://example.com/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('sends requests unchanged when not logged in', () => {
    http.get(`${environment.apiUrl}/api/Symptom`).subscribe({ error: () => undefined });

    const req = controller.expectOne(`${environment.apiUrl}/api/Symptom`);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('logs out and goes to login on a 401 from a protected endpoint', () => {
    sessionStorage.setItem('basicAuthCredentials', credentials);
    http.get(`${environment.apiUrl}/api/Symptom`).subscribe({ error: () => undefined });

    controller.expectOne(`${environment.apiUrl}/api/Symptom`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('basicAuthCredentials')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], expect.anything());
  });

  it('leaves the 401 of the login call to the component', () => {
    http.post(`${environment.apiUrl}/api/Auth/login`, {}).subscribe({ error: () => undefined });

    controller.expectOne(`${environment.apiUrl}/api/Auth/login`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).not.toHaveBeenCalled();
  });
});
