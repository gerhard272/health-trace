import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';
import { UserResponse } from '../models/auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const user: UserResponse = { id: 1, username: 'mario.rossi', firstName: 'Mario', lastName: 'Rossi', cf: 'RSSMRA80A01H501U' };

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores credentials and user only after a successful login', () => {
    service.login({ username: 'mario.rossi', password: 'Password123!' }).subscribe();

    expect(service.isAuthenticated()).toBe(false);
    http.expectOne(`${environment.apiUrl}/api/Auth/login`).flush(user);

    expect(service.getCredentials()).toBe(btoa('mario.rossi:Password123!'));
    expect(service.currentUser()).toEqual(user);
  });

  it('encodes non-Latin-1 passwords as UTF-8, like the API decodes them', () => {
    service.login({ username: 'mario', password: 'pàssword€' }).subscribe();
    http.expectOne(`${environment.apiUrl}/api/Auth/login`).flush(user);

    const decoded = new TextDecoder().decode(Uint8Array.from(atob(service.getCredentials()!), (c) => c.charCodeAt(0)));
    expect(decoded).toBe('mario:pàssword€');
  });

  it('stores nothing when the login is rejected', () => {
    service.login({ username: 'mario.rossi', password: 'wrong-password' }).subscribe({ error: () => undefined });
    http.expectOne(`${environment.apiUrl}/api/Auth/login`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
  });

  it('logout clears credentials and user', () => {
    service.login({ username: 'mario.rossi', password: 'Password123!' }).subscribe();
    http.expectOne(`${environment.apiUrl}/api/Auth/login`).flush(user);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });
});
