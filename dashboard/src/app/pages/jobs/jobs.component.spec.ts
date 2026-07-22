import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatSnackBarModule } from '@angular/material/snack-bar';

import { JobsComponent } from './jobs.component';

describe('JobsComponent', () => {
  let component: JobsComponent;
  let fixture: ComponentFixture<JobsComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobsComponent, HttpClientTestingModule, NoopAnimationsModule, MatSnackBarModule],
    }).compileComponents();

    fixture = TestBed.createComponent(JobsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('deve expor os 6 jobs esperados (CA-C7)', () => {
    const kinds = component.jobs.map(job => job.kind);
    expect(kinds).toEqual([
      'collector',
      'collector-amazon',
      'collector-mercadolivre',
      'collector-shopee',
      'processor',
      'publisher',
    ]);
  });

  it('CA-C7 — dispara POST /api/jobs/collector/trigger ao clicar em disparar o job collector geral', () => {
    const job = component.jobs[0];
    component.trigger(job);

    const req = httpMock.expectOne('/api/jobs/collector/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({ count: 3 });

    expect(job.triggering).toBeFalse();
    expect(job.lastResult).toBe('success');
  });

  it('CA-C7 — dispara o endpoint correto do collector por plataforma (amazon)', () => {
    const job = component.jobs.find(j => j.kind === 'collector-amazon')!;
    component.trigger(job);

    const req = httpMock.expectOne('/api/jobs/collector/amazon/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('CA-C7 — dispara o endpoint do processor', () => {
    const job = component.jobs.find(j => j.kind === 'processor')!;
    component.trigger(job);

    const req = httpMock.expectOne('/api/jobs/processor/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
    expect(job.lastResult).toBe('success');
  });

  it('CA-C7 — dispara o endpoint do publisher', () => {
    const job = component.jobs.find(j => j.kind === 'publisher')!;
    component.trigger(job);

    const req = httpMock.expectOne('/api/jobs/publisher/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
    expect(job.lastResult).toBe('success');
  });

  it('CA-C8 — exibe o resultado de sucesso da última execução sem travar a UI', () => {
    const job = component.jobs[0];
    component.trigger(job);
    expect(job.triggering).toBeTrue();

    const req = httpMock.expectOne('/api/jobs/collector/trigger');
    req.flush({ count: 7 });

    expect(job.triggering).toBeFalse();
    expect(job.lastResult).toBe('success');
    expect(job.lastMessage).toContain('7 itens');
  });

  it('CA-C8 — exibe o resultado de erro da última execução em caso de falha HTTP', () => {
    const job = component.jobs[0];
    component.trigger(job);

    const req = httpMock.expectOne('/api/jobs/collector/trigger');
    req.flush('erro', { status: 500, statusText: 'Internal Server Error' });

    expect(job.triggering).toBeFalse();
    expect(job.lastResult).toBe('error');
    expect(job.lastMessage).toBeTruthy();
  });
});
