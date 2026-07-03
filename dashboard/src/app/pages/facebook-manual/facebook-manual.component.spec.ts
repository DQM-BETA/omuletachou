import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FacebookManualComponent } from './facebook-manual.component';

describe('FacebookManualComponent', () => {
  let component: FacebookManualComponent;
  let fixture: ComponentFixture<FacebookManualComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FacebookManualComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(FacebookManualComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
