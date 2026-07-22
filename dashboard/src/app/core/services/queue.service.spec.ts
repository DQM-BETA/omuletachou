import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import { QueueService, QueueItem } from './queue.service';
import { PagedResult } from './paged-result.model';

describe('QueueService', () => {
  let service: QueueService;
  let httpMock: HttpTestingController;

  const sampleItem: QueueItem = {
    id: '1',
    productId: 'p1',
    socialNetwork: 'Facebook',
    status: 'ManualPending',
    scheduledAt: '2026-01-01T00:00:00Z',
    publishedAt: null,
    retryCount: 0,
    errorMessage: null,
    createdAt: '2026-01-01T00:00:00Z',
  };

  const pagedResult: PagedResult<QueueItem> = {
    items: [sampleItem],
    page: 1,
    pageSize: 10,
    totalItems: 1,
    totalPages: 1,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(QueueService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('list() chama GET /api/queue com filtros limpos', () => {
    service.list({ status: 'Failed', network: undefined, page: 1, pageSize: 10 }).subscribe((res) => {
      expect(res).toEqual(pagedResult);
    });

    const req = httpMock.expectOne(
      (r) => r.url === '/api/queue' && r.params.get('status') === 'Failed' && !r.params.has('network')
    );
    expect(req.request.method).toBe('GET');
    req.flush(pagedResult);
  });

  it('listManualPending() chama GET /api/queue/manual', () => {
    service.listManualPending({ page: 1, pageSize: 20 }).subscribe((res) => {
      expect(res).toEqual(pagedResult);
    });

    const req = httpMock.expectOne((r) => r.url === '/api/queue/manual');
    expect(req.request.method).toBe('GET');
    req.flush(pagedResult);
  });

  it('retry() chama POST /api/queue/{id}/retry', () => {
    service.retry('1').subscribe();

    const req = httpMock.expectOne('/api/queue/1/retry');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('markPublished() chama PATCH /api/queue/{id}/status com status Published', () => {
    service.markPublished('1').subscribe();

    const req = httpMock.expectOne('/api/queue/1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'Published' });
    req.flush(null);
  });
});
