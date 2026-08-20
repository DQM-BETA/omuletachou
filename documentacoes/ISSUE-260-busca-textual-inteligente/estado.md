issue: 260
titulo: feat: busca textual inteligente (fonética/fuzzy) na tela de produtos do site público
rota: normal
etapa_atual: Em Desenvolvimento
ultimo_agente: lider-tecnico
openspec_change: repos/omuletachou/openspec/changes/issue-260-busca-textual-inteligente
tech_stacks: [dotnet, nodejs]
repos:
  omuletachou: true
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-260-busca-textual-inteligente
openspec_path: repos/omuletachou/openspec/changes/issue-260-busca-textual-inteligente
status_comment_id: ~
sub_issues: [267 (stack:dotnet, task_id:T-01), 268 (stack:dotnet, task_id:T-02), 269 (stack:nodejs, task_id:T-03)]
desenv_tasks_merged: []
sub_issues_frontend: {269: T-03}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tempo (s) |
|---|---|---|---|---|---|
| 1 | Preparar demanda | Coordenador | Haiku 4.5 | 1234 | 5 |

---

### Notas
- Item 4 da Issue #230, separado por decisão do Gerente no Gate 1 (2026-08-20)
- Restrição vinculante: técnica de BD (ex.: pg_trgm no Postgres), **NÃO** chamada à IA por requisição
- Referência: Issue #230 (itens 1-3, mesmo componente filter-bar)
- PM Fase 1 (2026-08-20): perguntas de levantamento postadas na Issue — eixos: localização na UI, escopo da busca (quais campos), comportamento de disparo (tempo real vs botão), exemplos concretos de sucesso da busca fonética/fuzzy, e confirmação de que a restrição "sem IA" é definitiva. Aguardando respostas do Gerente para Fase 2 (PRD).
- PM Fase 2 (2026-08-20): Gate 1 respondido pelo Gerente (postado como comentário na Issue para rastreabilidade) — campo novo na filter-bar; escopo = título+categoria+descrição com título priorizado; tempo real com resposta percebida como instantânea (alvo técnico <300-500ms a definir pelo Arquiteto/LT); meta qualitativa de cobertura máxima de erros de digitação/variação (sem exemplos concretos fornecidos); restrição "sem IA" confirmada como definitiva, não reabrir. PRD completo escrito em `proposal.md` + `criterios-aceite.md`. Ambiguidade arquitetural = **SIM** (técnica exata de fuzzy/similaridade dentro da restrição "sem IA": pg_trgm vs full-text vs combinação, estratégia de índice, threshold de similaridade, peso do título no ranking) — encaminhado ao **Arquiteto**.
- Arquiteto (2026-08-20): design.md escrito — estratégia em 2 estágios (full-text `tsvector`/`tsquery` ponderado título>categoria>descrição como estágio 1; `pg_trgm`/`similarity()` como fallback só quando estágio 1 devolve zero, estágio 2); índice GIN em `search_vector` (coluna gerada); wrapper `immutable_unaccent()`; endpoint = extensão aditiva de `GET /api/public/deals` com `q` + `IsApproximateSearch` (bool?) em `PagedResult<T>`; frontend reaproveita padrão draft+debounce+`router.replace` da Issue #230; novo `app/loading.tsx`. Threshold de similaridade = 0.15. Sem UI/tela nova — sem ambiguidade visual, sem Arquiteto mobile.
- LT (2026-08-20): task breakdown concluído. Grounding no código real (`PublicController.cs`, `PagedResult.cs`, `ProductConfiguration.cs`, `FilterBar.tsx`, `page.tsx`, `lib/api.ts`/`types.ts`, testes existentes) antes de escrever a especificação técnica — confirmado que `PublicControllerTests` roda hoje contra `CustomWebApplicationFactory` com `UseInMemoryDatabase` (não suporta `tsvector`/`pg_trgm`), documentado o precedente Testcontainers (`ClaudeBudgetServiceIntegrationTests`) como padrão a seguir para os novos testes de busca. `especificacao-tecnica.md` escrita (docs_path) com contratos exatos de migration/query/endpoint/frontend. `design.md` do Arquiteto commitado junto (estava pendente). 3 sub-issues: T-01 (#267, dotnet, migration/schema — pré-requisito de T-02), T-02 (#268, dotnet, endpoint 2 estágios + testes Testcontainers, depende de T-01), T-03 (#269, nodejs, FilterBar/page.tsx/loading.tsx, paralelizável desde já — contrato já fechado). Sem UX/UI (campo reaproveita padrão visual existente da filter-bar).
