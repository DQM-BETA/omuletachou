import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult, cleanParams } from './paged-result.model';

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

export interface ProductsListParams {
  status?: string;
  platform?: string;
  page?: number;
  pageSize?: number;
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
}
