# Estado — ISSUE-12: Site Publico Next.js (SSR + SEO)

## Campos principais
issue: 12
repo: omuletachou
titulo: feat: Site Publico Next.js (SSR + SEO)
rota: normal
etapa_atual: Refinamento Técnico
docs_path: repos/omuletachou/documentacoes/ISSUE-12-site-publico
openspec_path: repos/omuletachou/openspec/changes/issue-12-site-publico
ultimo_agente: pm-analista-negocios
status_comment_id: 5025494280
pr_homologacao: ~
code_review_homolog_pr: ~
pr_release: ~

## Contexto
Stack: Next.js 14 + TypeScript + ISR (App Router) — NÃO SSR puro (decisão do Gerente no Gate 1)
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependência: Issue #11 (REST API pública: `/api/public/deals`, `/api/public/deals/{slug}`, `/api/public/deals/category/{categoria}`, `/api/public/push/subscribe`) — já entregue em main.
Dependência adicional: Issue #18 (Sub-C: Site Next.js 14 — Scaffolding e Docker) — já concluída. O scaffold em `website/` (App Router, TypeScript, páginas stub, Dockerfile, serviço `website` no `docker-compose.yml` porta 3000) já existe; esta issue evolui esse scaffold, NÃO recria.

**Nota técnica:** Esta é a primeira issue de frontend público Next.js com conteúdo real do projeto. O site consumirá a API pública já disponível via Docker (rede interna: `http://api:8080`), nunca expondo URLs internas ao browser.

## PM Fase 1 — levantamento postado
Comentário de perguntas postado na Issue #12 (2026-07-20): https://github.com/DQM-BETA/omuletachou/issues/12#issuecomment-5025528041

Eixos levantados: estrutura do projeto, páginas/rotas e estratégia de renderização, SEO, PWA/Push, domínio/deploy, design/UI, CORS.

## Gate 1 — respostas do Gerente
Comentário: https://github.com/DQM-BETA/omuletachou/issues/12#issuecomment-5025866625

Resumo:
1. NÃO é greenfield — scaffold já existe em `website/` (Issue #18). Evoluir, não recriar.
2. Escopo de páginas confirmado: Home (`/`), oferta (`/oferta/[slug]`), categoria (`/categoria/[categoria]`). Todas com ISR `revalidate: 300` (5 min) — SEM SSR puro.
3. SEO obrigatório nesta issue (não adiar): `generateMetadata` dinâmico, Open Graph completo, Schema.org JSON-LD `Product` na página de oferta, `sitemap.xml` dinâmico via `app/sitemap.ts`, `robots.txt` estático.
4. PWA/Push fora de escopo — Issue #14 cuida disso integralmente.
5. Serviço `website` já existe no `docker-compose.yml` (porta 3000). Deploy de produção é escopo da Issue #15 — não mexer em config de produção aqui.
6. Sem Figma — UX/UI da squad define layout a partir dos critérios funcionais (cards de oferta, página de produto). Priorizar simplicidade/performance.
7. CORS já suficiente (Issue #11): `omuletachou.com.br`, `www.omuletachou.com.br`, `localhost:3000`.

## PM Fase 2 — PRD consolidado
- `proposal.md`: repos/omuletachou/openspec/changes/issue-12-site-publico/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-12-site-publico/criterios-aceite.md (organizados por Sub-A/B/C + Transversal, formato Given/When/Then)
- Comentário de sumário do PRD postado na Issue #12.

**Avaliação de ambiguidade arquitetural: SEM ambiguidade.** O Gate 1 já resolveu as decisões que poderiam ser arquiteturais (estratégia de renderização — ISR 300s em vez de SSR puro; escopo de SEO; integração via rede interna Docker). Os pontos técnicos remanescentes (fetch em Server Components com `next: { revalidate }`, fallback do ISR quando a API está fora do ar, `notFound()` para slug inexistente) são padrões bem estabelecidos do Next.js App Router, não decisões de arquitetura que exijam revisão do Arquiteto. Próximo agente: **Líder Técnico** (refinamento técnico + task breakdown; LT decide quando o UX/UI da squad entra no fluxo, antes dos Devs).

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

Agrupamento sugerido pelo PM (LT decide o breakdown técnico final):
- **Sub-A — Integração de dados + Home**: `lib/api.ts` (fetchDeals/fetchDeal/fetchByCategory), `DealCard.tsx`, `Header.tsx`, Home com ISR, filtros de plataforma/categoria, paginação.
- **Sub-B — Página de oferta + SEO de produto**: `DealDetail.tsx`, `oferta/[slug]/page.tsx` com ISR, `generateMetadata`, Open Graph, JSON-LD `Product`, 404 de slug inexistente.
- **Sub-C — Página de categoria + sitemap/robots**: `categoria/[categoria]/page.tsx` com ISR, `app/sitemap.ts` dinâmico, `robots.txt` estático.

Dependência: Sub-A entrega `lib/api.ts` e componentes de card compartilhados — Sub-B/Sub-C dependem dela, mas podem iniciar em paralelo com contrato de `lib/api.ts` acordado antecipadamente.

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |
| 2 | PM Fase 1 | pm-analista-negocios | concluido |
| 3 | Gate 1 | Gerente | concluido |
| 4 | PM Fase 2 | pm-analista-negocios | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 32474 | 33 | 185s |
| 2 | PM Fase 1 | pm | sonnet | 29640 | 10 | 64s |
| 3 | PM Fase 2 | pm | sonnet | 50226 | 21 | 228s |
