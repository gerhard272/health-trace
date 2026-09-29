import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { authGuard, guestGuard } from './auth.guard';

describe('auth guards', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/export' } as RouterStateSnapshot;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

  const run = (guard: typeof authGuard) => TestBed.runInInjectionContext(() => guard(route, state));
  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('authGuard redirects to login with returnUrl when not authenticated', () => {
    expect(serialize(run(authGuard))).toBe('/login?returnUrl=%2Fexport');
  });

  it('authGuard lets an authenticated user through', () => {
    sessionStorage.setItem('basicAuthCredentials', btoa('mario:Password123!'));
    expect(run(authGuard)).toBe(true);
  });

  it('guestGuard sends an authenticated user to the symptoms page', () => {
    sessionStorage.setItem('basicAuthCredentials', btoa('mario:Password123!'));
    expect(serialize(run(guestGuard))).toBe('/symptoms');
  });

  it('guestGuard lets an anonymous user open login and register', () => {
    expect(run(guestGuard)).toBe(true);
  });
});
