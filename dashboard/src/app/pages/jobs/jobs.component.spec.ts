import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatSnackBarModule } from '@angular/material/snack-bar';

import { JobsComponent } from './jobs.component';
import { JobLastExecutionDto } from '../../core/services/jobs.service';

describe('JobsComponent', () => {
  let component: JobsComponent;
  let fixture: ComponentFixture<JobsComponent>;
  let httpMock: HttpTestingController;

  async function setup(initialExecutions: JobLastExecutionDto[] = []): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [JobsComponent, HttpClientTestingModule, NoopAnimationsModule, MatSnackBarModule],
    }).compileComponents();

    fixture = TestBed.createComponent(JobsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/jobs/last-executions');
    expect(req.request.method).toBe('GET');
    req.flush(initialExecutions);
    fixture.detectChanges();
  }

  function flushLastExecutions(dtos: JobLastExecutionDto[] = []): void {
    const req = httpMock.expectOne('/api/jobs/last-executions');
    expect(req.request.method).toBe('GET');
    req.flush(dtos);
    fixture.detectChanges();
  }

  afterEach(() => {
    httpMock.verify();
  });

  describe('comportamento básico e disparo de jobs (regressão)', () => {
    beforeEach(async () => setup());

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
      flushLastExecutions();

      expect(job.triggering).toBeFalse();
      expect(job.lastResult).toBe('success');
    });

    it('CA-C7 — dispara o endpoint correto do collector por plataforma (amazon)', () => {
      const job = component.jobs.find(j => j.kind === 'collector-amazon')!;
      component.trigger(job);

      const req = httpMock.expectOne('/api/jobs/collector/amazon/trigger');
      expect(req.request.method).toBe('POST');
      req.flush({});
      flushLastExecutions();
    });

    it('CA-C7 — dispara o endpoint do processor', () => {
      const job = component.jobs.find(j => j.kind === 'processor')!;
      component.trigger(job);

      const req = httpMock.expectOne('/api/jobs/processor/trigger');
      expect(req.request.method).toBe('POST');
      req.flush({});
      flushLastExecutions();
      expect(job.lastResult).toBe('success');
    });

    it('CA-C7 — dispara o endpoint do publisher', () => {
      const job = component.jobs.find(j => j.kind === 'publisher')!;
      component.trigger(job);

      const req = httpMock.expectOne('/api/jobs/publisher/trigger');
      expect(req.request.method).toBe('POST');
      req.flush({});
      flushLastExecutions();
      expect(job.lastResult).toBe('success');
    });

    it('CA-C8 — exibe o resultado de sucesso da última execução sem travar a UI', () => {
      const job = component.jobs[0];
      component.trigger(job);
      expect(job.triggering).toBeTrue();

      const req = httpMock.expectOne('/api/jobs/collector/trigger');
      req.flush({ count: 7 });
      flushLastExecutions();

      expect(job.triggering).toBeFalse();
      expect(job.lastResult).toBe('success');
      expect(job.lastMessage).toContain('7 itens');
    });

    it('CA-C8 — exibe o resultado de erro da última execução em caso de falha HTTP', () => {
      const job = component.jobs[0];
      component.trigger(job);

      const req = httpMock.expectOne('/api/jobs/collector/trigger');
      req.flush('erro', { status: 500, statusText: 'Internal Server Error' });
      flushLastExecutions();

      expect(job.triggering).toBeFalse();
      expect(job.lastResult).toBe('error');
      expect(job.lastMessage).toBeTruthy();
    });
  });

  describe('última execução — exibição no card (ISSUE-237)', () => {
    it('CA 1.1/1.3 — após ngOnInit, job com última execução "success" exibe status e timestamps do backend', async () => {
      await setup([
        {
          jobName: 'collector',
          status: 'success',
          startedAt: '2026-08-19T10:00:00Z',
          finishedAt: '2026-08-19T10:02:15Z',
          errorMessage: null,
        },
      ]);

      const job = component.jobs.find(j => j.kind === 'collector')!;
      expect(job.lastExecutionStatus).toBe('success');
      expect(job.lastExecutionStartedAt).toBe('2026-08-19T10:00:00Z');
      expect(job.lastExecutionFinishedAt).toBe('2026-08-19T10:02:15Z');

      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Sucesso');
    });

    it('CA 2.1/2.2 — última execução "failed" exibe indicador de falha e a mensagem de erro, nunca sucesso', async () => {
      await setup([
        {
          jobName: 'processor',
          status: 'failed',
          startedAt: '2026-08-19T09:00:00Z',
          finishedAt: '2026-08-19T09:00:05Z',
          errorMessage: 'Credenciais inválidas',
        },
      ]);

      const job = component.jobs.find(j => j.kind === 'processor')!;
      expect(job.lastExecutionStatus).toBe('failed');
      expect(job.lastExecutionError).toBe('Credenciais inválidas');

      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Falha');
      expect(text).toContain('Credenciais inválidas');
      expect(text).not.toContain('Sucesso');
    });

    it('CA 3.1 — job com status null exibe "Nenhuma execução ainda" sem erro nem data mal formatada', async () => {
      await setup([
        { jobName: 'publisher', status: null, startedAt: null, finishedAt: null, errorMessage: null },
      ]);

      const job = component.jobs.find(j => j.kind === 'publisher')!;
      expect(job.lastExecutionStatus).toBeNull();

      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Nenhuma execução ainda');
    });

    it('job sem entrada no array retornado pelo backend permanece como "nunca executado" (robustez)', async () => {
      await setup([]);

      component.jobs.forEach(job => {
        expect(job.lastExecutionStatus).toBeNull();
      });
      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Nenhuma execução ainda');
    });

    it('especificacao-tecnica.md §1.1 — status "running" é tratado com rótulo neutro, sem quebrar o template', async () => {
      await setup([
        {
          jobName: 'collector-amazon',
          status: 'running',
          startedAt: '2026-08-19T11:00:00Z',
          finishedAt: null,
          errorMessage: null,
        },
      ]);

      const job = component.jobs.find(j => j.kind === 'collector-amazon')!;
      expect(job.lastExecutionStatus).toBe('running');

      expect(() => fixture.detectChanges()).not.toThrow();
      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Em execução');
      expect(text).not.toContain('Sucesso');
      expect(text).not.toContain('Falha');
    });
  });

  describe('refetch após disparo manual (especificacao-tecnica.md §1.2)', () => {
    beforeEach(async () => setup());

    it('refaz GET /api/jobs/last-executions após trigger() concluir com sucesso e mescla o novo estado no card', () => {
      const job = component.jobs.find(j => j.kind === 'collector')!;

      component.trigger(job);
      const triggerReq = httpMock.expectOne('/api/jobs/collector/trigger');
      triggerReq.flush({ count: 1 });

      const refetchReq = httpMock.expectOne('/api/jobs/last-executions');
      expect(refetchReq.request.method).toBe('GET');
      refetchReq.flush([
        {
          jobName: 'collector',
          status: 'success',
          startedAt: '2026-08-19T12:00:00Z',
          finishedAt: '2026-08-19T12:00:05Z',
          errorMessage: null,
        },
      ]);

      expect(job.lastExecutionStatus).toBe('success');
      expect(job.lastExecutionStartedAt).toBe('2026-08-19T12:00:00Z');
      expect(job.lastExecutionFinishedAt).toBe('2026-08-19T12:00:05Z');
    });

    it('refaz GET /api/jobs/last-executions mesmo quando trigger() falha (branch error do subscribe)', () => {
      const job = component.jobs.find(j => j.kind === 'publisher')!;

      component.trigger(job);
      const triggerReq = httpMock.expectOne('/api/jobs/publisher/trigger');
      triggerReq.flush('erro', { status: 500, statusText: 'Internal Server Error' });

      const refetchReq = httpMock.expectOne('/api/jobs/last-executions');
      refetchReq.flush([
        {
          jobName: 'publisher',
          status: 'failed',
          startedAt: '2026-08-19T12:10:00Z',
          finishedAt: '2026-08-19T12:10:02Z',
          errorMessage: 'Falha ao publicar',
        },
      ]);

      expect(job.lastExecutionStatus).toBe('failed');
      expect(job.lastExecutionError).toBe('Falha ao publicar');
    });
  });
});
