export function formatPriceBRL(value: number): string {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  }).format(value);
}

/** Resolve a melhor URL de imagem disponível para uma oferta, com fallback de placeholder. */
export function resolveDealImageUrl(deal: {
  mediaUrl: string | null;
  mediaLocalPath: string | null;
}): string {
  return deal.mediaUrl ?? deal.mediaLocalPath ?? '/placeholder-deal.png';
}
