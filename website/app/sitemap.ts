import type { MetadataRoute } from 'next';
import { fetchDeals } from '@/lib/api';
import type { Deal } from '@/lib/types';

// Domínio hard-coded (não via env var): CA-T4 proíbe introduzir novas variáveis de produção
// nesta issue (domínio/deploy são escopo da Issue #15). Ajustar aqui se o domínio mudar.
const SITE_URL = 'https://omuletachou.com.br';

// pageSize alto para reduzir o número de chamadas à API pública ao esgotar `totalPages`;
// decisão documentada (especificacao-tecnica.md #5): paginar `fetchDeals` até esgotar,
// aceitável para o volume atual do catálogo. Se o catálogo crescer para dezenas de milhares
// de itens, revisar para geração de sitemap index (fora de escopo desta issue).
const SITEMAP_PAGE_SIZE = 100;

// `dynamic = 'force-dynamic'`: sitemap.ts não tem segmentos dinâmicos, então o Next.js
// tentaria pré-renderizá-lo estaticamente durante `next build` (dentro do multi-stage
// `Dockerfile`, sem acesso à API via rede Docker nesse estágio) — o build quebraria por
// falha de fetch. Forçando renderização em request-time, o sitemap é gerado sob demanda
// (baixo tráfego, aceitável) e o build da imagem não depende da API estar no ar.
export const dynamic = 'force-dynamic';

async function fetchAllActiveDeals(): Promise<Deal[]> {
  const allDeals: Deal[] = [];
  let page = 1;
  let totalPages = 1;

  do {
    const result = await fetchDeals(page, SITEMAP_PAGE_SIZE);
    allDeals.push(...result.items);
    totalPages = result.totalPages;
    page += 1;
  } while (page <= totalPages);

  return allDeals;
}

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const deals = await fetchAllActiveDeals();
  const categories = Array.from(new Set(deals.map((deal) => deal.category)));

  const homeEntry: MetadataRoute.Sitemap = [{ url: SITE_URL, lastModified: new Date() }];

  const categoryEntries: MetadataRoute.Sitemap = categories.map((category) => ({
    url: `${SITE_URL}/categoria/${encodeURIComponent(category)}`,
    lastModified: new Date(),
  }));

  const dealEntries: MetadataRoute.Sitemap = deals.map((deal) => ({
    url: `${SITE_URL}/oferta/${encodeURIComponent(deal.slug)}`,
    lastModified: new Date(deal.collectedAt),
  }));

  return [...homeEntry, ...categoryEntries, ...dealEntries];
}
