import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

import { ReportsComponent } from './reports.component';
import { ReportsService, ReportsSummary, ReportsTotals } from '../../core/services/reports.service';
import { QueueService, QueueItem } from '../../core/services/queue.service';
import { PagedResult } from '../../core/services/paged-result.model';

describe('ReportsComponent', () => {
  let component: ReportsComponent;
  let fixture: ComponentFixture<ReportsComponent>;
  let reportsServiceSpy: jasmine.SpyObj<ReportsService>;
  let queueServiceSpy: jasmine.SpyObj<QueueService>;

  const totals: ReportsTotals = { today: 3, week: 12, month: 47 };

  const summary: ReportsSummary = {
    periodStart: '2026-01-01',
    periodEnd: '2026-01-07',
    totalPublished: 5,
    byNetwork: [
      { network: 'Facebook', count: 3 },
      { network: 'Telegram', count: 2 },
    ],
    byDay: [{ date: '2026-01-01', count: 5 }],
  };

  const failedItem: QueueItem = {
    id: 'f1',
    productId: 'p1',
    socialNetwork: 'Telegram',
    status: 'Failed',
    scheduledAt: '2026-01-01T00:00:00Z',
    publishedAt: null,
    retryCount: 1,
    errorMessage: 'timeout',
    createdAt: '2026-01-01T00:00:00Z',
  };

  const failedResult: PagedResult<QueueItem> = {
    items: [failedItem],
    page: 1,
    pageSize: 10,
    totalItems: 1,
    totalPages: 1,
  };

  function setup(): void {
    reportsServiceSpy = jasmine.createSpyObj('ReportsService', ['totals', 'summary']);
    queueServiceSpy = jasmine.createSpyObj('QueueService', ['list', 'retry']);

    reportsServiceSpy.totals.and.returnValue(of(totals));
    reportsServiceSpy.summary.and.returnValue(of(summary));
    queueServiceSpy.list.and.returnValue(of(failedResult));

    TestBed.configureTestingModule({
      imports: [ReportsComponent, NoopAnimationsModule],
      providers: [
        { provide: ReportsService, useValue: reportsServiceSpy },
        { provide: QueueService, useValue: queueServiceSpy },
      ],
    });

    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
  }

  it('should create', () => {
    setup();
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('CA-D4: exibe cards com totais hoje/semana/mes', () => {
    setup();
    fixture.detectChanges();

    expect(component.totals).toEqual(totals);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="total-today"]')?.textContent).toContain('3');
    expect(compiled.querySelector('[data-testid="total-week"]')?.textContent).toContain('12');
    expect(compiled.querySelector('[data-testid="total-month"]')?.textContent).toContain('47');
  });

  it('CA-D5: monta o grafico de barras com dados de publicacoes por rede', () => {
    setup();
    fixture.detectChanges();

    expect(component.barChartData.labels).toEqual(['Facebook', 'Telegram']);
    expect(component.barChartData.datasets[0].data).toEqual([3, 2]);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="network-chart"]')).toBeTruthy();
  });

  it('CA-D6: exibe tabela de falhas recentes com botao Retry', () => {
    setup();
    fixture.detectChanges();

    expect(queueServiceSpy.list).toHaveBeenCalledWith({ status: 'Failed', pageSize: 10 });
    expect(component.failedItems).toEqual([failedItem]);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="failures-table"]')).toBeTruthy();
    expect(compiled.querySelector('[data-testid="retry-button"]')).toBeTruthy();
  });

  it('CA-D6: aciona o retry e remove o item da tabela em caso de sucesso', () => {
    setup();
    queueServiceSpy.retry.and.returnValue(of(void 0));
    fixture.detectChanges();

    component.retry(failedItem);

    expect(queueServiceSpy.retry).toHaveBeenCalledWith('f1');
    expect(component.failedItems.length).toBe(0);
  });

  it('exibe mensagem de erro quando o carregamento falha', () => {
    reportsServiceSpy = jasmine.createSpyObj('ReportsService', ['totals', 'summary']);
    queueServiceSpy = jasmine.createSpyObj('QueueService', ['list', 'retry']);
    reportsServiceSpy.totals.and.returnValue(throwError(() => new Error('fail')));
    reportsServiceSpy.summary.and.returnValue(of(summary));
    queueServiceSpy.list.and.returnValue(of(failedResult));

    TestBed.configureTestingModule({
      imports: [ReportsComponent, NoopAnimationsModule],
      providers: [
        { provide: ReportsService, useValue: reportsServiceSpy },
        { provide: QueueService, useValue: queueServiceSpy },
      ],
    });

    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.errorMessage).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="error-message"]')).toBeTruthy();
  });

  it('exibe mensagem quando nao ha falhas recentes', () => {
    reportsServiceSpy = jasmine.createSpyObj('ReportsService', ['totals', 'summary']);
    queueServiceSpy = jasmine.createSpyObj('QueueService', ['list', 'retry']);
    reportsServiceSpy.totals.and.returnValue(of(totals));
    reportsServiceSpy.summary.and.returnValue(of(summary));
    queueServiceSpy.list.and.returnValue(
      of({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 })
    );

    TestBed.configureTestingModule({
      imports: [ReportsComponent, NoopAnimationsModule],
      providers: [
        { provide: ReportsService, useValue: reportsServiceSpy },
        { provide: QueueService, useValue: queueServiceSpy },
      ],
    });

    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="no-failures"]')).toBeTruthy();
  });
});
