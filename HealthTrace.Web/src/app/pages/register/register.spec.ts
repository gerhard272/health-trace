import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { Register } from './register';
import { environment } from '../../../environments/environment';

describe('Register', () => {
  let component: Register;
  let fixture: ComponentFixture<Register>;
  let http: HttpTestingController;

  const validForm = {
    username: 'mario.rossi',
    password: 'Password123!',
    passwordConfirmation: 'Password123!',
    firstName: 'Mario',
    lastName: 'Rossi',
    cf: 'RSSMRA80A01H501U',
    birthDate: '',
    birthPlace: '',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Register);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('omits empty optional fields instead of sending empty strings', () => {
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component.registerForm.setValue(validForm);
    component.onSubmit();

    const req = http.expectOne(`${environment.apiUrl}/api/Auth/register`);
    expect(req.request.body.birthDate).toBeUndefined();
    expect(req.request.body.birthPlace).toBeUndefined();
    req.flush({ id: 1 });
  });

  it('converts the fiscal code to upper case', () => {
    component.registerForm.get('cf')!.setValue('rssmra80a01h501u');
    expect(component.registerForm.get('cf')!.value).toBe('RSSMRA80A01H501U');
  });

  it('shows the API validation errors on 400', async () => {
    component.registerForm.setValue(validForm);
    component.onSubmit();
    http.expectOne(`${environment.apiUrl}/api/Auth/register`).flush(
      { status: 400, title: 'Validation failed', errors: { username: ['Username already in use'] } },
      { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Username already in use');
  });
});
