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
  // `platform` reintroduzido pela Issue #229 (sub-issue #253, reversão parcial e intencional da
  // remoção feita na #167) — apenas como dado de exibição (tag de texto não interativa no
  // DealCard), não como mecanismo de filtro/navegação. Valor bruto do enum `Platform` do backend
  // (`"Amazon" | "MercadoLivre" | "Shopee"`), sem tradução (fica a cargo do frontend). Pode ser
  // `null`/`undefined`/ausente ou um valor não mapeado — nesses casos a tag não é renderizada.
  platform?: string | null;
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
