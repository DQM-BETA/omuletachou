import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { QueueService, QueueItem } from './queue.service';
import { PagedResult } from './paged-result.model';

describe('QueueService', () => {
  let service: QueueService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(QueueService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('CA-B6 — list() chama GET /api/queue e retorna itens com status', () => {
    const mockResponse: PagedResult<QueueItem> = {
      items: [
        {
          id: 'q1',
          productId: 'p1',
          socialNetwork: 'Telegram',
          status: 'Failed',
          scheduledAt: '2026-07-01T00:00:00Z',
          publishedAt: null,
          retryCount: 1,
          errorMessage: 'timeout',
          createdAt: '2026-06-30T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    };

    let result: PagedResult<QueueItem> | undefined;
    service.list({}).subscribe(res => (result = res));

    const req = httpMock.expectOne(r => r.url === '/api/queue');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);

    expect(result?.items[0].status).toBe('Failed');
  });

  it('CA-B8 — list() envia filtros de rede/status sem parâmetros vazios', () => {
    service.list({ status: 'Failed', network: 'Telegram' }).subscribe();

    const req = httpMock.expectOne(
      r => r.url === '/api/queue' && r.params.get('status') === 'Failed' && r.params.get('network') === 'Telegram'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  });

  it('list() não envia parâmetros vazios (cleanParams)', () => {
    service.list({ status: '', network: undefined }).subscribe();

    const req = httpMock.expectOne('/api/queue');
    expect(req.request.params.keys().length).toBe(0);
    req.flush({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  });

  it('listManualPending() chama GET /api/queue/manual', () => {
    service.listManualPending({}).subscribe();

    const req = httpMock.expectOne(r => r.url === '/api/queue/manual');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  });

  it('CA-B7 — retry() chama POST /api/queue/{id}/retry', () => {
    let completed = false;
    service.retry('q1').subscribe(() => (completed = true));

    const req = httpMock.expectOne('/api/queue/q1/retry');
    expect(req.request.method).toBe('POST');
    req.flush(null);

    expect(completed).toBeTrue();
  });

  it('markPublished() chama PATCH /api/queue/{id}/status com status Published', () => {
    let completed = false;
    service.markPublished('q1').subscribe(() => (completed = true));

    const req = httpMock.expectOne('/api/queue/q1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'Published' });
    req.flush(null);

    expect(completed).toBeTrue();
  });
});
