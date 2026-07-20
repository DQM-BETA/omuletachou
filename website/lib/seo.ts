import type { Deal } from './types';
import { formatPriceBRL } from './format';

const SITE_NAME = 'O Mulet Achou';
const SITE_URL = 'https://omuletachou.com.br';
const DEFAULT_OG_IMAGE = '/og-default.png';
const DESCRIPTION_MAX_LENGTH = 160;

export function buildDealTitle(deal: Deal): string {
  return `${deal.title} | ${SITE_NAME}`;
}

export function buildDealDescription(deal: Deal): string {
  const raw = `${deal.title} por ${formatPriceBRL(deal.salePrice)} na categoria ${deal.category}. Confira essa oferta no ${SITE_NAME}!`;
  if (raw.length <= DESCRIPTION_MAX_LENGTH) {
    return raw;
  }
  return `${raw.slice(0, DESCRIPTION_MAX_LENGTH - 1).trimEnd()}…`;
}

export function buildDealCanonicalUrl(deal: Deal): string {
  return `${SITE_URL}/oferta/${deal.slug}`;
}

/** Resolve a imagem de Open Graph do produto, com fallback para o asset padrão do site (CA-B8). */
export function resolveDealOgImage(deal: Deal): string {
  if (deal.mediaUrl) {
    return deal.mediaUrl;
  }
  if (deal.mediaLocalPath) {
    return deal.mediaLocalPath;
  }
  return DEFAULT_OG_IMAGE;
}

export function buildDealJsonLd(deal: Deal) {
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: deal.title,
    category: deal.category,
    image: [resolveDealOgImage(deal)],
    offers: {
      '@type': 'Offer',
      price: deal.salePrice,
      priceCurrency: 'BRL',
      availability: 'https://schema.org/InStock',
      url: buildDealCanonicalUrl(deal),
    },
  };
}
