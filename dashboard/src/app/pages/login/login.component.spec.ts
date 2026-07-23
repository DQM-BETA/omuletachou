import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({}) },
          },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('CA-A1 — login com sucesso navega para /products', () => {
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    component.form.setValue({ email: 'user@omuletachou.com.br', password: 'senha-correta' });

    component.submit();

    const req = httpMock.expectOne('/api/auth/login');
    req.flush({ token: 'fake-jwt-token' });

    expect(navigateSpy).toHaveBeenCalledWith(['/products']);
    expect(component.errorMessage).toBeNull();
  });

  it('CA-A2 — login com credenciais inválidas exibe mensagem de erro', () => {
    component.form.setValue({ email: 'user@omuletachou.com.br', password: 'senha-errada' });

    component.submit();

    const req = httpMock.expectOne('/api/auth/login');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(component.errorMessage).toBe('Email ou senha inválidos');
    expect(component.loading).toBeFalse();
  });

  it('não dispara chamada HTTP quando o formulário é inválido', () => {
    component.form.setValue({ email: '', password: '' });
    component.submit();
    httpMock.expectNone('/api/auth/login');
    expect(component.form.invalid).toBeTrue();
  });
});
