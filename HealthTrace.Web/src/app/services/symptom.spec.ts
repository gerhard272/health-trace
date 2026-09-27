import { TestBed } from '@angular/core/testing';
import { Symptom } from './symptom';

describe('Symptom', () => {
  let service: Symptom;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Symptom);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
