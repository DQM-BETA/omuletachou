import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult, cleanParams } from './paged-result.model';

export type ProductStatus =
  | 'Pending'
  | 'Queued'
  | 'Published'
  | 'Rejected'
  | 'Processing'
  | 'Error'
  | 'AwaitingAffiliateLink';
export type Platform = 'Amazon' | 'MercadoLivre' | 'Shopee';

export interface ProductListItem {
  id: string;
  title: string;
  salePrice: number;
  originalPrice: number;
  discountPct: number;
  status: ProductStatus;
  platform: Platform;
  slug: string;
  category: string;
  createdAt: string;
  ai_score?: number | null;
  ai_reason?: string | null;
  sourceUrl?: string | null;
}

export interface ProductDetail extends ProductListItem {
  description: string;
  affiliateLink: string | null;
  imageUrl: string | null;
  mediaUrl: string | null;
  mediaLocalPath: string | null;
  updatedAt: string;
  ai_caption?: string | null;
}

export interface ProductsListParams {
  status?: string;
  platform?: string;
  page?: number;
  pageSize?: number;
}

/**
 * Um item do lote de POST /api/products/affiliate-links/import (Issue #182/#185) — pareamento
 * EXPLICITO por productId (montado pelo dashboard, que ja tem o id de cada linha exibida).
 */
export interface AffiliateLinkImportItem {
  productId: string;
  affiliateLink: string;
}

/** Item pulado na importacao em lote, com o motivo (Issue #182/#185). */
export interface AffiliateLinkImportSkip {
  productId: string;
  reason: string;
}

/** Resultado de POST /api/products/affiliate-links/import (Issue #182/#185). */
export interface ImportAffiliateLinksResult {
  imported: number;
  skipped: AffiliateLinkImportSkip[];
}

@Injectable({ providedIn: 'root' })
export class ProductsService {
  constructor(private http: HttpClient) {}

  list(params: ProductsListParams): Observable<PagedResult<ProductListItem>> {
    return this.http.get<PagedResult<ProductListItem>>('/api/products', { params: cleanParams(params) });
  }

  updateStatus(id: string, status: 'pending' | 'rejected'): Observable<void> {
    return this.http.patch<void>(`/api/products/${id}/status`, { status });
  }

  /**
   * Detalhe de um produto (usado pela tela Facebook Manual para exibir preview de
   * midia + legenda completa de cada card de post pendente — CA-D1). GET /api/products/{id}
   * ja existe na API desde a Issue #11, sem alteracao de contrato.
   */
  getById(id: string): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`/api/products/${id}`);
  }

  /**
   * Produtos ML aguardando importacao manual do link de afiliado (Issue #182/#185) — tela
   * "Links de Afiliado — Mercado Livre". Sem paginacao adicional: volume operacional baixo,
   * pageSize=200 cobre o pior caso (ver especificacao-tecnica.md §3.6).
   */
  listAwaitingAffiliateLink(): Observable<PagedResult<ProductListItem>> {
    return this.list({ status: 'AwaitingAffiliateLink', pageSize: 200 });
  }

  /**
   * Importa em lote os links de afiliado colados pelo operador (Issue #182/#185). Pareamento
   * produto/link ja resolvido pelo dashboard (por productId) antes de chamar este metodo.
   */
  importAffiliateLinks(items: AffiliateLinkImportItem[]): Observable<ImportAffiliateLinksResult> {
    return this.http.post<ImportAffiliateLinksResult>('/api/products/affiliate-links/import', { items });
  }
}
