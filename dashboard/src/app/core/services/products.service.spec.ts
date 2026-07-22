import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ProductsService, ProductListItem, ProductDetail } from './products.service';
import { PagedResult } from './paged-result.model';

describe('ProductsService', () => {
  let service: ProductsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(ProductsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('CA-B1/CA-B2 — list() chama GET /api/products com ai_score/ai_reason no payload', () => {
    const mockResponse: PagedResult<ProductListItem> = {
      items: [
        {
          id: '1',
          title: 'Produto X',
          salePrice: 100,
          originalPrice: 150,
          discountPct: 33,
          status: 'Pending',
          platform: 'Amazon',
          slug: 'produto-x',
          category: 'eletronicos',
          createdAt: '2026-07-01T00:00:00Z',
          ai_score: 9,
          ai_reason: 'Bom desconto e alta demanda',
        },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    };

    let result: PagedResult<ProductListItem> | undefined;
    service.list({}).subscribe(res => (result = res));

    const req = httpMock.expectOne(r => r.url === '/api/products');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);

    expect(result?.items[0].ai_score).toBe(9);
    expect(result?.items[0].ai_reason).toBe('Bom desconto e alta demanda');
  });

  it('CA-B3 — list() envia filtros de plataforma/status/página sem parâmetros vazios', () => {
    service.list({ status: 'Pending', platform: 'Amazon', page: 2, pageSize: 10 }).subscribe();

    const req = httpMock.expectOne(
      r => r.url === '/api/products' && r.params.get('status') === 'Pending' && r.params.get('platform') === 'Amazon'
    );
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({ items: [], page: 2, pageSize: 10, totalItems: 0, totalPages: 0 });
  });

  it('list() não envia parâmetros undefined (cleanParams)', () => {
    service.list({ status: undefined, platform: undefined }).subscribe();

    const req = httpMock.expectOne('/api/products');
    expect(req.request.params.keys().length).toBe(0);
    req.flush({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  });

  it('CA-B4 — updateStatus("pending") chama PATCH /api/products/{id}/status', () => {
    let completed = false;
    service.updateStatus('abc-123', 'pending').subscribe(() => (completed = true));

    const req = httpMock.expectOne('/api/products/abc-123/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'pending' });
    req.flush(null);

    expect(completed).toBeTrue();
  });

  it('CA-B5 — updateStatus("rejected") chama PATCH /api/products/{id}/status', () => {
    service.updateStatus('abc-123', 'rejected').subscribe();

    const req = httpMock.expectOne('/api/products/abc-123/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'rejected' });
    req.flush(null);
  });

  it('CA-D1 — getById() chama GET /api/products/{id}', () => {
    const mock: ProductDetail = {
      id: '1',
      title: 'Produto X',
      salePrice: 10,
      originalPrice: 20,
      discountPct: 50,
      status: 'Published',
      platform: 'Amazon',
      slug: 'produto-x',
      category: 'cat',
      createdAt: '2026-01-01T00:00:00Z',
      description: 'Legenda completa do produto X',
      affiliateLink: 'https://x',
      imageUrl: 'https://img',
      mediaUrl: null,
      mediaLocalPath: null,
      updatedAt: '2026-01-01T00:00:00Z',
    };

    service.getById('1').subscribe((res) => {
      expect(res).toEqual(mock);
    });

    const req = httpMock.expectOne('/api/products/1');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });
});
