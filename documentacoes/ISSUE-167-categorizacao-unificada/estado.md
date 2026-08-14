---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Retomado pelo Gerente ("pode seguir na rota normal") — LT segue para task breakdown + sub-issues
ultimo_agente: lider-tecnico
rota: normal
openspec_change: repos/omuletachou/openspec/changes/issue-167-categorizacao-unificada
tech_stacks:
  - dotnet
  - nodejs
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-167-categorizacao-unificada
openspec_path: repos/omuletachou/openspec/changes/issue-167-categorizacao-unificada
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: IC_kwDOTMlfyM8AAAABO7lC2w
createdAt: 2026-08-14
---

## Resumo
Demanda `backlog`: categorização unificada de produtos (Category + Subcategory) e remoção da distinção de plataforma (Amazon/MercadoLivre/Shopee) no site público. PM Fase 1 (validação técnica + levantamento) e Gate 1 (respostas do Gerente) concluídos na Issue. PM Fase 2 concluída: PRD (`proposal.md`) e critérios de aceite (Given/When/Then) escritos. Arquiteto concluído: `design.md` com as 3 decisões técnicas (contabilização de custo via `Usage` do Anthropic.SDK, 5 índices compostos, remoção da rota antiga de categoria com migração do `website`) + achado crítico de dependência circular (`CategoryDetector` precisa migrar de `Application` para `Domain`). **LT concluído: `especificacao-tecnica.md` consolidando tudo em plano executável — rota `backlog` termina aqui.**

## Decisões do Gate 1 (Gerente) incorporadas ao PRD
1. Taxonomia v1 fechada: 9 categorias, 3-5 subcategorias cada (~35 subcategorias). `Category`/`Subcategory` = VARCHAR livre (config versionada, não schema/enum).
2. Sem recategorização retroativa — só produtos novos a partir da mudança. Volume residual em "Geral" pós-lançamento é backlog separado.
3. Arquitetura de 2 camadas: dicionário (camada 1) roda na coleta (`CollectAsync`, sem custo de IA); fallback IA (camada 2) permanece restrito ao `ProcessorJob`, só para produtos aprovados (`Status == Queued`) — NÃO combinado com `ScoreProductAsync`. Teto de gasto: `claude.monthly_budget_limit_brl` em `app_settings`, default R$30/mês, desativa camada 2 automaticamente ao estourar (scoring/legenda sempre ativos).
4. Ordenação padrão continua por `AiScore` — novos filtros/ordenações são opcionais.
5. Remoção de `Platform` do DTO público é higiene de contrato de dados (não expor estratégia de curadoria por plataforma via scraping), não visual — confirmado que não há badge hoje. `Platform` continua interno/dashboard/AffiliateLink.

## Decisões do Arquiteto incorporadas ao design.md
1. **Achado crítico**: `AfiliadoBot.Infrastructure` só referencia `AfiliadoBot.Domain` (não `Application`) — `CategoryDetector` precisa mover de `Application` para `Domain` (namespace `AfiliadoBot.Domain.Services`), senão os 3 collectors não compilam ao chamá-lo em `CollectAsync`.
2. Custo Claude: usa `Usage` (tokens) já retornado por `Anthropic.SDK` (nenhuma estimativa própria); contabilizado só no ponto do fallback de categorização (`ClassifyCategoryAsync`), não transversal; persistido em `app_settings` (`claude.monthly_usage`, JSON), reset lazy mensal; `UPDATE` SQL atômico (`CASE`) evita race condition entre execuções concorrentes do `ProcessorJob`.
3. 5 índices compostos definidos, `status` sempre líder (`IX_products_status_aiscore` + 4 variantes `status+category+subcategory+<sort>`).
4. Rota antiga `/api/public/deals/category/{categoria}` **removida** do backend; `website/app/categoria/[categoria]/page.tsx` mantém a URL pública, só migra `fetchByCategory` para a nova querystring — ordem de deploy obrigatória (subir `GetDeals` novo → migrar frontend → só então remover rota antiga) para não quebrar produção.

## Documentação produzida
- `openspec/changes/issue-167-categorizacao-unificada/proposal.md` — PRD completo.
- `documentacoes/ISSUE-167-categorizacao-unificada/criterios-aceite.md` — 27 cenários Given/When/Then.
- `openspec/changes/issue-167-categorizacao-unificada/design.md` — design técnico do Arquiteto (3 decisões + achado de dependência circular).
- `documentacoes/ISSUE-167-categorizacao-unificada/especificacao-tecnica.md` — **novo (esta invocação)**: plano técnico executável, cobrindo migration (coluna + 5 índices + seeds de `app_settings`), mover `CategoryDetector` (com mapeamento de todos os arquivos que o referenciam), estrutura de dados do dicionário expandido (9 categorias/~35 subcategorias), integração nos 3 collectors, fallback IA no `ProcessorJob` (com reordenação `EnsureCategoryFallbackAsync` antes de `EnsureSlug`), contador de orçamento (`IClaudeBudgetService` + `UPDATE` atômico), `PublicDealDto`/`PublicController` (remoção de `Platform`, novo `GetDeals` com filtros, `GET /api/public/categories`, remoção de `GetByCategory`), migração do `website` (`api.ts`, `types.ts`, `Header.tsx` sem chips de plataforma, novo `FilterBar`). Inclui sugestão de task breakdown em 4 sub-issues (`backend-schema-collectors`, `backend-ia-orcamento`, `backend-api-filtros`, `frontend-filtros`) e avaliação de UX/UI.

## Avaliação de UX/UI (registrada pelo LT, não spawnado nesta invocação)
**Recomendado.** O `FilterBar` da Home é UI nova real (dropdowns dependentes categoria→subcategoria,
slider de faixa de preço, botões de desconto mínimo, seletor de ordenação) — não é ajuste de CSS em
componente existente. Recomenda-se UX/UI (mockup do `FilterBar`) antes da sub-issue
`frontend-filtros`, quando a issue for retomada pela rota `normal`.

## Próximos passos (quando o Gerente decidir retomar via rota `normal`)
1. Sessão principal spawna o LT novamente na seção "Refinamento" normal — como o repo já existe e
   as docs já estão prontas (`proposal.md`, `criterios-aceite.md`, `design.md`,
   `especificacao-tecnica.md`), o LT pula direto para: escrever `tasks.md` (openspec) + criar as
   sub-issues no GitHub (sugestão de fatiamento já registrada acima) + mover `documentacoes/` e
   `openspec/changes/` (já estão dentro do repo, não em staging — nada a mover).
2. UX/UI (recomendado, ver avaliação acima) antes da sub-issue `frontend-filtros`.
3. Dev(s) por sub-issue, seguindo `especificacao-tecnica.md`.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 49906 | 22 | 158s |
| 2 | PM Fase 1 (validação técnica + levantamento, Gate 1) | PM | Sonnet | 60461 | 28 | 240s |
| 3 | PM Fase 2 (PRD + critérios de aceite) | PM | Sonnet | 51527 | 16 | 225s |
| 4 | Arquiteto (3 decisões técnicas + achado de dependência circular) | Arquiteto | Sonnet | 110481 | 49 | 673s |
| 5 | Líder Técnico (especificação técnica consolidada) | Líder Técnico | Sonnet | 100393 | 32 | 288s |

**Total acumulado (backlog, sem dev):** 372.768 tokens · ~24 min proc.
