import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type ProductStatus = 'Pending' | 'Queued' | 'Published' | 'Rejected' | 'Processing' | 'Error';
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
}

export interface ProductDetail extends ProductListItem {
  description: string;
  affiliateLink: string | null;
  imageUrl: string | null;
  mediaUrl: string | null;
  mediaLocalPath: string | null;
  updatedAt: string;
}

@Injectable({ providedIn: 'root' })
export class ProductsService {
  constructor(private http: HttpClient) {}

  /**
   * Detalhe de um produto (usado pela tela Facebook Manual para exibir preview de
   * midia + legenda completa de cada card de post pendente — CA-D1). GET /api/products/{id}
   * ja existe na API desde a Issue #11, sem alteracao de contrato.
   */
  getById(id: string): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`/api/products/${id}`);
  }
}
