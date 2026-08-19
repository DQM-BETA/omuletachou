import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { cleanParams } from './paged-result.model';

export interface ReportsSummary {
  periodStart: string;
  periodEnd: string;
  totalPublished: number;
  byNetwork: { network: string; count: number }[];
  byDay: { date: string; count: number }[];
}

export interface ReportsTotals {
  today: number;
  week: number;
  month: number;
}

/**
 * Filtros do relatório de produtos publicados (Issue #228, sub-issue #245).
 * Todos opcionais — omitidos da query string via `cleanParams` quando vazios.
 */
export interface ProductsReportFilters {
  category?: string;
  subcategory?: string;
  platform?: string;
  status?: string;
  collectedFrom?: string;
  collectedTo?: string;
}

export interface ProductsReportPlatformBreakdown {
  platform: string;
  count: number;
}

export interface ProductsReportCategoryBreakdown {
  category: string;
  count: number;
}

export interface ProductsReportStatusBreakdown {
  status: string;
  count: number;
}

export interface ProductsReportSubcategoryBreakdown {
  subcategory: string;
  count: number;
}

/** Espelha `ProductsReportSummaryDto` (`GET /api/reports/products/summary`, especificacao-tecnica.md §4). */
export interface ProductsReportSummary {
  total: number;
  byPlatform: ProductsReportPlatformBreakdown[];
  byCategory: ProductsReportCategoryBreakdown[];
  byStatus: ProductsReportStatusBreakdown[];
  bySubcategory: ProductsReportSubcategoryBreakdown[];
}

export interface CategoryTreeSubcategory {
  subcategory: string;
  count: number;
}

/** Espelha `CategoryTreeDto` (`GET /api/public/categories`, Issue #167) — reaproveitado para popular
 * os filtros de Categoria/Subcategoria do relatório (ux-ui-spec.md §3.2), sem endpoint novo. */
export interface CategoryTree {
  category: string;
  subcategories: CategoryTreeSubcategory[];
  count: number;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  constructor(private http: HttpClient) {}

  summary(): Observable<ReportsSummary> {
    return this.http.get<ReportsSummary>('/api/reports/summary');
  }

  totals(): Observable<ReportsTotals> {
    return this.http.get<ReportsTotals>('/api/reports/totals');
  }

  // Falhas recentes: reaproveita QueueService.list({ status: 'Failed' }) — sem endpoint novo.

  /**
   * Cards agregados do relatório de produtos publicados (Issue #228/#245).
   * GET /api/reports/products/summary — sem paginação, `total` + 4 breakdowns.
   */
  productsSummary(filters: ProductsReportFilters): Observable<ProductsReportSummary> {
    return this.http.get<ProductsReportSummary>('/api/reports/products/summary', {
      params: cleanParams(filters),
    });
  }

  /**
   * Árvore Categoria -> [Subcategoria] (GET /api/public/categories, já existente/público, Issue
   * #167) — reaproveitada para popular os `mat-select` de Categoria/Subcategoria do relatório.
   */
  categories(): Observable<CategoryTree[]> {
    return this.http.get<CategoryTree[]>('/api/public/categories');
  }
}
