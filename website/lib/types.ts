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
  subcategory?: string | null; // Issue #167 — nova, opcional (nem todo produto tem subcategoria)
  collectedAt: string; // ISO 8601 (JSON de DateTime)
  // `platform` removido do contrato público em PublicDealDto (Issue #167, CA 5.1) — não existe
  // mais neste tipo. A distinção de plataforma segue disponível apenas no dashboard interno.
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

/** Contagem de produtos ativos de uma subcategoria (GET /api/public/categories, CA 6.7). */
export interface SubcategoryCount {
  subcategory: string;
  count: number;
}

/** Árvore Category -> [Subcategory] retornada por GET /api/public/categories (Issue #167). */
export interface CategoryTree {
  category: string;
  count: number;
  subcategories: SubcategoryCount[];
}
