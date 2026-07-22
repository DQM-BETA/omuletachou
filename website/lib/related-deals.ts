import { fetchByCategory } from './api';
import type { Deal } from './types';

export const RELATED_DEALS_COUNT = 4;

/**
 * Busca as ofertas relacionadas de uma oferta (mesma categoria, excluindo o próprio slug).
 * Busca RELATED_DEALS_COUNT + 1 para compensar a exclusão do produto atual quando ele
 * estiver entre os primeiros itens retornados pela API.
 */
export async function getRelatedDeals(deal: Deal): Promise<Deal[]> {
  const result = await fetchByCategory(deal.category, 1, RELATED_DEALS_COUNT + 1);
  return result.items.filter((item) => item.slug !== deal.slug).slice(0, RELATED_DEALS_COUNT);
}
