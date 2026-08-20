---
issue: 228
titulo: feat: relatório de produtos com filtros (categoria/plataforma/status) na tela Reports
etapa_atual: Aguardando PR release (LT)
ultimo_agente: qa (aprovado, 2026-08-20)
openspec_change: openspec/changes/issue-228-relatorio-produtos-filtros
tech_stacks: [dotnet, angular]
repos:
  omuletachou: "https://github.com/DQM-BETA/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados
openspec_path: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros
sub_issues: ["#242 (stack:dotnet, task_id:T-01)", "#243 (stack:dotnet, task_id:T-02)", "#244 (stack:dotnet, task_id:T-03)", "#245 (stack:angular, task_id:T-04)"]
desenv_tasks_merged: ["#242", "#243", "#244", "#245"]
sub_issues_frontend: {"#245": "T-04"}
pr_homologacao: 250
pr_release: ~
code_review_homolog_pr: 250 — APROVADO na 2ª rodada (2026-08-20), merge commit desenv→homolog concluído (oid 5f639ad)
qa_status: aprovado
figma_url: ~
blockers: ~
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
- PR: https://github.com/DQM-BETA/omuletachou/pull/246 (feature→desenv) — **merged (squash) para desenv em 2026-08-19 pelo LT.**

## Sub-issue #243 (T-02) — Dev .NET (2026-08-19)
- Branch `feature/ISSUE-243-endpoint-reports-summary` (worktree `.worktrees/feature-ISSUE-243-endpoint-reports-summary`), base `desenv`.
- Novo action `ReportsController.ProductsSummary` (`GET /api/reports/products/summary`, `[Authorize]`): filtros opcionais AND (`category`, `subcategory`, `platform`, `status`, `collectedFrom`, `collectedTo`), janela `[from, toExclusive)` inclusiva sobre `CreatedAt`, sem filtro de status não restringe a Published, sem match retorna 200 com total 0 e as 4 listas vazias. Novos DTOs em `AfiliadoBot.Api/Reports/ReportsDtos.cs` (`ProductsReportSummaryDto` + 4 records de breakdown).
- TDD: 11 testes novos em `ReportsControllerTests` (status=Published breakdown completo CA 1.1, status=Pending explícito CA 2.4, filtros combinados AND CA 2.6, sem match CA 1.3/2.7, platform inválida sem 400, janela de data inclusiva CA 2.5, 401 sem token). Suíte completa: 480/480 passando, sem regressão.
- Validação real: Postgres isolado em container temporário (`test_pg_issue243`), API rodada localmente (`dotnet run`) contra ele — migrations aplicadas e boot sem exceção; dados reais inseridos via SQL direto validaram total/breakdowns/janela de data batendo com o esperado. Container e processo derrubados ao final, sem tocar no stack `afiliado_*` compartilhado (em uso pela sub-issue #244 em paralelo).
- PR: https://github.com/DQM-BETA/omuletachou/pull/247 (feature→desenv) — **merged (squash) para desenv em 2026-08-19 pelo LT.**

## Sub-issue #242 (T-01) — Dev .NET (2026-08-19)
- Branch `feature/ISSUE-242-indice-status-platform-createdat` (worktree `.worktrees/feature-ISSUE-242-indice-status-platform-createdat`), base `desenv`.
- Novo índice composto `IX_products_status_platform_createdat` (`Status`, `Platform`, `CreatedAt` desc) em `ProductConfiguration.cs`; migration EF Core `AddStatusPlatformCreatedAtIndex` (só `CREATE INDEX`, não é `UNIQUE`).
- TDD: teste novo `ProductConfigurationTests` (RED confirmado sem o índice, GREEN após adicioná-lo) inspeciona o EF model (design-time) — índice existe, colunas na ordem `Status, Platform, CreatedAt`, só `CreatedAt` descendente, não-único. Suíte completa: 477/477 passando, sem regressão.
- Validação real: migration aplicada com sucesso (a) incrementalmente sobre a base de dev local compartilhada (`afiliado_db`, via script idempotente + `psql`) e (b) do zero sobre Postgres isolado em container temporário (`dotnet ef database update` completo, todas as migrations). Índice confirmado via `psql`: `CREATE INDEX "IX_products_status_platform_createdat" ON public.products USING btree (status, platform, created_at DESC)`. `dotnet run` contra o Postgres isolado: app inicia sem exceção (boot do DI ok, migrations aplicadas automaticamente).
- PR: https://github.com/DQM-BETA/omuletachou/pull/248 (feature→desenv) — **merged (squash) para desenv em 2026-08-19 pelo LT (mergeado primeiro, contém a migration base).**

## Sub-issue #245 (T-04) — Dev Angular (2026-08-19)
- Branch `feature/ISSUE-245-filtros-relatorio-produtos` (worktree `.worktrees/feature-ISSUE-245-filtros-relatorio-produtos`), base `desenv`.
- `reports.service.ts`: novo método `productsSummary(filters)` (`GET /api/reports/products/summary`) + interfaces `ProductsReportFilters`/`ProductsReportSummary`; novo método `categories()` (`GET /api/public/categories`, endpoint público já existente da Issue #167, reaproveitado para popular os `mat-select` de Categoria/Subcategoria dos filtros — sem endpoint novo, conforme ux-ui-spec.md §3.2).
- `products.service.ts`: `ProductsListParams` ganhou `category?`, `subcategory?`, `collectedFrom?`, `collectedTo?` (aditivos); `ProductListItem` ganhou `subcategory?: string | null`.
- `reports.component.ts`/`.html`: novo bloco "Relatório de produtos publicados" abaixo dos cards Hoje/Semana/Mês e do gráfico existentes (inalterados, CA 1.2) — `filterForm` reativo (Categoria, Subcategoria, Plataforma via `mat-button-toggle-group`, Status, faixa de data via `mat-date-range-input`), auto-apply com `debounceTime(150)`+`distinctUntilChanged` (faixa de data só dispara com início E fim preenchidos), default `status=Published` quando o campo Status está vazio (CA 1.1/2.4), `forkJoin` (cards+página 1) ao aplicar/mudar filtro, só `list()` ao trocar de página (CA design.md §2.1), "Limpar filtros" (disabled sem filtro ativo, CA 2.8), chips de filtros ativos removíveis, estado vazio (CA 1.3/2.7) e erro compartilhado cards+tabela sem manter dado antigo + retry (CA 5.1). Cards de breakdown com mini-barra de proporção, ordenados por contagem desc, truncados a 5 + "expandir". Sem exportar/imprimir (CA 4.1).
- TDD: 3 arquivos de teste atualizados/estendidos — `reports.service.spec.ts` (+3 testes: `productsSummary` sem/com filtros combinados, `categories`), `products.service.spec.ts` (+2 testes: novos params, campo `subcategory`), `reports.component.spec.ts` (+22 testes cobrindo CA 1.1–1.3, 2.1–2.9, 4.1, 5.1, troca de página sem recalcular cards, falha ao carregar categorias não bloqueia o relatório, chips removíveis). Suíte completa: 172/172 passando, sem regressão. Cobertura `pages/reports`: 89.8%; `reports.service.ts`/`products.service.ts`: 100%.
- Validação real: `ng build` (production config) e `ng serve` sobem sem erro; `curl http://localhost:4301/reports` retornou 200 com o bundle do novo bloco compilado. Backend (#242/#243/#244) implementado em paralelo em worktrees separados — este PR usa mock do `HttpClient`/serviços (padrão já usado no projeto, ver Issue #237); integração real fica para Code Review/QA após todas as sub-issues mergeadas.
- Nota de implementação: `ux-ui-spec.md` §5 menciona reaproveitar "o mapeamento de cor por status já existente na tela Products" para a coluna Status da tabela — na prática `products.component.scss` não tem esse mapeamento por status (só um badge cinza único + cursor:help no Error). Implementado um mapeamento de cores por status (Published=verde, Pending=âmbar, Error=vermelho, demais=neutro) local ao `reports.component.scss`, sem alterar `ProductsComponent` (fora de escopo). Decisão de detalhe visual, não de negócio/arquitetura — registrado aqui para o Code Review avaliar se vale unificar com Products em follow-up.
- PR: https://github.com/DQM-BETA/omuletachou/pull/249 (feature→desenv) — **merged (squash) para desenv em 2026-08-19 pelo LT (mergeado por último, após os 3 PRs de backend).**

## Merge das sub-issues (LT — 2026-08-19)
- Ordem de merge (squash, feature→desenv): #248 (#242, índice/migration) → #247 (#243, endpoint summary) → #246 (#244, extensão products) → #249 (#245, frontend Angular). Ordem escolhida para aplicar primeiro a migration base e minimizar risco de conflito em `AfiliadoBotDbContextModelSnapshot.cs`.
- Todos os 4 merges concluídos **sem conflito real** — `mergeStateStatus: CLEAN` confirmado antes de cada merge (aguardado o recálculo do GitHub entre um merge e o próximo). Não houve necessidade de `gh pr update-branch`.
- 4 sub-issues fechadas (#242, #243, #244, #245) via `gh issue close --reason completed`.
- `desenv` local sincronizado via `git pull` (fast-forward 28fb75d..4890eac), confirmado com `git log`.
- PR de promoção `desenv→homolog` criado: **#250** (merge commit, não-squash, conforme convenção de promoção entre branches de longa vida).
- Nota de follow-up (não bloqueante) herdada do Dev de #245: mapeamento de cor por status ficou local em `reports.component.scss` (não reaproveitou `ProductsComponent`, que não tinha esse mapeamento) — Code Review avaliar se vale unificar em follow-up futuro.

## Code Review — PR #250 desenv→homolog (2026-08-19) — REPROVADO (1ª rodada)

- Execução completa: `dotnet test` 490/490, `npm test` 172/172, `docker compose build --no-cache api dashboard` + boot real (`/health` 200), migration `AddStatusPlatformCreatedAtIndex` confirmada aplicada e índice confirmado via `psql`.
- Integração real end-to-end contra Postgres real do container: `GET /api/reports/products/summary` e `GET /api/products` validados com filtros combinados (AND), sem-match (200/total 0), platform inválida (sem 400), 401 sem token, não-regressão de `GetProducts` sem os 4 novos params — todos ok.
- Checklist de veto: sem segredo commitado, `[Authorize]` ok nos dois controllers, sem N+1 (1 IQueryable base + Count + 4 GroupBy), sem `.first()`/`.nth()`/`.last()` em specs novas, sem teste-lixo.
- Nota do LT sobre mapeamento de cor por status local em `reports.component.scss`: avaliada e **aceita** — `ProductsComponent` de fato não tem esse mapeamento pronto hoje (só um badge cinza único), a premissa do ux-ui-spec estava incorreta.
- **Divergência bloqueante:** `ux-ui-spec.md` §8 exige bloco de filtros colapsado por padrão em `mat-expansion-panel` (com badge de contagem) no breakpoint mobile (<600px) — não implementado (só há `grid-template-columns: 1fr` via CSS, sem colapso/badge). Responsividade web é citada explicitamente como critério de veto do Code Review.
- Achado secundário (não bloqueante isolado): §4.3/§5.1 pedem skeleton no carregamento inicial dos cards/tabela; a implementação só usa `mat-progress-bar` no card de filtros, podendo piscar o estado "Nenhum dado"/"Nenhum produto encontrado" antes do primeiro `forkJoin` resolver.
- Evidência completa postada como comentário no PR: https://github.com/DQM-BETA/omuletachou/pull/250#issuecomment-5347125317
- Encaminhado ao LT: implementar o colapso mobile do bloco de filtros (bloqueante) + avaliar o gap de skeleton (opcional). Resto do PR aprovado, sem necessidade de re-trabalho fora desses itens.

## Mapeamento de falha — LT (2026-08-19)

**Decisão de fluxo:** reabrir a sub-issue **#245** (mesma sub-issue, mesma `task_id: T-04`) em vez de criar uma nova sub-issue de correção. Racional:
- A falha é uma correção de escopo já coberto por #245 (bloco de filtros do `ReportsComponent`), não uma tarefa nova.
- A convenção de branch (`feature/ISSUE-NNN-descricao`, NNN = nº da sub-issue) e o restante do pipeline (mapeamento de custo/ledger, `sub_issues_frontend`) já referenciam #245/T-04 — criar uma sub-issue nova fragmentaria o rastreio sem ganho.
- Não há precedente na squad de abrir sub-issue de correção separada para reprovação de Code Review pós-merge (revisado: melhorias `2026-06-18` e `2026-06-23` tratam reincidência/correção sempre pela issue original, nunca abrindo uma nova).
- `gh issue reopen 245 --repo DQM-BETA/omuletachou` executado, com comentário de mapeamento das duas falhas (link: https://github.com/DQM-BETA/omuletachou/issues/245).

**Mapeamento dos achados do Code Review (PR #250) → sub-issue #245:**
1. **Bloqueante** — `ux-ui-spec.md` §8 (Responsividade mobile <600px): bloco de filtros precisa estar dentro de `mat-expansion-panel`, colapsado por padrão, com badge de contagem de filtros ativos (ex. "Filtros (2)"), expandindo ao toque. Hoje só há `grid-template-columns: 1fr` via CSS, sempre expandido. Arquivos afetados: `dashboard/src/app/pages/reports/reports.component.html` e `.scss`.
2. **Secundário (incluir na mesma correção, não bloqueante isolado)** — `ux-ui-spec.md` §4.3/§5.1: skeleton de carregamento inicial ausente nos cards/tabela; hoje só há `mat-progress-bar`, o que pode piscar "Nenhum dado"/"Nenhum produto encontrado" antes do primeiro `forkJoin` resolver.

**Não escopo desta correção:** resto do PR #250 (backend T-01/T-02/T-03, contrato de dados, autorização, não-regressão, testes, mapeamento de cor por status) já aprovado pelo Code Review — dev não deve tocar nesses pontos.

**Fluxo de saída:** Dev Angular abre `feature/ISSUE-245-...` a partir de `desenv` atualizada, corrige os 2 itens, TDD dos cenários novos/ajustados, push, PR `feature→desenv`. Após o LT mergear esse PR (squash) em `desenv`, o PR #250 (`desenv→homolog`, ainda aberto) absorve o commit automaticamente — não é necessário recriar o PR de promoção. Code Review então reavalia o PR #250 atualizado.

## Correção sub-issue #245 (Dev Angular — 2026-08-20)

- Continuação de uma sessão anterior do Dev Angular que caiu por limite de spend do usuário antes de commitar (trabalho preservado no worktree, não perdido — 179/179 testes já validados por ela).
- Branch `feature/ISSUE-245-filtros-relatorio-produtos` (mesmo worktree, já com `desenv` mesclada no commit `d83c7ad` — sub-issues #242/#243/#244 presentes).
- Reconfirmados os 2 achados do Code Review já implementados no worktree (diff limpo vs. `origin/desenv`, só os 4 arquivos de `reports.component.*`):
  1. **Bloqueante** — colapso mobile do bloco de filtros: `mat-expansion-panel` (`data-testid="filters-mobile-panel"`), colapsado por padrão (`[expanded]="false"`), badge de contagem (`activeFiltersCount`, `data-testid="filters-active-badge"`) só em `isMobile` (via `BreakpointObserver`/`Breakpoints.Handset`); desktop/tablet mantém o bloco sempre expandido, sem panel. Controles de filtro fatorados em `<ng-template #filtersControls>` reaproveitado nos dois branches (`*ngTemplateOutlet`).
  2. **Secundário** — skeleton de carregamento inicial: `showInitialSkeleton` (`productsLoading && !productsSummaryData`) renderiza cards/tabela com `shimmer` no lugar do resultado, só na primeira carga (ou retry pós-erro) — troca de filtro com dado já carregado não reexibe skeleton.
- Testes novos em `reports.component.spec.ts` (10 casos): colapso mobile/desktop, badge ausente/presente com contagem, skeleton na carga inicial, skeleton ausente em recálculo com dado prévio, skeleton reaparece em novo retry pós-erro.
- `npm test` (Karma/Chrome Headless): **179/179 passando**, sem regressão (reconfirmado nesta sessão).
- `ng build --configuration production`: build ok, sem erros (só warnings pré-existentes de orçamento de bundle CSS/JS, não relacionados a esta correção).
- Nenhum arquivo backend tocado por esta correção (diff vs. `origin/desenv` restrito a `dashboard/src/app/pages/reports/`) — suíte `dotnet test` não fazia parte do escopo desta correção pontual de UI.
- Commit `001de17` na branch (push feito, branch já tinha 9 commits de merge de `desenv` à frente do remoto antes desta correção — todos legítimos, confirmados via `git log origin/...HEAD`).
- **PR novo criado:** #251 (`feature/ISSUE-245-filtros-relatorio-produtos` → `desenv`), já que o PR #249 (mesma direção) havia sido mergeado (squash) e fechado em 2026-08-19 — não é possível reabrir/reaproveitar um PR squash-mergeado. O PR #250 (`desenv→homolog`) segue aberto e absorverá o commit desta correção automaticamente assim que o LT mergear #251 em `desenv` (squash), sem necessidade de recriar #250.

## Correção Code Review — merge PR #251 (LT — 2026-08-20)
- PR https://github.com/DQM-BETA/omuletachou/pull/251 (feature→desenv) — **merged (squash) para desenv em 2026-08-20 pelo LT**, `mergedAt: 2026-08-20T12:20:24Z`.
- Sub-issue #245 fechada novamente (`gh issue close --reason completed`).
- PR #250 (desenv→homolog) confirmado atualizado automaticamente (mesma branch `desenv`) — 18 commits, `mergeStateStatus: CLEAN`, `mergeable: MERGEABLE`. Não foi necessário recriar o PR de promoção.
- `etapa_atual` volta a **Code Review** — aguardando reavaliação do PR #250 com a correção incluída.

## Code Review — PR #250 desenv→homolog (2026-08-20) — APROVADO (2ª rodada)

- Não-regressão confirmada: `dotnet test` 490/490 (idêntico à 1ª rodada), `npm test` 179/179 (172→179, +7 testes da correção), `docker compose build --no-cache api dashboard` OK, boot real (`/health` 200, dashboard `/` 200).
- Verificação direcionada aos 2 pontos da reprovação anterior, via Playwright contra os containers reais (login real com credenciais seed `operador@omuletachou.local`):
  1. **Colapso mobile (§8):** viewport 375px — `mat-expansion-panel[data-testid="filters-mobile-panel"]` presente e colapsado por padrão (`aria-expanded="false"`, sem `mat-expanded`) só em mobile; ausente em desktop. Badge (`data-testid="filters-active-badge"`) ausente sem filtro, mostra `(1)`/`(2)` corretamente com 1/2 filtros aplicados contra a API real (Plataforma=MercadoLivre, Status). Screenshot de evidência tirado.
  2. **Skeleton inicial (§4.3/§5.1):** delay artificial de 2s nas chamadas `GET /api/reports/products/summary` e `GET /api/products` via `page.route` — `products-summary-skeleton`/`products-table-skeleton` visíveis durante a espera, sem flash de "Nenhum dado"; some ao resolver, resultados reais aparecem.
- Checklist de veto revalidado: sem `.first()`/`.nth()`/`.last()` em specs e2e (nenhum Playwright novo neste PR), sem teste-lixo (10 testes novos com asserts reais de DOM/estado), sem segredo commitado.
- Achado não-bloqueante registrado (fora de escopo desta correção): Sidenav do Layout global não colapsa para overlay em viewport mobile (375px) — comportamento do shell, não do `ReportsComponent`; fica para avaliação futura, não bloqueia.
- Evidência completa postada como comentário no PR: https://github.com/DQM-BETA/omuletachou/pull/250#issuecomment-5355922801
- **Merge `desenv→homolog` (merge commit) executado**: oid `5f639adab83b3eeaccfe8c469416aa51d768f354`, `mergedAt: 2026-08-20T12:32:13Z`.
- `etapa_atual` avança para **QA**.

## QA — aprovado (2026-08-20)
- Branch `homolog` sincronizada (`git fetch` + `checkout` + `pull`); commit `5f639ad` (merge PR #250) confirmado presente via `git log`.
- `dotnet test`: 490/490. `npm test` (Angular): 179/179. `tsc --noEmit` do código de app: 0 erros (3 erros pré-existentes em arquivos de e2e/config, fora do escopo desta issue).
- `docker compose build --no-cache api dashboard` a partir de `homolog` + `docker compose up -d db api dashboard`: build e boot reais ok, `/health` 200, índice `IX_products_status_platform_createdat` confirmado via `psql` no Postgres real.
- Validação integrada real: login real (JWT), `GET /api/reports/products/summary` e `GET /api/products` exercitados contra dados reais (105 Published, breakdown por categoria/plataforma/status/subcategoria, filtros combinados AND, sem-match 200/total 0, não-regressão de `GetProducts` sem os 4 novos params). UI real via Playwright (login real, dados reais, imagem Docker de `homolog`): filtro de Categoria aplicado bate exatamente com a API (Total 17); "Limpar filtros" volta a 105; combinação sem resultado mostra estado vazio limpo; falha de rede simulada mostra erro claro + retry sem dado antigo; painel mobile (`mat-expansion-panel`) colapsado por padrão com badge de contagem confirmado via DOM real (ausente em desktop) — os 2 achados do Code Review anterior (colapso mobile, skeleton) confirmados corrigidos.
- Gate visual: `npm run test:visual` (Playwright) rodado com `STAGING_URL` apontando para a imagem Docker real de `homolog` e `SCREENSHOTS_DIR={docs_path}/screenshots` — 8/8 passando, PNGs arquivados em `documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/screenshots/`. Header/sidenav únicos em todas as telas, sem duplicação, layout condizente com `ux-ui-spec.md`.
- Todos os 16 cenários Given/When/Then de `criterios-aceite.md` (grupos 1–5) validados com evidência de execução real. Nenhuma issue encontrada. Relatório completo: `documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/relatorio-qa.md`.
- Stack Docker derrubada (`docker compose down`) ao final da validação.

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
| 10 | Dev Angular (sub-issue #245, T-04) | Dev Angular | sonnet-5 | ~ | ~ | ~ |
| 11 | Líder Técnico (merge 4 sub-issues + PR #250 desenv→homolog) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |
| 12 | Code Review (PR #250 desenv→homolog) — REPROVADO | Code Review | sonnet-5 | ~ | ~ | ~ |
| 13 | Líder Técnico (mapeamento falha CR, #245 reaberta) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |
| 14 | Dev Angular (correção sub-issue #245 — colapso mobile + skeleton, PR #251) | Dev Angular | sonnet-5 | ~ | ~ | ~ |
| 15 | Líder Técnico (merge PR #251 → desenv; confirma PR #250 atualizado) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |
| 16 | Code Review (PR #250 desenv→homolog, 2ª rodada) — APROVADO, merge homolog | Code Review | sonnet-5 | ~ | ~ | ~ |
| 17 | QA (validação integrada real — APROVADO) | QA | sonnet-5 | ~ | ~ | ~ |
