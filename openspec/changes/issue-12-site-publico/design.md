# Design (resumido) — ISSUE-12: Site Público Next.js (ISR + SEO)

> PM Fase 2 concluiu sem escalar ao Arquiteto (sem ambiguidade arquitetural — ver `estado.md`). Este design.md é o resumo técnico do LT, não uma revisão de arquitetura.

## Visão geral da solução
Evoluir o scaffold Next.js 14 (App Router) da Issue #18 em `website/`, adicionando:
1. Uma camada de acesso a dados (`lib/api.ts`) que consome a API pública ASP.NET Core (Issue #11) via rede interna Docker (`http://api:8080`), nunca exposta ao browser.
2. Três páginas com ISR (`revalidate: 300`): Home (`/`), produto (`/oferta/[slug]`), categoria (`/categoria/[categoria]`).
3. Pacote de SEO completo: `generateMetadata`, Open Graph, JSON-LD `Product`/`Offer`, `sitemap.xml`, `robots.txt`.

## Componentes/telas envolvidos
- `lib/api.ts` — funções `fetchDeals`, `fetchDeal`, `fetchByCategory`; tipos `Deal`, `PagedResult<T>`.
- `components/DealCard.tsx` — card de oferta (Home e categoria).
- `components/Header.tsx` — navegação/filtros de plataforma.
- `components/DealDetail.tsx` — página de produto completa.
- `app/page.tsx` (Home), `app/categoria/[categoria]/page.tsx`, `app/oferta/[slug]/page.tsx` — já existem como stub, evoluir.
- `app/sitemap.ts` (novo), `app/robots.ts` ou `public/robots.txt` (novo).

## Stack
Next.js 14 (App Router) + TypeScript + ISR — já definida no Gate 1 (sem alternativa avaliada aqui).

## Fluxo de dados
Server Component → `fetch('http://api:8080/api/public/deals?...', { next: { revalidate: 300 } })` → `PublicDealDto[]`/`PagedResult<PublicDealDto>` do backend → mapeado para tipo `Deal` do frontend → renderizado no HTML da resposta (sem fetch client-side para o conteúdo principal). Filtros de plataforma/paginação usam navegação App Router (query params) para não exigir client-side rendering do conteúdo — ver `especificacao-tecnica.md`.

## Decisões de breakdown (LT)
- Contrato de `lib/api.ts` é simples (3 funções HTTP GET, tipos espelhando 1:1 o `PublicDealDto`/`PagedResult<T>` do backend, já estáveis desde a Issue #11 em `main`) — **definido integralmente nesta especificação técnica antes do dev começar**, eliminando a necessidade de um dev "descobrir" o contrato.
- Apesar do contrato estar fechado, a implementação real de `lib/api.ts` (Sub-A) precisa estar mergeada em `desenv` antes de Sub-B/Sub-C compilarem contra ela de verdade (evita 2 devs editando o mesmo arquivo em paralelo e reduz risco de quebra de build integrado — CA-T1). Decisão: **Sub-A primeiro, depois Sub-B e Sub-C em paralelo** (mesmo padrão usado na Issue #11 com a autenticação).
- Demanda tem UI real e o Gate 1 delegou explicitamente o layout ao UX/UI da squad ("sem Figma — UX/UI da squad define layout"), não ao critério do dev. **UX/UI entra no fluxo antes dos Devs**, produzindo um wireframe/tokens mínimo (grid de cards, página de produto, paleta/tipografia) a partir dos critérios funcionais do Gate 1 — Sub-A/B/C referenciam esse artefato.
