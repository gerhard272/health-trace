import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { Login } from './login';
import { environment } from '../../../environments/environment';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let http: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('shows the credentials error on 401', async () => {
    component.loginForm.setValue({ username: 'mario.rossi', password: 'wrong-password' });
    component.onSubmit();
    http.expectOne(`${environment.apiUrl}/api/Auth/login`).flush(null, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    //the app is zoneless: the message must appear in the DOM, not just in the component
    expect(fixture.nativeElement.textContent).toContain('Username or password is incorrect.');
  });

  it('navigates to the diary after a successful login', () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    component.loginForm.setValue({ username: 'mario.rossi', password: 'Password123!' });
    component.onSubmit();
    http.expectOne(`${environment.apiUrl}/api/Auth/login`).flush({ id: 1, username: 'mario.rossi', firstName: 'Mario', lastName: 'Rossi', cf: 'RSSMRA80A01H501U' });

    expect(navigate).toHaveBeenCalledWith('/symptoms');
  });
});
