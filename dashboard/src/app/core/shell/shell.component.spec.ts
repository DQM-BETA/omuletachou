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

  describe('cabeçalho/logo (Issue #209)', () => {
    it('exibe o texto "omuletachou" dentro de um elemento de logo dedicado', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      const logo = compiled.querySelector('[data-testid="shell-logo"]') as HTMLElement;
      expect(logo).toBeTruthy();
      expect(logo.textContent?.trim()).toBe('omuletachou');
    });

    it('mantém o cabeçalho fixo (sticky) no topo da barra lateral, fora da área de scroll da navegação', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      const toolbar = compiled.querySelector('.shell-toolbar') as HTMLElement;
      const navList = compiled.querySelector('.shell-nav-list') as HTMLElement;
      expect(toolbar).toBeTruthy();
      expect(navList).toBeTruthy();

      const toolbarStyle = getComputedStyle(toolbar);
      expect(toolbarStyle.position).toBe('sticky');
      expect(toolbarStyle.top).toBe('0px');
      expect(toolbarStyle.boxSizing).toBe('border-box');

      const navListStyle = getComputedStyle(navList);
      expect(navListStyle.overflowY).toBe('auto');
    });

    it('protege o texto do logo contra recorte/overflow (sem quebra de linha, com ellipsis)', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      const logo = compiled.querySelector('.shell-toolbar-logo') as HTMLElement;
      const logoStyle = getComputedStyle(logo);
      expect(logoStyle.whiteSpace).toBe('nowrap');
      expect(logoStyle.overflow).toBe('hidden');
      expect(logoStyle.textOverflow).toBe('ellipsis');
    });
  });
});
