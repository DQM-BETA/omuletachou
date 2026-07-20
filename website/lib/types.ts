export interface Deal {
  title: string;
  salePrice: number;
  originalPrice: number;
  discountPct: number;
  affiliateLink: string | null;
  mediaUrl: string | null;
  mediaLocalPath: string | null; // URL pública já resolvida pelo backend (não é path de disco)
  slug: string;
  category: string;
  collectedAt: string; // ISO 8601 (JSON de DateTime)
  platform: 'Amazon' | 'MercadoLivre' | 'Shopee' | string; // string do enum Platform do backend
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}
