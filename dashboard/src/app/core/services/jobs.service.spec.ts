import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import { JobsService } from './jobs.service';

describe('JobsService', () => {
  let service: JobsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [JobsService],
    });
    service = TestBed.inject(JobsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('deve disparar o job collector geral via POST /api/jobs/collector/trigger', () => {
    service.trigger('collector').subscribe(result => {
      expect(result).toEqual({ count: 5 });
    });

    const req = httpMock.expectOne('/api/jobs/collector/trigger');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ count: 5 });
  });

  it('deve disparar o job collector por plataforma (amazon)', () => {
    service.trigger('collector-amazon').subscribe();

    const req = httpMock.expectOne('/api/jobs/collector/amazon/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('deve disparar o job collector por plataforma (mercadolivre)', () => {
    service.trigger('collector-mercadolivre').subscribe();

    const req = httpMock.expectOne('/api/jobs/collector/mercadolivre/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('deve disparar o job collector por plataforma (shopee)', () => {
    service.trigger('collector-shopee').subscribe();

    const req = httpMock.expectOne('/api/jobs/collector/shopee/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('deve disparar o job processor via POST /api/jobs/processor/trigger', () => {
    service.trigger('processor').subscribe();

    const req = httpMock.expectOne('/api/jobs/processor/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('deve disparar o job publisher via POST /api/jobs/publisher/trigger', () => {
    service.trigger('publisher').subscribe();

    const req = httpMock.expectOne('/api/jobs/publisher/trigger');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('deve propagar erro HTTP sem quebrar o observable (tratado pelo componente)', () => {
    let errorReceived: unknown = null;

    service.trigger('publisher').subscribe({
      next: () => fail('não deveria emitir sucesso'),
      error: err => (errorReceived = err),
    });

    const req = httpMock.expectOne('/api/jobs/publisher/trigger');
    req.flush('erro interno', { status: 500, statusText: 'Internal Server Error' });

    expect(errorReceived).toBeTruthy();
  });
});
