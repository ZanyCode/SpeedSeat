import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ManualControlComponent } from './manual-control.component';

describe('ManualControlComponent', () => {
  let component: ManualControlComponent;
  let fixture: ComponentFixture<ManualControlComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ManualControlComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ManualControlComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
