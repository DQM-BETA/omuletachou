# Tasks — ISSUE-12: Site Público Next.js (ISR + SEO)

Contrato completo em `documentacoes/ISSUE-12-site-publico/especificacao-tecnica.md`. Critérios de aceite completos (Given/When/Then) em `documentacoes/ISSUE-12-site-publico/criterios-aceite.md`.

**Ordem de execução:** Sub-A primeiro (contrato `lib/api.ts` real precisa estar em `desenv` antes de Sub-B/Sub-C compilarem contra ele). Sub-B e Sub-C podem rodar em paralelo entre si depois que Sub-A mergear. UX/UI entrega o wireframe/tokens antes de qualquer dev iniciar (layout não é decisão do dev nesta issue — Gate 1 delegou ao UX/UI).

---

## T-01 — Sub-A: Integração de dados + Home (sub-issue #94)
Critérios de aceite: CA-A1 a CA-A7 + parte de CA-T1/CA-T2 (ver `criterios-aceite.md`).
Contexto técnico: seção 1 e 2 (linhas `lib/types.ts`/`lib/api.ts`/`DealCard`/`Header`/`app/page.tsx`) e seção 3 (fallback ISR) de `especificacao-tecnica.md`. Consultar o wireframe/tokens do UX/UI (path a ser adicionado em `docs_path` quando concluído) para layout do card e da grade.
Stack: Next.js 14 (App Router) + TypeScript. Repo: `DQM-BETA/omuletachou`, pasta `website/`.

## T-02 — Sub-B: Página de oferta + SEO de produto (sub-issue #95)
Depende de: T-01 (#94) mergeado em `desenv` (usa `lib/api.ts`/`lib/types.ts` reais).
Critérios de aceite: CA-B1 a CA-B9 + parte de CA-T1/CA-T2.
Contexto técnico: seções 1, 2 (`DealDetail`/`oferta/[slug]/page.tsx`), 4 (`notFound`) e 5 (SEO de produto) de `especificacao-tecnica.md`. Consultar wireframe do UX/UI para layout da página de produto.
Stack: Next.js 14 (App Router) + TypeScript.

## T-03 — Sub-C: Página de categoria + sitemap/robots (sub-issue #96)
Depende de: T-01 (#94) mergeado em `desenv` (usa `lib/api.ts`/`lib/types.ts` reais e reaproveita grade/`DealCard` de Sub-A).
Critérios de aceite: CA-C1 a CA-C6 + parte de CA-T1/CA-T2.
Contexto técnico: seções 1, 2 (`categoria/[categoria]/page.tsx`, `app/sitemap.ts`, `robots.txt`), 4 (estado vazio, não 404) e 5 (SEO de categoria/sitemap) de `especificacao-tecnica.md`.
Stack: Next.js 14 (App Router) + TypeScript.

---

## Transversal (todas as sub-issues, validar antes do PR)
- CA-T1: `npm run build` sem erros de TypeScript.
- CA-T2: nenhuma sub-issue deve adicionar try/catch que mascare erro de API fora do ar (ver seção 3 da especificação).
- CA-T3: nenhum `manifest.json`/service worker/push.
- CA-T4: nenhuma variável de produção/domínio/SSL alterada; se precisar adicionar `API_INTERNAL_URL` ao `docker-compose.yml`, usar apenas o valor de rede interna Docker já existente (`http://api:8080`), não uma URL de produção.
