import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { forkJoin, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter } from 'rxjs/operators';

import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';

import { ReportsService, ReportsTotals, ProductsReportFilters, ProductsReportSummary } from '../../core/services/reports.service';
import { QueueService, QueueItem } from '../../core/services/queue.service';
import { ProductsService, ProductListItem, Platform, ProductStatus } from '../../core/services/products.service';

type BreakdownKey = 'platform' | 'category' | 'status' | 'subcategory';

interface BreakdownItem {
  label: string;
  count: number;
}

interface FilterChip {
  key: string;
  label: string;
}

const PRODUCTS_REPORT_ERROR_MESSAGE =
  'Não foi possível carregar o relatório. Verifique sua conexão e tente novamente.';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonToggleModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatChipsModule,
    MatTooltipModule,
    MatPaginatorModule,
    NgChartsModule,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent implements OnInit, OnDestroy {
  loading = true;
  errorMessage: string | null = null;

  totals: ReportsTotals | null = null;
  failedItems: QueueItem[] = [];
  retryingIds = new Set<string>();

  readonly failedColumns = ['socialNetwork', 'errorMessage', 'retryCount', 'actions'];

  barChartData: ChartData<'bar'> = { labels: [], datasets: [{ data: [], label: 'Publicacoes' }] };
  readonly barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { display: true } },
  };

  // --- Relatório de produtos publicados (Issue #228/#245) ---

  readonly platforms: Platform[] = ['MercadoLivre', 'Amazon', 'Shopee'];
  readonly statuses: ProductStatus[] = [
    'Pending',
    'Queued',
    'Published',
    'Rejected',
    'Processing',
    'Error',
    'AwaitingAffiliateLink',
  ];

  categoryOptions: string[] = [];
  subcategoryOptions: string[] = [];

  filterForm = this.fb.group({
    category: [''],
    subcategory: [''],
    platform: [''],
    status: [''],
    dateRange: this.fb.group({
      start: [null as Date | null],
      end: [null as Date | null],
    }),
  });

  productsLoading = true;
  tableLoading = false;
  productsErrorMessage: string | null = null;

  productsSummaryData: ProductsReportSummary | null = null;
  productsTableData: ProductListItem[] = [];
  productsTotalItems = 0;
  productsPageIndex = 0;
  productsPageSize = 20;

  readonly productsColumns = [
    'title',
    'category',
    'subcategory',
    'platform',
    'status',
    'price',
    'createdAt',
  ];

  expandedBreakdowns: Record<BreakdownKey, boolean> = {
    platform: false,
    category: false,
    status: false,
    subcategory: false,
  };

  readonly breakdownConfig: { key: BreakdownKey; title: string }[] = [
    { key: 'platform', title: 'Por Plataforma' },
    { key: 'category', title: 'Por Categoria' },
    { key: 'status', title: 'Por Status' },
    { key: 'subcategory', title: 'Por Subcategoria' },
  ];

  private productsRequestSub?: Subscription;
  private pageRequestSub?: Subscription;
  private filterSub?: Subscription;

  constructor(
    private fb: FormBuilder,
    private reportsService: ReportsService,
    private queueService: QueueService,
    private productsService: ProductsService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadReports();

    this.loadCategories();
    this.loadProductsReport();

    this.filterSub = this.filterForm.valueChanges
      .pipe(
        debounceTime(150),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        filter((value) => !this.isPartialDateRange(value.dateRange))
      )
      .subscribe(() => {
        this.productsPageIndex = 0;
        this.loadProductsReport();
      });
  }

  ngOnDestroy(): void {
    this.filterSub?.unsubscribe();
    this.productsRequestSub?.unsubscribe();
    this.pageRequestSub?.unsubscribe();
  }

  loadReports(): void {
    this.loading = true;
    this.errorMessage = null;

    forkJoin({
      totals: this.reportsService.totals(),
      summary: this.reportsService.summary(),
      failed: this.queueService.list({ status: 'Failed', pageSize: 10 }),
    }).subscribe({
      next: ({ totals, summary, failed }) => {
        this.totals = totals;
        this.failedItems = failed.items;
        this.barChartData = {
          labels: summary.byNetwork.map((n) => n.network),
          datasets: [{ data: summary.byNetwork.map((n) => n.count), label: 'Publicacoes (7 dias)' }],
        };
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Erro ao carregar os relatorios.';
        this.loading = false;
      },
    });
  }

  retry(item: QueueItem): void {
    this.retryingIds.add(item.id);
    this.queueService.retry(item.id).subscribe({
      next: () => {
        this.retryingIds.delete(item.id);
        this.failedItems = this.failedItems.filter((i) => i.id !== item.id);
        this.snackBar.open('Item reenviado para a fila.', 'Fechar', { duration: 3000 });
      },
      error: () => {
        this.retryingIds.delete(item.id);
        this.snackBar.open('Erro ao tentar reenviar o item.', 'Fechar', { duration: 3000 });
      },
    });
  }

  isRetrying(id: string): boolean {
    return this.retryingIds.has(id);
  }

  // --- Relatório de produtos publicados ---

  loadCategories(): void {
    this.reportsService.categories().subscribe({
      next: (tree) => {
        this.categoryOptions = tree.map((c) => c.category);
        const subcategories = new Set<string>();
        tree.forEach((c) => c.subcategories.forEach((s) => subcategories.add(s.subcategory)));
        this.subcategoryOptions = Array.from(subcategories).sort();
      },
      error: () => {
        // Não bloqueia o relatório — filtros de Categoria/Subcategoria ficam sem opções, o
        // restante (cards/tabela com os demais filtros) continua funcionando.
        this.categoryOptions = [];
        this.subcategoryOptions = [];
      },
    });
  }

  loadProductsReport(): void {
    this.productsLoading = true;
    this.productsErrorMessage = null;
    this.productsRequestSub?.unsubscribe();
    this.pageRequestSub?.unsubscribe();

    const filters = this.buildFilters();
    this.productsRequestSub = forkJoin({
      summary: this.reportsService.productsSummary(filters),
      list: this.productsService.list({ ...filters, page: 1, pageSize: this.productsPageSize }),
    }).subscribe({
      next: ({ summary, list }) => {
        this.productsSummaryData = summary;
        this.productsTableData = list.items;
        this.productsTotalItems = list.totalItems;
        this.productsPageIndex = 0;
        this.productsLoading = false;
      },
      error: () => {
        this.productsSummaryData = null;
        this.productsTableData = [];
        this.productsTotalItems = 0;
        this.productsErrorMessage = PRODUCTS_REPORT_ERROR_MESSAGE;
        this.productsLoading = false;
      },
    });
  }

  retryProductsReport(): void {
    this.loadProductsReport();
  }

  onProductsPage(event: PageEvent): void {
    this.productsPageIndex = event.pageIndex;
    this.productsPageSize = event.pageSize;
    this.tableLoading = true;

    const filters = this.buildFilters();
    this.pageRequestSub = this.productsService
      .list({ ...filters, page: this.productsPageIndex + 1, pageSize: this.productsPageSize })
      .subscribe({
        next: (result) => {
          this.productsTableData = result.items;
          this.productsTotalItems = result.totalItems;
          this.tableLoading = false;
        },
        error: () => {
          this.tableLoading = false;
          this.snackBar.open('Erro ao carregar a página do relatório.', 'Fechar', { duration: 5000 });
        },
      });
  }

  clearFilters(): void {
    this.filterForm.reset({
      category: '',
      subcategory: '',
      platform: '',
      status: '',
      dateRange: { start: null, end: null },
    });
  }

  removeChip(key: string): void {
    if (key === 'dateRange') {
      this.filterForm.get('dateRange')?.setValue({ start: null, end: null });
    } else {
      this.filterForm.get(key)?.setValue('');
    }
  }

  get hasActiveFilters(): boolean {
    const v = this.filterForm.getRawValue();
    return !!(v.category || v.subcategory || v.platform || v.status || v.dateRange?.start || v.dateRange?.end);
  }

  get activeFilterChips(): FilterChip[] {
    const v = this.filterForm.getRawValue();
    const chips: FilterChip[] = [];
    if (v.category) chips.push({ key: 'category', label: `Categoria: ${v.category}` });
    if (v.subcategory) chips.push({ key: 'subcategory', label: `Subcategoria: ${v.subcategory}` });
    if (v.platform) chips.push({ key: 'platform', label: `Plataforma: ${v.platform}` });
    if (v.status) chips.push({ key: 'status', label: `Status: ${v.status}` });
    if (v.dateRange?.start && v.dateRange?.end) {
      chips.push({
        key: 'dateRange',
        label: `Coleta: ${this.toDateParam(v.dateRange.start)} a ${this.toDateParam(v.dateRange.end)}`,
      });
    }
    return chips;
  }

  breakdownItems(key: BreakdownKey): BreakdownItem[] {
    const summary = this.productsSummaryData;
    if (!summary) return [];
    switch (key) {
      case 'platform':
        return summary.byPlatform.map((i) => ({ label: i.platform, count: i.count }));
      case 'category':
        return summary.byCategory.map((i) => ({ label: i.category, count: i.count }));
      case 'status':
        return summary.byStatus.map((i) => ({ label: i.status, count: i.count }));
      case 'subcategory':
        return summary.bySubcategory.map((i) => ({ label: i.subcategory, count: i.count }));
    }
  }

  visibleBreakdownItems(key: BreakdownKey): BreakdownItem[] {
    const sorted = [...this.breakdownItems(key)].sort((a, b) => b.count - a.count);
    if (this.expandedBreakdowns[key] || sorted.length <= 5) return sorted;
    return sorted.slice(0, 5);
  }

  hiddenBreakdownCount(key: BreakdownKey): number {
    const total = this.breakdownItems(key).length;
    return this.expandedBreakdowns[key] || total <= 5 ? 0 : total - 5;
  }

  toggleBreakdown(key: BreakdownKey): void {
    this.expandedBreakdowns[key] = !this.expandedBreakdowns[key];
  }

  breakdownBarWidth(item: BreakdownItem, key: BreakdownKey): number {
    const max = this.breakdownItems(key).reduce((m, i) => Math.max(m, i.count), 0);
    return max === 0 ? 0 : Math.round((item.count / max) * 100);
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Published':
        return 'status-success';
      case 'Pending':
        return 'status-warning';
      case 'Error':
        return 'status-danger';
      default:
        return 'status-neutral';
    }
  }

  private buildFilters(): ProductsReportFilters {
    const v = this.filterForm.getRawValue();
    return {
      category: v.category || undefined,
      subcategory: v.subcategory || undefined,
      platform: v.platform || undefined,
      status: v.status || 'Published',
      collectedFrom: this.toDateParam(v.dateRange?.start),
      collectedTo: this.toDateParam(v.dateRange?.end),
    };
  }

  private isPartialDateRange(
    range: Partial<{ start: Date | null; end: Date | null }> | null | undefined
  ): boolean {
    if (!range) return false;
    return (!!range.start && !range.end) || (!range.start && !!range.end);
  }

  private toDateParam(d: Date | null | undefined): string | undefined {
    if (!d) return undefined;
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }
}
