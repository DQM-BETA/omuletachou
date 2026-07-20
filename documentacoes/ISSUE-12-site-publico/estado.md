# Estado — ISSUE-12: Site Publico Next.js (SSR + SEO)

## Campos principais
issue: 12
repo: omuletachou
titulo: feat: Site Publico Next.js (SSR + SEO)
rota: normal
etapa_atual: Em Desenvolvimento (Sub-A #94 mergeada em desenv via PR #97 squash — merge commit e718fdd; Sub-B #95 e Sub-C #96 desbloqueadas para prosseguir em paralelo, contrato lib/api.ts fechado)
docs_path: repos/omuletachou/documentacoes/ISSUE-12-site-publico
openspec_path: repos/omuletachou/openspec/changes/issue-12-site-publico
ultimo_agente: lider-tecnico
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

## Refinamento Técnico (LT) — concluído
- `design.md` (resumido, PM roteou sem Arquiteto): repos/omuletachou/openspec/changes/issue-12-site-publico/design.md
- `especificacao-tecnica.md`: repos/omuletachou/documentacoes/ISSUE-12-site-publico/especificacao-tecnica.md — contrato definitivo de `lib/api.ts`/`lib/types.ts` (espelha `PublicDealDto`/`PagedResult<T>` do backend, Issue #11), estrutura de componentes por sub-issue, estratégia de fallback do ISR, `notFound()`, pontos de SEO.
- `tasks.md`: repos/omuletachou/openspec/changes/issue-12-site-publico/tasks.md (T-01/T-02/T-03, mapeados às sub-issues reais)
- **Decisão de sequenciamento:** Sub-A (contrato `lib/api.ts` real) mergeada em `desenv` primeiro; Sub-B e Sub-C dependem dela e podem paralelizar entre si depois — contrato já fechado na especificação técnica desde já, evitando o dev "descobrir" a interface, mas a implementação real evita 2 devs no mesmo arquivo/conflito de build integrado (CA-T1).
- **Decisão de UX/UI:** demanda tem UI real e o Gate 1 delegou o layout ao UX/UI da squad (não ao critério do dev, "sem Figma — UX/UI define layout"). UX/UI entra no fluxo ANTES dos Devs, produzindo wireframe/tokens mínimo (grid de cards, página de produto, paleta/tipografia) a partir dos critérios funcionais do Gate 1.
- Comentário de resumo técnico postado na Issue #12: https://github.com/DQM-BETA/omuletachou/issues/12#issuecomment-5025948289

## Sub-issues
sub_issues: [#94 (stack:nodejs, task_id:T-01, Sub-A: Integração de dados + Home) — MERGED, #95 (stack:nodejs, task_id:T-02, Sub-B: Página de oferta + SEO — depende de #94, DESBLOQUEADA), #96 (stack:nodejs, task_id:T-03, Sub-C: Página de categoria + sitemap/robots — depende de #94, DESBLOQUEADA)]
desenv_tasks_merged: [#94]

Ordem de spawn recomendada: UX/UI primeiro (spec visual) → Dev #94 (Sub-A) → após merge de #94, Dev #95 e Dev #96 em paralelo.

## Merge Sub-A #94 (LT)
- PR #97 (`feature/94-integracao-home` → `desenv`): mergeado via squash. `mergeStateStatus` confirmado `CLEAN`/`MERGEABLE` antes do merge (estava `UNKNOWN` na checagem anterior, resolvido após nova consulta). Merge commit: `e718fdda9c882b39004aff9379bb255c4928e721`, mergedAt: 2026-07-20T20:42:42Z.
- Sub-issue #94 fechada (`gh issue close 94 --reason completed`).
- Contrato `lib/api.ts`/`lib/types.ts` definitivo disponível em `desenv` para Sub-B e Sub-C.
- Branch local `desenv` sincronizada com `origin/desenv` (fast-forward c3ad8f3..e718fdd).

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |
| 2 | PM Fase 1 | pm-analista-negocios | concluido |
| 3 | Gate 1 | Gerente | concluido |
| 4 | PM Fase 2 | pm-analista-negocios | concluido |
| 5 | Refinamento Tecnico | lider-tecnico | concluido |
| 6 | UX/UI — spec visual | ux-ui | concluido — ux-ui-spec.md escrito (grid de cards, anatomia do DealCard, layout da página de oferta, tokens de cor/tipografia mobile-first) |
| 7 | Dev Sub-A #94 | dev-nodejs | concluido — PR #97 (feature/94-integracao-home→desenv), lib/api.ts contrato definitivo, DealCard/Header, Home com ISR, 22 testes (100%), smoke test Docker real com dado no Postgres |
| 8 | Merge Sub-A #94 | lider-tecnico | concluido — PR #97 squash merge em desenv (e718fdd), sub-issue #94 fechada, #95/#96 desbloqueadas |
| 9 | Dev Sub-B #95 | dev-nodejs | concluido — PR #98 (feature/95-oferta-seo→desenv): `app/oferta/[slug]/page.tsx` (fetchDeal, revalidate 300, notFound), `components/DealDetail.tsx`, `lib/related-deals.ts` (fetchByCategory, 4 relacionados), `lib/seo.ts` (title/description/canonical/og-image/JSON-LD Product), `public/og-default.png` novo; 46 testes (100%), cobertura global 96.4%; smoke test Docker real (db+api+website, produto inserido no Postgres, GET /oferta/{slug} 200 com OG+JSON-LD confirmados no HTML, slug inexistente 404) |
| 10 | Dev Sub-C #96 | dev-nodejs | concluido — PR #99 (feature/96-categoria-sitemap→desenv): `app/categoria/[categoria]/page.tsx` (fetchByCategory, revalidate 300, generateMetadata "{Categoria} \| O Mulet Achou", estado vazio CA-C4 sem notFound()), `app/sitemap.ts` (dinâmico, pagina fetchDeals até esgotar totalPages, Home+categorias+ofertas com lastModified, `export const dynamic = 'force-dynamic'` para não quebrar `next build` no Dockerfile sem API disponível no estágio de build), `public/robots.txt` estático (Allow: /, referencia sitemap); 33 testes (100%), cobertura ≥80%; build TS sem erros; smoke test Docker real em stack isolada (`docker-compose.smoke96.yml` temporário, projeto `omuletachou96`, containers/portas distintas para não colidir com a stack da Sub-B rodando em paralelo — arquivo removido ao final, não commitado), dados reais no Postgres, `/categoria/eletronicos` 200 com grade real, `/categoria/inexistente-xyz` 200 com estado vazio (sem 404), `/sitemap.xml` e `/robots.txt` 200 com conteúdo correto. Ambas Sub-B (#95) e Sub-C (#96) concluídas — próximo: LT faz merge das duas e PR desenv→homolog. |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 32474 | 33 | 185s |
| 2 | PM Fase 1 | pm | sonnet | 29640 | 10 | 64s |
| 3 | PM Fase 2 | pm | sonnet | 50226 | 21 | 228s |
| 4 | Refinamento Tecnico | lider-tecnico | sonnet | 79581 | 28 | 242s |
| 5 | UX/UI — spec visual | ux-ui | sonnet | 49326 | 7 | 118s |
| 6 | Dev Sub-A #94 (PR #97) | dev-nodejs | sonnet | N/D (sessão interrompida por reinício do processo; PR #97 e estado.md confirmados manualmente pela sessão principal) | N/D | N/D |
| 7 | Merge Sub-A #94 (LT) | lider-tecnico | sonnet | 38965 | 14 | 96s |
| 8 | Dev Sub-C #96 (PR #99) | dev-nodejs | sonnet | PENDENTE_USAGE | PENDENTE_USAGE | PENDENTE_USAGE |
