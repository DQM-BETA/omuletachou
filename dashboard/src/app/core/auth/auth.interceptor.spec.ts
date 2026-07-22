import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { RouterTestingModule } from '@angular/router/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [RouterTestingModule],
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  }

  beforeEach(() => {
    sessionStorage.clear();
    setup();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('CA-A6 — anexa o header Authorization quando há token', () => {
    sessionStorage.setItem('omuletachou_token', 'my-token');
    TestBed.resetTestingModule();
    setup();

    httpClient.get('/api/products').subscribe();
    const req = httpMock.expectOne('/api/products');
    expect(req.request.headers.get('Authorization')).toBe('Bearer my-token');
    req.flush({});
  });

  it('não anexa header quando não há token', () => {
    httpClient.get('/api/products').subscribe();
    const req = httpMock.expectOne('/api/products');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('CA-A7 — 401 fora de /api/auth/login dispara logout()', () => {
    const logoutSpy = spyOn(authService, 'logout');
    httpClient.get('/api/products').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/products');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(logoutSpy).toHaveBeenCalledWith('Sessão expirada, faça login novamente');
  });

  it('401 em /api/auth/login NÃO dispara logout()', () => {
    const logoutSpy = spyOn(authService, 'logout');
    httpClient.post('/api/auth/login', { email: 'a', password: 'b' }).subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/auth/login');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(logoutSpy).not.toHaveBeenCalled();
  });
});
