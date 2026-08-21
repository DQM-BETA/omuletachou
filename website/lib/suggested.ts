'use client';

import type { Deal } from './types';

// Client-side: nunca usar API_INTERNAL_URL aqui (server-only) — mesmo padrão de
// lib/tracking.ts/lib/push.ts (ver comentário no topo de lib/api.ts).
const API_URL = process.env.NEXT_PUBLIC_API_URL;

/**
 * Busca a faixa de produtos sugeridos (Issue #231, sub-issue #280 — T-05).
 *
 * A lógica de fallback (categoria com resultado vs. "mais clicados" geral) e o corte mínimo de
 * 4 itens são decididos inteiramente pelo backend (design.md §6, especificacao-tecnica.md §3.3)
 * — esta função só repassa o estado atual de filtro da página de listagem:
 * - `category`: categoria ativa no filtro (undefined/vazio quando nenhum filtro de categoria
 *   está aplicado).
 * - `hasResults`: se a listagem principal, com os filtros atuais, retornou pelo menos 1 produto.
 *
 * Lança em caso de resposta não-OK — o chamador (`SuggestedProductsCarousel`) trata a falha em
 * um `try/catch` isolado (CA 1.8), sem propagar erro para o resto da página.
 */
export async function fetchSuggestedProducts(
  category: string | undefined,
  hasResults: boolean
): Promise<Deal[]> {
  const params = new URLSearchParams({ hasResults: String(hasResults) });
  if (category) {
    params.set('categories', category);
  }
  const url = `${API_URL}/api/public/products/suggested?${params.toString()}`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  return response.json();
}
