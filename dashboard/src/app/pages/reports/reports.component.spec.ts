import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

import { ReportsComponent } from './reports.component';
import {
  ReportsService,
  ReportsSummary,
  ReportsTotals,
  ProductsReportSummary,
  CategoryTree,
} from '../../core/services/reports.service';
import { QueueService, QueueItem } from '../../core/services/queue.service';
import { ProductsService, ProductListItem } from '../../core/services/products.service';
import { PagedResult } from '../../core/services/paged-result.model';

describe('ReportsComponent', () => {
  let component: ReportsComponent;
  let fixture: ComponentFixture<ReportsComponent>;
  let reportsServiceSpy: jasmine.SpyObj<ReportsService>;
  let queueServiceSpy: jasmine.SpyObj<QueueService>;
  let productsServiceSpy: jasmine.SpyObj<ProductsService>;

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

  const categoryTree: CategoryTree[] = [
    {
      category: 'Eletrônicos',
      subcategories: [
        { subcategory: 'Celulares', count: 5 },
        { subcategory: 'Fones', count: 2 },
      ],
      count: 7,
    },
  ];

  const productsSummary: ProductsReportSummary = {
    total: 10,
    byPlatform: [
      { platform: 'MercadoLivre', count: 6 },
      { platform: 'Amazon', count: 4 },
    ],
    byCategory: [{ category: 'Eletrônicos', count: 10 }],
    byStatus: [{ status: 'Published', count: 10 }],
    bySubcategory: [{ subcategory: 'Celulares', count: 7 }],
  };

  const productItem: ProductListItem = {
    id: 'p1',
    title: 'Produto X',
    salePrice: 100,
    originalPrice: 150,
    discountPct: 33,
    status: 'Published',
    platform: 'MercadoLivre',
    slug: 'produto-x',
    category: 'Eletrônicos',
    subcategory: 'Celulares',
    createdAt: '2026-07-01T00:00:00Z',
  };

  const productsListResult: PagedResult<ProductListItem> = {
    items: [productItem],
    page: 1,
    pageSize: 20,
    totalItems: 1,
    totalPages: 1,
  };

  const emptyProductsSummary: ProductsReportSummary = {
    total: 0,
    byPlatform: [],
    byCategory: [],
    byStatus: [],
    bySubcategory: [],
  };

  const emptyProductsListResult: PagedResult<ProductListItem> = {
    items: [],
    page: 1,
    pageSize: 20,
    totalItems: 0,
    totalPages: 0,
  };

  function setup(options?: {
    productsSummaryResult?: ProductsReportSummary;
    productsListResult?: PagedResult<ProductListItem>;
    productsSummaryError?: boolean;
    productsListError?: boolean;
    categoriesError?: boolean;
  }): void {
    reportsServiceSpy = jasmine.createSpyObj('ReportsService', [
      'totals',
      'summary',
      'productsSummary',
      'categories',
    ]);
    queueServiceSpy = jasmine.createSpyObj('QueueService', ['list', 'retry']);
    productsServiceSpy = jasmine.createSpyObj('ProductsService', ['list']);

    reportsServiceSpy.totals.and.returnValue(of(totals));
    reportsServiceSpy.summary.and.returnValue(of(summary));
    queueServiceSpy.list.and.returnValue(of(failedResult));

    reportsServiceSpy.categories.and.returnValue(
      options?.categoriesError ? throwError(() => new Error('fail')) : of(categoryTree)
    );

    reportsServiceSpy.productsSummary.and.returnValue(
      options?.productsSummaryError
        ? throwError(() => new Error('fail'))
        : of(options?.productsSummaryResult ?? productsSummary)
    );
    productsServiceSpy.list.and.returnValue(
      options?.productsListError
        ? throwError(() => new Error('fail'))
        : of(options?.productsListResult ?? productsListResult)
    );

    TestBed.configureTestingModule({
      imports: [ReportsComponent, NoopAnimationsModule],
      providers: [
        { provide: ReportsService, useValue: reportsServiceSpy },
        { provide: QueueService, useValue: queueServiceSpy },
        { provide: ProductsService, useValue: productsServiceSpy },
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
    setup();
    reportsServiceSpy.totals.and.returnValue(throwError(() => new Error('fail')));
    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.errorMessage).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="error-message"]')).toBeTruthy();
  });

  it('exibe mensagem quando nao ha falhas recentes', () => {
    setup();
    queueServiceSpy.list.and.returnValue(
      of({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 })
    );
    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="no-failures"]')).toBeTruthy();
  });

  // --- Relatório de produtos publicados (Issue #228/#245) ---

  describe('Relatório de produtos publicados', () => {
    it('CA 1.1/1.2: ao carregar sem filtro, busca cards+tabela com status=Published (default) e mantém os cards/gráfico existentes', () => {
      setup();
      fixture.detectChanges();

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ status: 'Published' })
      );
      expect(productsServiceSpy.list).toHaveBeenCalledWith(
        jasmine.objectContaining({ status: 'Published', page: 1 })
      );
      expect(component.productsSummaryData).toEqual(productsSummary);
      expect(component.productsTableData).toEqual([productItem]);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[data-testid="products-total-card"]')?.textContent).toContain('10');
      expect(compiled.querySelector('[data-testid="products-table"]')).toBeTruthy();
      // Cards/gráfico existentes continuam exibidos (CA 1.2)
      expect(compiled.querySelector('[data-testid="totals-cards"]')).toBeTruthy();
      expect(compiled.querySelector('[data-testid="network-chart"]')).toBeTruthy();
    });

    it('CA 1.3: nenhum produto Published — cards mostram zero e tabela mostra estado vazio, sem erro', () => {
      setup({ productsSummaryResult: emptyProductsSummary, productsListResult: emptyProductsListResult });
      fixture.detectChanges();

      expect(component.productsErrorMessage).toBeNull();
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[data-testid="products-total-card"]')?.textContent).toContain('0');
      expect(compiled.querySelector('[data-testid="products-table-empty"]')).toBeTruthy();
    });

    it('CA 2.1: filtrar por Categoria recalcula cards+tabela automaticamente', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      reportsServiceSpy.productsSummary.calls.reset();
      productsServiceSpy.list.calls.reset();

      component.filterForm.get('category')?.setValue('Eletrônicos');
      tick(150);
      fixture.detectChanges();
      tick(20);

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ category: 'Eletrônicos' })
      );
      expect(productsServiceSpy.list).toHaveBeenCalledWith(
        jasmine.objectContaining({ category: 'Eletrônicos', page: 1 })
      );
    }));

    it('CA 2.3: filtrar por Plataforma recalcula cards+tabela', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      reportsServiceSpy.productsSummary.calls.reset();

      component.filterForm.get('platform')?.setValue('Amazon');
      tick(150);

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ platform: 'Amazon' })
      );
    }));

    it('CA 2.4: filtrar por Status diferente de Published envia o valor escolhido (não o default)', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      reportsServiceSpy.productsSummary.calls.reset();

      component.filterForm.get('status')?.setValue('Pending');
      tick(150);

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ status: 'Pending' })
      );
    }));

    it('CA 2.5: faixa de data só dispara quando início e fim estão preenchidos', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      reportsServiceSpy.productsSummary.calls.reset();

      component.filterForm.get('dateRange')?.get('start')?.setValue(new Date(2026, 0, 1));
      tick(150);
      expect(reportsServiceSpy.productsSummary).not.toHaveBeenCalled();

      component.filterForm.get('dateRange')?.get('end')?.setValue(new Date(2026, 0, 31));
      tick(150);

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ collectedFrom: '2026-01-01', collectedTo: '2026-01-31' })
      );
    }));

    it('CA 2.6: múltiplos filtros aplicados juntos são enviados como interseção (AND)', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      reportsServiceSpy.productsSummary.calls.reset();

      component.filterForm.patchValue({ category: 'Eletrônicos', platform: 'MercadoLivre', status: 'Published' });
      tick(150);

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({
          category: 'Eletrônicos',
          platform: 'MercadoLivre',
          status: 'Published',
        })
      );
    }));

    it('CA 2.7: combinação sem resultados — cards zeram e tabela fica vazia, sem dado remanescente', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      expect(component.productsSummaryData?.total).toBe(10);

      reportsServiceSpy.productsSummary.and.returnValue(of(emptyProductsSummary));
      productsServiceSpy.list.and.returnValue(of(emptyProductsListResult));

      component.filterForm.get('category')?.setValue('Categoria Inexistente');
      tick(150);
      fixture.detectChanges();
      tick(20);

      expect(component.productsSummaryData).toEqual(emptyProductsSummary);
      expect(component.productsTableData).toEqual([]);
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[data-testid="products-table-empty"]')).toBeTruthy();
    }));

    it('CA 2.8: "Limpar filtros" volta ao universo completo Published', fakeAsync(() => {
      setup();
      fixture.detectChanges();

      component.filterForm.patchValue({ category: 'Eletrônicos', platform: 'MercadoLivre' });
      tick(150);
      reportsServiceSpy.productsSummary.calls.reset();
      productsServiceSpy.list.calls.reset();

      component.clearFilters();
      tick(150);

      expect(component.filterForm.get('category')?.value).toBe('');
      expect(component.filterForm.get('platform')?.value).toBe('');
      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ status: 'Published', category: undefined, platform: undefined })
      );
    }));

    it('CA 2.9: trocar o valor de um filtro já aplicado recalcula sem precisar recarregar a página', fakeAsync(() => {
      setup();
      fixture.detectChanges();

      component.filterForm.get('platform')?.setValue('MercadoLivre');
      tick(150);
      reportsServiceSpy.productsSummary.calls.reset();

      component.filterForm.get('platform')?.setValue('Amazon');
      tick(150);

      expect(reportsServiceSpy.productsSummary).toHaveBeenCalledWith(
        jasmine.objectContaining({ platform: 'Amazon' })
      );
    }));

    it('CA 4.1: não existe opção de exportar/imprimir o relatório', () => {
      setup();
      fixture.detectChanges();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[data-testid="export-button"]')).toBeFalsy();
      expect(compiled.querySelector('[data-testid="print-button"]')).toBeFalsy();
      expect(compiled.textContent?.toLowerCase()).not.toContain('exportar');
      expect(compiled.textContent?.toLowerCase()).not.toContain('imprimir');
    });

    it('CA 5.1: erro de rede ao aplicar filtro exibe mensagem clara, sem manter dado antigo, com opção de tentar novamente', fakeAsync(() => {
      setup();
      fixture.detectChanges();
      expect(component.productsSummaryData?.total).toBe(10);

      reportsServiceSpy.productsSummary.and.returnValue(throwError(() => new Error('network fail')));

      component.filterForm.get('category')?.setValue('Eletrônicos');
      tick(150);
      fixture.detectChanges();
      tick(20);

      expect(component.productsErrorMessage).toBeTruthy();
      expect(component.productsSummaryData).toBeNull();
      expect(component.productsTableData).toEqual([]);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[data-testid="products-error"]')).toBeTruthy();
      const retryButton = compiled.querySelector('[data-testid="products-retry-button"]') as HTMLButtonElement;
      expect(retryButton).toBeTruthy();

      // Permite nova tentativa
      reportsServiceSpy.productsSummary.and.returnValue(of(productsSummary));
      component.retryProductsReport();
      fixture.detectChanges();
      tick(20);

      expect(component.productsErrorMessage).toBeNull();
      expect(component.productsSummaryData).toEqual(productsSummary);
    }));

    it('erro na chamada de lista (mesmo com summary ok) também dispara o estado de erro compartilhado', fakeAsync(() => {
      setup();
      productsServiceSpy.list.and.returnValue(throwError(() => new Error('fail')));
      fixture = TestBed.createComponent(ReportsComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();

      expect(component.productsErrorMessage).toBeTruthy();
      expect(component.productsSummaryData).toBeNull();
    }));

    it('troca de página da tabela chama apenas list() (não recalcula os cards)', () => {
      setup();
      fixture.detectChanges();
      reportsServiceSpy.productsSummary.calls.reset();
      productsServiceSpy.list.calls.reset();
      productsServiceSpy.list.and.returnValue(
        of({ ...productsListResult, page: 2 })
      );

      component.onProductsPage({ pageIndex: 1, pageSize: 20, length: 1 });

      expect(reportsServiceSpy.productsSummary).not.toHaveBeenCalled();
      expect(productsServiceSpy.list).toHaveBeenCalledWith(jasmine.objectContaining({ page: 2 }));
    });

    it('popula as opções de Categoria/Subcategoria a partir de GET /api/public/categories (reaproveitado)', () => {
      setup();
      fixture.detectChanges();

      expect(reportsServiceSpy.categories).toHaveBeenCalled();
      expect(component.categoryOptions).toEqual(['Eletrônicos']);
      expect(component.subcategoryOptions).toEqual(['Celulares', 'Fones']);
    });

    it('falha ao carregar categorias não bloqueia o restante do relatório', () => {
      setup({ categoriesError: true });
      fixture.detectChanges();

      expect(component.categoryOptions).toEqual([]);
      expect(component.subcategoryOptions).toEqual([]);
      expect(component.productsErrorMessage).toBeNull();
      expect(component.productsSummaryData).toEqual(productsSummary);
    });

    it('"Limpar filtros" fica desabilitado quando não há filtro ativo', () => {
      setup();
      fixture.detectChanges();

      const compiled = fixture.nativeElement as HTMLElement;
      const clearButton = compiled.querySelector('[data-testid="clear-filters-button"]') as HTMLButtonElement;
      expect(clearButton.disabled).toBeTrue();
    });

    it('"Limpar filtros" fica habilitado quando há filtro ativo', () => {
      setup();
      fixture.detectChanges();

      component.filterForm.get('category')?.setValue('Eletrônicos');
      fixture.detectChanges();

      const compiled = fixture.nativeElement as HTMLElement;
      const clearButton = compiled.querySelector('[data-testid="clear-filters-button"]') as HTMLButtonElement;
      expect(clearButton.disabled).toBeFalse();
    });

    it('exibe chips de filtros ativos, removíveis individualmente', fakeAsync(() => {
      setup();
      fixture.detectChanges();

      component.filterForm.get('category')?.setValue('Eletrônicos');
      tick(150);
      fixture.detectChanges();

      expect(component.activeFilterChips).toEqual([{ key: 'category', label: 'Categoria: Eletrônicos' }]);
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[data-testid="filter-chip-category"]')).toBeTruthy();

      component.removeChip('category');
      tick(150);
      fixture.detectChanges();
      tick(20);

      expect(component.filterForm.get('category')?.value).toBe('');
      expect(component.activeFilterChips).toEqual([]);
    }));
  });
});
