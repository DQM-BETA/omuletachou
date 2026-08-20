import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import {
  ReportsService,
  ReportsSummary,
  ReportsTotals,
  ProductsReportSummary,
  CategoryTree,
} from './reports.service';

describe('ReportsService', () => {
  let service: ReportsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(ReportsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('summary() chama GET /api/reports/summary', () => {
    const mock: ReportsSummary = {
      periodStart: '2026-01-01',
      periodEnd: '2026-01-07',
      totalPublished: 5,
      byNetwork: [{ network: 'Facebook', count: 5 }],
      byDay: [{ date: '2026-01-01', count: 5 }],
    };

    service.summary().subscribe((res) => {
      expect(res).toEqual(mock);
    });

    const req = httpMock.expectOne('/api/reports/summary');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });

  it('totals() chama GET /api/reports/totals', () => {
    const mock: ReportsTotals = { today: 3, week: 12, month: 47 };

    service.totals().subscribe((res) => {
      expect(res).toEqual(mock);
    });

    const req = httpMock.expectOne('/api/reports/totals');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });

  it('Issue #228/#245 — productsSummary() chama GET /api/reports/products/summary sem filtros vazios', () => {
    const mock: ProductsReportSummary = {
      total: 10,
      byPlatform: [{ platform: 'MercadoLivre', count: 6 }],
      byCategory: [{ category: 'Eletrônicos', count: 4 }],
      byStatus: [{ status: 'Published', count: 10 }],
      bySubcategory: [{ subcategory: 'Celulares', count: 3 }],
    };

    service.productsSummary({ status: 'Published' }).subscribe((res) => {
      expect(res).toEqual(mock);
    });

    const req = httpMock.expectOne(
      (r) => r.url === '/api/reports/products/summary' && r.params.get('status') === 'Published'
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys().length).toBe(1);
    req.flush(mock);
  });

  it('Issue #228/#245 — productsSummary() envia todos os filtros combinados (interseção AND)', () => {
    service
      .productsSummary({
        category: 'Eletrônicos',
        subcategory: 'Celulares',
        platform: 'MercadoLivre',
        status: 'Published',
        collectedFrom: '2026-01-01',
        collectedTo: '2026-01-31',
      })
      .subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/reports/products/summary' &&
        r.params.get('category') === 'Eletrônicos' &&
        r.params.get('subcategory') === 'Celulares' &&
        r.params.get('platform') === 'MercadoLivre' &&
        r.params.get('status') === 'Published' &&
        r.params.get('collectedFrom') === '2026-01-01' &&
        r.params.get('collectedTo') === '2026-01-31'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ total: 0, byPlatform: [], byCategory: [], byStatus: [], bySubcategory: [] });
  });

  it('Issue #228/#245 — categories() chama GET /api/public/categories', () => {
    const mock: CategoryTree[] = [
      { category: 'Eletrônicos', subcategories: [{ subcategory: 'Celulares', count: 3 }], count: 3 },
    ];

    service.categories().subscribe((res) => {
      expect(res).toEqual(mock);
    });

    const req = httpMock.expectOne('/api/public/categories');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });
});
