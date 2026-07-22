# Especificação técnica — ISSUE-12: Site Público Next.js (ISR + SEO)

## 1. Contrato `lib/api.ts` (definitivo — Sub-A implementa, Sub-B/Sub-C consomem)

Espelha 1:1 `PublicDealDto` e `PagedResult<T>` do backend (`backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs` e `backend/src/AfiliadoBot.Api/Common/PagedResult.cs`, Issue #11, já em `main`).

```ts
// lib/types.ts
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

// lib/api.ts
const API_BASE_URL = process.env.API_INTERNAL_URL ?? 'http://api:8080';
// Nunca usar NEXT_PUBLIC_* aqui — API_INTERNAL_URL não deve ser exposta ao bundle do browser.
// Todas as chamadas abaixo só rodam em Server Components/route handlers server-side.

export async function fetchDeals(page = 1, pageSize = 12, category?: string): Promise<PagedResult<Deal>>;
export async function fetchDeal(slug: string): Promise<Deal | null>;
// null quando a API retorna 404 -> a página chama notFound() a partir do null, não a função.
export async function fetchByCategory(categoria: string, page = 1, pageSize = 12): Promise<PagedResult<Deal>>;
```

Regras de implementação:
- Todas as funções usam `fetch(url, { next: { revalidate: 300 } })` — a memoização/ISR do Next.js é por `fetch`, não por página; manter `revalidate: 300` consistente entre `lib/api.ts` e o `export const revalidate = 300` de cada `page.tsx` (dupla declaração é redundante mas inofensiva; a de `fetch` é a que efetivamente controla o cache de dados).
- `fetchDeal` mapeia HTTP 404 → retorna `null` (não lança exceção). Qualquer outro erro HTTP (5xx) ou falha de rede → deixar o erro propagar para o Next.js tratar via cache antigo (ver seção 3), nunca capturar e retornar dado fake.
- Endpoints reais: `GET /api/public/deals?page=&pageSize=`, `GET /api/public/deals/{slug}`, `GET /api/public/deals/category/{categoria}?page=&pageSize=`.
- `mediaLocalPath` do backend já é uma URL pública absoluta (`http://.../media/xxx`) quando presente — usar como fallback de imagem se `mediaUrl` for nulo; se ambos nulos, usar placeholder local (`/public/placeholder-deal.png`, a ser adicionado por Sub-A).

## 2. Estrutura de componentes

| Arquivo | Sub-issue | Responsabilidade |
|---|---|---|
| `lib/types.ts`, `lib/api.ts` | Sub-A | contrato acima |
| `components/DealCard.tsx` | Sub-A | card: imagem (ou placeholder), título, preço riscado, preço atual, badge `%OFF`, CTA `target="_blank" rel="nofollow"` |
| `components/Header.tsx` | Sub-A | navegação + filtro de plataforma (via query param `?platform=`, App Router `useSearchParams`/link, sem client fetch) |
| `app/page.tsx` (Home) | Sub-A | `export const revalidate = 300`; lê `searchParams.page`/`searchParams.platform`; chama `fetchDeals`; grade de `DealCard` + paginação (links `?page=N`) |
| `components/DealDetail.tsx` | Sub-B | mídia destaque, preço grande, badge desconto, CTA principal, seção "Mais ofertas" (4 relacionados via `fetchByCategory(deal.category)` filtrando o próprio slug) |
| `app/oferta/[slug]/page.tsx` | Sub-B | `export const revalidate = 300`; `generateMetadata` dinâmico; `fetchDeal(slug)` → `null` → `notFound()`; JSON-LD `Product` |
| `app/categoria/[categoria]/page.tsx` | Sub-C | `export const revalidate = 300`; reaproveita grade da Home; `generateMetadata` com título `"{Categoria} | O Mulet Achou"`; estado vazio (não 404) quando `items.length === 0` |
| `app/sitemap.ts` | Sub-C | `MetadataRoute.Sitemap`; lista Home + todas as categorias distintas + todas as ofertas ativas (paginar internamente até esgotar `totalPages` de `fetchDeals`) |
| `public/robots.txt` | Sub-C | estático: `User-agent: *`, `Allow: /`, `Sitemap: https://omuletachou.com.br/sitemap.xml` |

## 3. Tratamento de erro / fallback (API fora do ar)

- Comportamento padrão do Next.js ISR: se a regeneração em background falhar (erro de rede/timeout no `fetch`), o Next.js **continua servindo a última página estática gerada com sucesso** — não requer código adicional além de não capturar o erro dentro da função de página (deixar o erro subir naturalmente do `fetch`).
- Exceção: `fetchDeal` deve diferenciar "404 real" (retorna `null` → `notFound()`) de "erro de rede/5xx" (deixa propagar, sem confundir os dois — um 404 de API fora do ar não pode virar 404 de produto inexistente).
- Log mínimo: `console.error` na função de `lib/api.ts` quando a resposta não for OK e não for 404, incluindo a URL chamada (sem vazar isso ao HTML renderizado).
- Nenhum try/catch amplo que "engula" o erro e renderize página vazia sem log — isso mascara indisponibilidade real da API.

## 4. `notFound()` — slug e categoria

- `/oferta/[slug]`: `const deal = await fetchDeal(params.slug); if (!deal) notFound();`
- `/categoria/[categoria]`: **nunca** chama `notFound()` por ausência de itens — categoria inexistente/sem ofertas renderiza a página normalmente com mensagem de estado vazio (CA-C4). `notFound()` só se cabível futuramente para validar categoria contra uma lista fechada (fora de escopo agora — não implementar allowlist nesta issue).

## 5. SEO — pontos de implementação

- `generateMetadata` em `oferta/[slug]/page.tsx` (Sub-B): `title`, `description` (truncar), `openGraph: { title, description, images: [ogImage], url }`, `alternates: { canonical }`.
- `og:image` fallback: se `deal.mediaUrl`/`mediaLocalPath` nulos, usar `/og-default.png` (asset estático a ser adicionado por Sub-B).
- JSON-LD: `<script type="application/ld+json">` no corpo de `oferta/[slug]/page.tsx` com `@type: "Product"`, `offers: { "@type": "Offer", price, priceCurrency: "BRL", availability: "https://schema.org/InStock" }`.
- `generateMetadata` em `categoria/[categoria]/page.tsx` (Sub-C): `title: "${categoriaFormatada} | O Mulet Achou"`.
- `app/sitemap.ts` (Sub-C): usa `fetchDeals` paginando até `totalPages` para listar todas as ofertas; `lastModified: new Date(deal.collectedAt)`.

## 6. Variáveis de ambiente

- `API_INTERNAL_URL` (server-only, default `http://api:8080`) — já deve existir/ser adicionada ao `docker-compose.yml` do serviço `website` se ainda não estiver (verificar; se faltar, Sub-A adiciona ao compose existente, sem mexer em config de produção/Issue #15).
- Nenhuma variável `NEXT_PUBLIC_*` nova relacionada à API (evita expor URL interna ao bundle client, CA-A1).

## 7. Build

- `npm run build` deve compilar sem erros de TypeScript (CA-T1) — `tsconfig.json` já em modo estrito no scaffold da Issue #18; manter.
