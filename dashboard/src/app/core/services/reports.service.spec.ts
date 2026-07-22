import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import { ReportsService, ReportsSummary, ReportsTotals } from './reports.service';

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
});
