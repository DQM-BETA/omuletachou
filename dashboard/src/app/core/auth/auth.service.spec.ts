import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, RouterTestingModule],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start unauthenticated when sessionStorage is empty', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getToken()).toBeNull();
  });

  it('CA-A1/CA-A3 — login com sucesso armazena o token em sessionStorage e no signal', () => {
    let completed = false;
    service.login('user@omuletachou.com.br', 'senha-correta').subscribe(() => (completed = true));

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'user@omuletachou.com.br', password: 'senha-correta' });
    req.flush({ token: 'fake-jwt-token' });

    expect(completed).toBeTrue();
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.getToken()).toBe('fake-jwt-token');
    expect(sessionStorage.getItem('omuletachou_token')).toBe('fake-jwt-token');
  });

  it('CA-A2 — login com credenciais inválidas não armazena token', () => {
    let errored = false;
    service.login('user@omuletachou.com.br', 'senha-errada').subscribe({
      error: () => (errored = true),
    });

    const req = httpMock.expectOne('/api/auth/login');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(errored).toBeTrue();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getToken()).toBeNull();
    expect(sessionStorage.getItem('omuletachou_token')).toBeNull();
  });

  it('CA-A7 — logout() limpa storage/signal e navega para /login com mensagem', () => {
    sessionStorage.setItem('omuletachou_token', 'existing-token');
    const navigateSpy = spyOn(router, 'navigate');
    // recria o service para ler o token já existente
    const svc = TestBed.inject(AuthService);

    svc.logout('Sessão expirada, faça login novamente');

    expect(svc.isAuthenticated()).toBeFalse();
    expect(sessionStorage.getItem('omuletachou_token')).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], {
      queryParams: { message: 'Sessão expirada, faça login novamente' },
    });
  });

  it('logout() sem mensagem navega para /login sem queryParams', () => {
    const navigateSpy = spyOn(router, 'navigate');
    service.logout();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: {} });
  });

  it('deve restaurar o token já presente no sessionStorage ao instanciar (reload)', () => {
    sessionStorage.setItem('omuletachou_token', 'reloaded-token');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, RouterTestingModule],
    });
    const svc = TestBed.inject(AuthService);
    expect(svc.isAuthenticated()).toBeTrue();
    expect(svc.getToken()).toBe('reloaded-token');
  });
});
