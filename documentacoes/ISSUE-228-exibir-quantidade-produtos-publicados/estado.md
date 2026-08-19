---
issue: 228
titulo: feat: relatório de produtos com filtros (categoria/plataforma/status) na tela Reports
etapa_atual: Em Desenvolvimento
ultimo_agente: dev-dotnet (#242)
openspec_change: openspec/changes/issue-228-relatorio-produtos-filtros
tech_stacks: [dotnet, angular]
repos:
  omuletachou: "https://github.com/DQM-BETA/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados
openspec_path: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros
sub_issues: ["#242 (stack:dotnet, task_id:T-01)", "#243 (stack:dotnet, task_id:T-02)", "#244 (stack:dotnet, task_id:T-03)", "#245 (stack:angular, task_id:T-04)"]
desenv_tasks_merged: []
sub_issues_frontend: {"#245": "T-04"}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~
rota: normal
---

## Resumo
Adicionar relatório de produtos publicados no site à tela `Reports`, com filtros combináveis (Categoria, Subcategoria, Plataforma, Status, Faixa de data de coleta) e exibição em cards de resumo agregados + tabela/gráfico detalhado, on-demand.

## Contexto
A tela `Reports` (`localhost:8081/reports`) mostra cards de "Hoje/Semana/Mês" e gráfico de "Publicações por rede (últimos 7 dias)" — ambos baseados na fila de publicação social (`publication_queue`). Desde Issue #208, a visibilidade no site é desacoplada da rede social (um produto pode estar `Published` no site sem passar pela fila social). Faltava indicador de quantos produtos estão realmente publicados e visíveis no site — agora especificado nesta issue.

## Gate 1 — Respostas do Gerente (2026-08-19)
1. Objetivo de negócio confirmado: ferramenta operacional interna do dashboard (admin), não é feature do site público.
2. Filtros v1: Categoria, Plataforma, Status (já certos) + **Subcategoria** e **Faixa de data de coleta** entram na v1. **Faixa de desconto fica fora do escopo** (não implementar).
3. Formato: combinação — cards de resumo agregados + tabela/gráfico detalhado abaixo (opção d).
4. Sem exportação/impressão nesta versão — só consulta em tela.
5. Sem atualização em tempo real — recalcula on-demand ao aplicar/mudar filtro.
6. Rota promovida de `backlog` para `normal` — segue o pipeline completo.

Comentário com as respostas: https://github.com/DQM-BETA/omuletachou/issues/228#issuecomment-5346638492

## PRD (PM Fase 2 — 2026-08-19)
- `proposal.md`: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/criterios-aceite.md (5 grupos de cenários Given/When/Then: exibição padrão, filtros combináveis, atualização on-demand, sem exportação, tratamento de erro)

## Ambiguidade arquitetural avaliada
**Sim.** Contrato do endpoint de relatório (agregados vs. detalhado — um endpoint ou dois), performance de agregação com múltiplos filtros combinados em `products` (possível necessidade de índice novo, especialmente `Subcategory` e data de coleta), se as agregações são calculadas em tempo real a cada request ou exigem cache/materialização, e formato/tipo da coluna de data de coleta para suportar filtro de faixa. Encaminhado ao **Arquiteto** antes do refinamento do LT.

## Rota
`normal` — promovida pelo Gerente no Gate 1 (2026-08-19). Segue o pipeline completo.

## Arquitetura (Arquiteto)
`design.md` completo em `openspec_path` — decisões: dois endpoints (`GET /api/reports/products/summary` novo para cards agregados + extensão aditiva de `GET /api/products` para a tabela/gráfico detalhado), índice composto novo `IX_products_status_platform_createdat`, sem cache/materialização (query direta on-demand), default `Status=Published` implementado no Angular (não no backend), `Product.CreatedAt` já é a data de coleta (sem migration de tipo).

## Refinamento Técnico (LT — 2026-08-19)
- `especificacao-tecnica.md`: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/especificacao-tecnica.md — contratos de API (endpoint novo + extensão), schema, padrões obrigatórios.
- `tasks.md`: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros/tasks.md — 4 sub-tarefas com critérios Given/When/Then + contexto técnico.
- Task breakdown: 4 sub-issues criadas — 3 stack:dotnet (índice/migration, endpoint summary, extensão GetProducts) + 1 stack:angular (filtros/cards/tabela na tela Reports).
- Design.md do Arquiteto (estava pendente de commit) commitado junto com este refinamento.
- Demanda tem UI (design.md §4 lista componentes de filtros/cards/tabela-gráfico no Angular) — UX/UI atua antes do dev da sub-issue #245.

## UX/UI (2026-08-19)
- `ux-ui-spec.md`: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/ux-ui-spec.md — spec visual da sub-issue #245.
- Figma da squad só tem o kit de tokens genérico (sem mockups de `Products`/`Jobs`/`Reports`) — spec ancorada no Angular Material já em uso no dashboard (reaproveita componentes/padrões visuais já existentes em `ProductsComponent`), com tokens de cor do Figma (Iris/Fuschia) mapeados como acento conceitual ao tema Material existente (não paleta nova).
- Decisão de UX que resolve a ambiguidade "tabela/gráfico" do proposal: tabela paginada (`mat-table`) para o detalhe (dado columnar por produto) + mini barras de proporção embutidas nos cards de breakdown (não um gráfico `ng2-charts` separado — dado já é o agregado do summary).
- Filtros com auto-apply (sem botão "Aplicar"), chips removíveis individualmente, "Limpar filtros" com estado disabled quando não há filtro ativo, tooltip explicando "data de coleta ≠ data de publicação".
- Todos os estados especificados (default/loading/vazio/erro/sucesso; disabled/readonly marcados N/A onde não se aplicam) para filtros, cards e tabela — erro compartilhado entre cards+tabela (CA 5.1, nunca mostra dado antigo).
- Heurísticas de Nielsen traduzidas em critérios verificáveis (tabela §7 do spec) + responsividade por 3 breakpoints (desktop/tablet/mobile) + fluxo de navegação (sem rota nova, tudo dentro de `ReportsComponent`).

## Sub-issue #244 (T-03) — Dev .NET (2026-08-19)
- Branch `feature/ISSUE-244-extensao-get-products` (worktree `.worktrees/feature-ISSUE-244-extensao-get-products`), base `desenv`.
- `ProductsController.GetProducts` ganhou os 4 filtros aditivos (`category`, `subcategory`, `collectedFrom`, `collectedTo`); `ProductListItemDto` ganhou `Subcategory` (`string?`) ao final.
- TDD: 9 testes novos em `ProductsControllerTests` (category, subcategory, faixa de data inclusiva, AND combinado, campo `Subcategory` no payload, não-regressão explícita sem os 4 novos params). Suíte completa: 479/479 passando.
- Validação real: build da imagem Docker da API a partir da branch, container temporário conectado ao Postgres do ambiente local (`omuletachou_omuletachou_net`) — boot sem exceção, filtros exercitados contra dados reais (`category=Casa e Cozinha`, `subcategory=Eletroportáteis`, faixa de data, combinação AND), container removido após validar.
- PR: https://github.com/DQM-BETA/omuletachou/pull/246 (feature→desenv), aguardando merge do LT.

## Sub-issue #243 (T-02) — Dev .NET (2026-08-19)
- Branch `feature/ISSUE-243-endpoint-reports-summary` (worktree `.worktrees/feature-ISSUE-243-endpoint-reports-summary`), base `desenv`.
- Novo action `ReportsController.ProductsSummary` (`GET /api/reports/products/summary`, `[Authorize]`): filtros opcionais AND (`category`, `subcategory`, `platform`, `status`, `collectedFrom`, `collectedTo`), janela `[from, toExclusive)` inclusiva sobre `CreatedAt`, sem filtro de status não restringe a Published, sem match retorna 200 com total 0 e as 4 listas vazias. Novos DTOs em `AfiliadoBot.Api/Reports/ReportsDtos.cs` (`ProductsReportSummaryDto` + 4 records de breakdown).
- TDD: 11 testes novos em `ReportsControllerTests` (status=Published breakdown completo CA 1.1, status=Pending explícito CA 2.4, filtros combinados AND CA 2.6, sem match CA 1.3/2.7, platform inválida sem 400, janela de data inclusiva CA 2.5, 401 sem token). Suíte completa: 480/480 passando, sem regressão.
- Validação real: Postgres isolado em container temporário (`test_pg_issue243`), API rodada localmente (`dotnet run`) contra ele — migrations aplicadas e boot sem exceção; dados reais inseridos via SQL direto validaram total/breakdowns/janela de data batendo com o esperado. Container e processo derrubados ao final, sem tocar no stack `afiliado_*` compartilhado (em uso pela sub-issue #244 em paralelo).
- PR: https://github.com/DQM-BETA/omuletachou/pull/247 (feature→desenv), aguardando merge do LT.

## Sub-issue #242 (T-01) — Dev .NET (2026-08-19)
- Branch `feature/ISSUE-242-indice-status-platform-createdat` (worktree `.worktrees/feature-ISSUE-242-indice-status-platform-createdat`), base `desenv`.
- Novo índice composto `IX_products_status_platform_createdat` (`Status`, `Platform`, `CreatedAt` desc) em `ProductConfiguration.cs`; migration EF Core `AddStatusPlatformCreatedAtIndex` (só `CREATE INDEX`, não é `UNIQUE`).
- TDD: teste novo `ProductConfigurationTests` (RED confirmado sem o índice, GREEN após adicioná-lo) inspeciona o EF model (design-time) — índice existe, colunas na ordem `Status, Platform, CreatedAt`, só `CreatedAt` descendente, não-único. Suíte completa: 477/477 passando, sem regressão.
- Validação real: migration aplicada com sucesso (a) incrementalmente sobre a base de dev local compartilhada (`afiliado_db`, via script idempotente + `psql`) e (b) do zero sobre Postgres isolado em container temporário (`dotnet ef database update` completo, todas as migrations). Índice confirmado via `psql`: `CREATE INDEX "IX_products_status_platform_createdat" ON public.products USING btree (status, platform, created_at DESC)`. `dotnet run` contra o Postgres isolado: app inicia sem exceção (boot do DI ok, migrations aplicadas automaticamente).
- PR: https://github.com/DQM-BETA/omuletachou/pull/248 (feature→desenv), aguardando merge do LT.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (registar backlog) | Coordenador | haiku-4.5 | ~ | ~| ~ |
| 2 | Atualização (escopo expandido, 2026-08-19) | Coordenador | haiku-4.5 | ~ | ~ | ~ |
| 3 | PM Fase 1 (levantamento) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 4 | PM Fase 2 (PRD + critérios de aceite) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 5 | Arquiteto (design.md) | Arquiteto | sonnet-5 | ~ | ~ | ~ |
| 6 | Refinamento Técnico (task breakdown) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |
| 7 | UX/UI (spec visual sub-issue #245) | UX/UI | sonnet-5 | ~ | ~ | ~ |
| 8 | Dev .NET (sub-issue #244, T-03) | Dev .NET | sonnet-5 | ~ | ~ | ~ |
| 9 | Dev .NET (sub-issue #242, T-01) | Dev .NET | sonnet-5 | ~ | ~ | ~ |
| 9 | Dev .NET (sub-issue #243, T-02) | Dev .NET | sonnet-5 | ~ | ~ | ~ |
