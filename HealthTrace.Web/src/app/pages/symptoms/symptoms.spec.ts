import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Symptoms } from './symptoms';

describe('Symptoms', () => {
  let component: Symptoms;
  let fixture: ComponentFixture<Symptoms>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Symptoms],
    }).compileComponents();

    fixture = TestBed.createComponent(Symptoms);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
