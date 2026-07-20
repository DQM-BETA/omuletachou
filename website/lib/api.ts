import type { Deal, PagedResult } from './types';

// Server-only: nunca usar NEXT_PUBLIC_* aqui — API_INTERNAL_URL não deve ser exposta
// ao bundle do browser. Todas as funções abaixo só devem ser chamadas a partir de
// Server Components / route handlers.
const API_BASE_URL = process.env.API_INTERNAL_URL ?? 'http://api:8080';

const REVALIDATE_SECONDS = 300;

async function handleResponse<T>(response: Response, url: string): Promise<T> {
  if (!response.ok) {
    // eslint-disable-next-line no-console
    console.error(
      `[lib/api] Resposta não-OK da API pública: ${response.status} ${response.statusText} — ${url}`
    );
    throw new Error(`Falha ao buscar ${url}: HTTP ${response.status}`);
  }
  return response.json() as Promise<T>;
}

export async function fetchDeals(
  page = 1,
  pageSize = 12,
  category?: string
): Promise<PagedResult<Deal>> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (category) {
    params.set('category', category);
  }
  const url = `${API_BASE_URL}/api/public/deals?${params.toString()}`;
  const response = await fetch(url, { next: { revalidate: REVALIDATE_SECONDS } });
  return handleResponse<PagedResult<Deal>>(response, url);
}

export async function fetchDeal(slug: string): Promise<Deal | null> {
  const url = `${API_BASE_URL}/api/public/deals/${encodeURIComponent(slug)}`;
  const response = await fetch(url, { next: { revalidate: REVALIDATE_SECONDS } });

  if (response.status === 404) {
    return null;
  }

  return handleResponse<Deal>(response, url);
}

export async function fetchByCategory(
  categoria: string,
  page = 1,
  pageSize = 12
): Promise<PagedResult<Deal>> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  const url = `${API_BASE_URL}/api/public/deals/category/${encodeURIComponent(
    categoria
  )}?${params.toString()}`;
  const response = await fetch(url, { next: { revalidate: REVALIDATE_SECONDS } });
  return handleResponse<PagedResult<Deal>>(response, url);
}
