import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { authGuard, loginGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard / loginGuard', () => {
  let router: Router;
  let authServiceStub: { isAuthenticated: () => boolean };

  const runGuard = (guard: typeof authGuard) =>
    TestBed.runInInjectionContext(() => guard({} as any, {} as any));

  beforeEach(() => {
    authServiceStub = { isAuthenticated: () => false };
    TestBed.configureTestingModule({
      imports: [RouterTestingModule, HttpClientTestingModule],
      providers: [{ provide: AuthService, useFactory: () => authServiceStub }],
    });
    router = TestBed.inject(Router);
  });

  describe('authGuard (CA-A4)', () => {
    it('permite acesso quando autenticado', () => {
      authServiceStub.isAuthenticated = () => true;
      const navigateSpy = spyOn(router, 'navigate');
      expect(runGuard(authGuard)).toBeTrue();
      expect(navigateSpy).not.toHaveBeenCalled();
    });

    it('bloqueia e redireciona para /login quando não autenticado', () => {
      authServiceStub.isAuthenticated = () => false;
      const navigateSpy = spyOn(router, 'navigate');
      expect(runGuard(authGuard)).toBeFalse();
      expect(navigateSpy).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('loginGuard (CA-A5)', () => {
    it('permite exibir /login quando não autenticado', () => {
      authServiceStub.isAuthenticated = () => false;
      const navigateSpy = spyOn(router, 'navigate');
      expect(runGuard(loginGuard)).toBeTrue();
      expect(navigateSpy).not.toHaveBeenCalled();
    });

    it('redireciona para /products quando já autenticado', () => {
      authServiceStub.isAuthenticated = () => true;
      const navigateSpy = spyOn(router, 'navigate');
      expect(runGuard(loginGuard)).toBeFalse();
      expect(navigateSpy).toHaveBeenCalledWith(['/products']);
    });
  });
});
