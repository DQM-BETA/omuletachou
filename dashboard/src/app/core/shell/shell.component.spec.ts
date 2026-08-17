import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ShellComponent } from './shell.component';
import { AuthService } from '../auth/auth.service';

describe('ShellComponent', () => {
  let component: ShellComponent;
  let fixture: ComponentFixture<ShellComponent>;
  let authServiceStub: { logout: jasmine.Spy };

  beforeEach(async () => {
    authServiceStub = { logout: jasmine.createSpy('logout') };

    await TestBed.configureTestingModule({
      imports: [ShellComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [{ provide: AuthService, useValue: authServiceStub }],
    }).compileComponents();

    fixture = TestBed.createComponent(ShellComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('exibe as 7 páginas do menu lateral (Issue #185: inclui Links ML)', () => {
    expect(component.navItems.length).toBe(7);
    const compiled = fixture.nativeElement as HTMLElement;
    const items = compiled.querySelectorAll('[data-testid="nav-item"]');
    expect(items.length).toBe(7);
    expect(component.navItems).toContain(
      jasmine.objectContaining({ label: 'Links ML', path: '/mercadolivre-links' })
    );
  });

  it('botão de logout chama AuthService.logout()', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const button = compiled.querySelector('[data-testid="logout-button"]') as HTMLButtonElement;
    button.click();
    expect(authServiceStub.logout).toHaveBeenCalled();
  });
});
