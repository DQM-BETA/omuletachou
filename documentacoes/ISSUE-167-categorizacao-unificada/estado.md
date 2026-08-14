---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Code Review — 4 sub-issues mergeadas em desenv; PR de homologação #176 (desenv->homolog) criado
ultimo_agente: lt
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
sub_issues:
  - "#168 (backend-schema-collectors, stack:dotnet, task_id:Sub-A) — CONCLUÍDA, merged em desenv"
  - "#169 (backend-ia-orcamento, stack:dotnet, task_id:Sub-B) — CONCLUÍDA, merged em desenv"
  - "#170 (backend-api-filtros, stack:dotnet, task_id:Sub-C) — CONCLUÍDA, merged em desenv"
  - "#171 (frontend-filtros, stack:nodejs, task_id:Sub-D) — CONCLUÍDA, merged em desenv"
desenv_tasks_merged: ["#168", "#169", "#170", "#171"]
sub_issue_168_pr: "#172 (feature/ISSUE-168-schema-collectors -> desenv, MERGED squash, commit fc083f3; branch remota deletada; sub-issue #168 fechada)"
sub_issue_169_pr: "#174 (feature/ISSUE-169-ia-orcamento -> desenv, MERGED squash, commit 03cb40e; branch remota deletada; sub-issue #169 fechada)"
sub_issue_170_pr: "#173 (feature/ISSUE-170-api-filtros -> desenv, MERGED squash, commit 03c7a05; branch remota deletada; sub-issue #170 fechada)"
sub_issue_171_pr: "#175 (feature/ISSUE-171-frontend-filtros -> desenv, MERGED squash, commit 0142d86; branch remota deletada; sub-issue #171 fechada)"
sub_issues_frontend: {}
pr_homologacao: "#176 (desenv -> homolog, merge commit, aberto)"
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: IC_kwDOTMlfyM8AAAABO7lC2w
createdAt: 2026-08-14
---

## Resumo
Demanda retomada pelo Gerente em rota `normal` ("pode seguir na rota normal") — toda a documentação
(PRD, critérios de aceite, design do Arquiteto, especificação técnica) já estava pronta da rodada
`backlog` anterior. LT concluiu o task breakdown final: `tasks.md` escrito e 4 sub-issues criadas
no GitHub, revisando (e mantendo, sem alterações estruturais) a sugestão de fatiamento já
registrada em `especificacao-tecnica.md`.

## Task breakdown final (4 sub-issues)
1. **#168 — backend-schema-collectors** (`stack:dotnet`): migration (coluna `Subcategory` + 5
   índices + seeds de orçamento), mover `CategoryDetector` para `Domain`, dicionário expandido (9
   categorias/~35 subcategorias), integração nos 3 collectors. **Bloqueante — sem dependências,
   base para #169 e #170.**
2. **#169 — backend-ia-orcamento** (`stack:dotnet`, depende de #168 em `desenv`): fallback IA no
   `ProcessorJob` (reordenado, antes do slug), `IClaudeBudgetService` (orçamento mensal com UPDATE
   atômico), `ClassifyCategoryAsync` no `ClaudeAiService`.
3. **#170 — backend-api-filtros** (`stack:dotnet`, depende de #168 em `desenv`, paralelo a #169):
   `PublicDealDto` sem `Platform`, `GetDeals` com filtros combináveis, `GET /api/public/categories`,
   remoção de `GetByCategory` (sem deploy isolado antes de #171 pronta).
4. **#171 — frontend-filtros** (`stack:nodejs`, depende de #170 para o contrato final; pode
   começar `api.ts`/`types.ts`/`Header.tsx` e o `FilterBar` contra mocks em paralelo): migração de
   `fetchByCategory`, remoção dos chips de plataforma do `Header`, novo `FilterBar` na Home.

Ordem de merge: **#168 primeiro → (#169 ‖ #170) → #171**, release `homolog→main` só com as 4
sub-issues prontas juntas (design.md §5.2 exige #170+#171 no mesmo deploy).

## Decisão UX/UI (confirmada)
**Aciona o UX/UI**, antes da sub-issue #171 (frontend-filtros). O `FilterBar` é UI nova real
(dropdowns dependentes, slider de faixa de preço, botões de desconto mínimo, seletor de ordenação).
As sub-issues de backend (#168, #169, #170) não dependem do UX/UI — **podem começar em paralelo**.

## UX/UI — FilterBar (Sub-D)
Spec visual concluída em `documentacoes/ISSUE-167-categorizacao-unificada/ux-ui-spec-filterbar.md`.
Reconsultado o Figma do design system (`yi6YkNAy9HfHus2oiPi3G7`) — segue vazio/padrão, sem tokens
novos desde a Issue #154; a spec reaproveita 100% dos tokens já implementados (`--color-primary`,
Work Sans, grid 8pt, raios, sombras), sem paleta nova. Decisões principais:
- **Responsivo**: `<1024px` colapsa em barra-resumo (botão "Filtros" + ordenação inline) + painel
  bottom-sheet (`filter-bar__drawer`) com os demais controles, mais um FAB de reabertura durante o
  scroll do grid; `≥1024px` (mesmo limiar de `deals-grid`) expõe os 5 controles em uma única linha —
  justificativa: 5 controles não cabem em scroll horizontal de chips como o `site-header` (Issue
  #154), e ocultar tudo atrás de 1 botão no desktop desperdiçaria espaço disponível.
- **Base técnica**: sem Radix/RNR (stack é Next.js + CSS puro, mesmo padrão já implementado na
  Issue #154) — elementos HTML nativos e acessíveis (`button`, `input[type=range]`,
  `ul[role=listbox]`) com classes BEM `filter-bar__*`.
- **Estados cobertos**: dropdown default/hover/foco/aberto-fechado/disabled (subcategoria antes de
  escolher categoria, com texto explicativo — heurística de prevenção de erros); botões de desconto
  default/hover/active(toggle único)/pressed; slider default/hover/drag/foco; "Limpar filtros"
  habilitado/disabled (visível mesmo sem filtro ativo); pílulas de filtro ativo removíveis
  individualmente.
- **Heurísticas de Nielsen**: visibilidade do status (filtro ativo sempre destacado em
  `--color-primary`, badge de contagem), controle do usuário (limpar tudo, remover pílula
  individual, 3 formas de fechar o drawer), prevenção de erros, reconhecimento vs. memorização.
- **Estado vazio (CA 7.5)**: reaproveita 100% `.deals-empty` (Issue #154), só muda o texto/mensagem
  e adiciona o CTA "Ver todas as ofertas" usando a classe já existente `.deal-card__cta` — nenhum
  componente visual novo.
- Nenhuma mudança de estrutura de dados/API — escopo estritamente visual, contrato de filtros já
  fechado por `especificacao-tecnica.md`/`design.md` (LT/Arquiteto).

## Documentação produzida (definitivo, dentro do repo)
- `openspec/changes/issue-167-categorizacao-unificada/proposal.md` — PRD.
- `documentacoes/ISSUE-167-categorizacao-unificada/criterios-aceite.md` — 27 cenários Given/When/Then.
- `openspec/changes/issue-167-categorizacao-unificada/design.md` — design técnico do Arquiteto.
- `documentacoes/ISSUE-167-categorizacao-unificada/especificacao-tecnica.md` — plano técnico
  executável completo.
- `openspec/changes/issue-167-categorizacao-unificada/tasks.md` — task breakdown final com as 4
  sub-tarefas (critérios de aceite + contexto técnico + ordem de dependência), referenciado pelas 4
  sub-issues.
- `documentacoes/ISSUE-167-categorizacao-unificada/ux-ui-spec-filterbar.md` — spec visual completa
  do `FilterBar` (layout desktop/mobile, classes BEM, tokens, estados, heurísticas de Nielsen,
  fluxo de navegação).

## Dev #168 — backend-schema-collectors (CONCLUÍDO — merged em desenv)
Branch `feature/ISSUE-168-schema-collectors` → PR #172 para `desenv`, squash merge (commit
`fc083f3`), branch remota e local deletadas, sub-issue #168 fechada com comentário de resumo.
`dotnet test`: 383/383 passando. Stack Docker validada (build+boot limpo, migration aplicada
contra Postgres real, ambiente removido ao final).

**Revisão do LT no merge**: diff do PR #172 conferido (migration + `CategoryDetector` em `Domain`
+ `Product.SetCategory` estendido + integração nos 3 collectors — escopo bate com o descrito).
Dupla checagem dos Ids de `app_settings`: `SeedFacebookCredentials` (última migration de seed
anterior) usa Ids 49-50; a migration desta sub-issue usa 51-55 (confirmado no `.cs`, `.Designer.cs`
e `ModelSnapshot.cs`) — sem colisão.

**Achado de infra durante a implementação** (não bloqueante, contornado nesta sub-issue, mas vale
registrar em `.claude/melhorias/` para próximas issues que semeiam `app_settings`): os Ids 41-50
já estão ocupados no banco real por 3 migrations anteriores (`SeedTikTokCredentials`,
`SeedPushVapidKeys`, `SeedFacebookCredentials`) que inseriram dados via `InsertData` direto na
migration, sem atualizar o `HasData` declarativo de `AppSettingConfiguration.cs` nem o
`ModelSnapshot.cs` — por isso o `dotnet ef migrations add` (que só enxerga o modelo declarativo)
ofereceu Id 41 como "livre", e só rodando a migration contra um Postgres real o conflito
(`duplicate key`) apareceu. Corrigido usando Ids 51-55 (conferidos varrendo todas as migrations,
não só o `HasData` atual). Os próprios seeds desta sub-issue (`claude.monthly_budget_limit_brl` e
demais) seguem o mesmo padrão "órfão" (`InsertData` direto, sem `HasData` correspondente em
`AppSettingConfiguration.cs`) — decisão consciente para não regenerar/reconciliar todo o histórico
de seeds fora do escopo desta sub-issue, mas o padrão do repo tende a divergir ainda mais se
repetido. Registrado como melhoria formal:
`.claude/melhorias/2026-08-14-devops-app-settings-seed-ids-colidem-fora-do-snapshot.md`.

**Também fora de escopo (observação, sem ação necessária)**: `YoutubePublisher.CategoryMap`
(`backend/src/AfiliadoBot.Infrastructure/Integrations/Social/YoutubePublisher.cs`) mapeia
`Product.Category` para IDs de categoria do YouTube, mas só conhece as 5 categorias antigas —
as 4 novas (Eletrodomésticos, Climatização, Ferramentas, e a subdivisão de Beleza) caem no
fallback `DefaultCategoryId = "22"`. Comportamento gracioso (sem erro), não coberto pelos CA
desta issue; mencionar ao LT/PM se a granularidade do vídeo do YouTube importar no futuro.

## Dev #170 — backend-api-filtros (CONCLUÍDO — merged em desenv)
Branch `feature/ISSUE-170-api-filtros` (worktree, base `desenv` atualizada com #168) → PR #173
para `desenv`, squash merge (commit `03c7a05`), branch remota deletada, sub-issue #170 fechada.
`dotnet test`: 395/395 passando, sem regressão (380 pré-existentes + 15 novos casos desta
sub-issue). Escopo:
- `PublicDealDto`: `Platform` removido (CA 5.1); `Subcategory` adicionado. DTO interno/dashboard
  (`ProductDtos.cs`) não tocado (CA 5.2/5.3 confirmados por teste, incluindo o de `GetBySlug`).
- `PublicController.GetDeals`: filtros `category`/`subcategory`/`minPrice`/`maxPrice`/
  `minDiscount`/`sort` (`price_asc`/`discount_desc`/`recent`, default `AiScore desc`), todos
  opcionais/combináveis (CA 6.1-6.6), seguindo a ordem dos 5 índices compostos de #168.
- Novo `GET /api/public/categories`: árvore `Category > [Subcategory]` com contagem (CA 6.7).
- `GetByCategory` (`/deals/category/{categoria}`) **removida nesta sub-issue** (decisão final do
  Dev, documentada no PR #173: segue design.md §5.2 sem reabrir a decisão — a remoção só afeta
  `desenv`, e o release `homolog→main` já está condicionado a esperar a Sub-D/#171 também pronta
  por `tasks.md`, então não há janela de produção quebrada). LT concordou com a decisão no merge.
- Validação Docker: stack subida (build+boot limpo, `/health` 200, sem exceção), produtos reais
  populados via SQL, endpoints exercitados via `curl` (filtros combinados, `minDiscount`+sort,
  categoria inexistente → 200 vazio, rota antiga → 404, árvore de categorias com contagem
  correta, ausência de `platform` confirmada em 100% do JSON). Ambiente removido ao final
  (`docker compose down -v` + imagem + `.env`/override locais apagados).

## Dev #169 — backend-ia-orcamento (CONCLUÍDO — merged em desenv)
Branch `feature/ISSUE-169-ia-orcamento` (worktree, base `desenv` já com #168) → PR #174 para
`desenv`, squash merge (commit `03cb40e`), branch remota deletada, sub-issue #169 fechada.
`dotnet test`: 402/402 passando, sem regressão (383 pré-existentes de #168 + 19 novos casos desta
sub-issue: 6 em `ClaudeAiServiceTests` `ClassifyCategoryAsync`, 8 unitários + 3 de integração real
em `ClaudeBudgetServiceTests`/`ClaudeBudgetServiceIntegrationTests`, 5 em `ProcessorJobTests`).
Escopo:
- `IAnthropicClientWrapper.CompleteAsync`: passou de `Task<string>` para
  `Task<ClaudeCompletionResult>` (`Text`/`InputTokens`/`OutputTokens`, usando
  `MessageResponse.Usage` real do Anthropic.SDK, sem estimativa manual).
  `ClaudeAiService.ScoreProductAsync`/`GenerateCaptionAsync` só trocaram `response` por
  `response.Text` (CA 3.4 — nenhuma lógica de orçamento nova nesses dois métodos).
- `IClaudeBudgetService`/`ClaudeBudgetService` (novo, Infrastructure):
  `IsCategorizationBudgetAvailableAsync` (leitura simples, reset lazy mensal — CA 4.5) +
  `RecordUsageAsync` (só debita após sucesso — CA 4.2). Escrita real (Postgres/Npgsql) via
  `UPDATE app_settings SET value = CASE ... END` atômico (`ExecuteSqlInterpolatedAsync`, fora
  do change tracker do EF — design.md §3.5); caminho de fallback não-atômico só para o provider
  InMemory dos testes unitários (nunca exercitado em produção). Chaves confirmadas na migration
  já mergeada por #168: `claude.monthly_usage`, `claude.monthly_budget_limit_brl` (default 30),
  `claude.price_input_usd_per_mtok`/`claude.price_output_usd_per_mtok`/`claude.usd_brl_rate`
  (defaults 1/5/5.5, "soft guard" — Gerente/DevOps confirma valores reais antes do deploy).
- `IAiService.ClassifyCategoryAsync` (novo) + `ClaudeAiService.ClassifyCategoryAsync`: checa
  orçamento primeiro (CA 4.3 — sem chamar a API se estourado), monta prompt reaproveitando
  `CategoryDetector.Categorias` (nova propriedade pública exposta em Domain, evita duplicar a
  taxonomia), parseia `{category, subcategory}` da resposta, debita orçamento só em sucesso.
  Erro/timeout/resposta não-parseável → `null`, sem debitar (CA 4.2).
- `ProcessorJob.EnsureCategoryFallbackAsync` (novo): só chama `ClassifyCategoryAsync` quando
  `Category == "Geral"` (CA 3.3); `Status == Queued` já garantido pela query do topo (CA 3.2).
  Reordenado para rodar **antes** de `EnsureSlug` (CA 3.1). Logging adicionado (`ILogger` já
  injetado no job) para observabilidade do fallback disparando/resultado.
- `Program.cs`: registra `IClaudeBudgetService` e passa para `ClaudeAiService` via DI.
- Testes de integração real (Testcontainers.PostgreSql, novo pacote em `AfiliadoBot.Tests`):
  `ClaudeBudgetServiceIntegrationTests` sobe Postgres real, roda as migrations reais do projeto e
  valida (a) 20 chamadas concorrentes de `RecordUsageAsync` somam exatamente o esperado (prova
  que o `UPDATE...CASE` é atômico, sem lost-update), (b) reset lazy contra Postgres real, (c)
  `IsCategorizationBudgetAvailableAsync` retorna `false` após ultrapassar o limite.
- Validação Docker (`docker compose up db api`, sem website/dashboard — fora do escopo desta
  sub-issue): app sobe sem exceção (DI do novo `IClaudeBudgetService`/`ClaudeAiService`
  resolvida OK); produto `Queued`/`Geral` inserido via SQL + processor disparado via
  `/api/jobs/processor/trigger` (login via usuário seedado) → log confirma
  "acionando fallback de categorização via IA" e "fallback não classificou ... permanece Geral"
  (esperado — sem `Claude:ApiKey` real neste ambiente, a chamada HTTP falha e é capturada,
  mesma postura de erro do scoring/legenda); `claude.monthly_usage` permaneceu em 0 (confirma
  que só chamada bem-sucedida debita). Segundo cenário: `claude.monthly_usage` sobrescrito via
  SQL para acima do limite (999 > 30) + novo produto `Queued`/`Geral` → mesmo resultado
  (permanece "Geral"), confirmando o caminho de orçamento estourado (CA 4.3). Ambiente removido
  ao final (`docker compose down -v` + imagem + `.env` local apagados).

**Revisão do LT no merge**: diff do PR #174 conferido — contrato de tokens reais
(`ClaudeCompletionResult`/`IAnthropicClientWrapper`), `IClaudeBudgetService` com UPDATE atômico,
`ClassifyCategoryAsync` no `ClaudeAiService` e a reordenação em `ProcessorJob` batem com o escopo
descrito no PR e no `tasks.md`. Sem achados de infra adicionais nesta sub-issue (Ids de
`app_settings` já reservados por #168, sem colisão).

## Dev #171 — frontend-filtros (CONCLUÍDO — merged em desenv)
Branch `feature/ISSUE-171-frontend-filtros` (worktree, base `desenv` já com #168/#169/#170) → PR
#175 para `desenv`, squash merge (commit `0142d86`), branch remota deletada, sub-issue #171 fechada.
`npm test`: 102/102 passando (100%), cobertura
statements 94%/branches 90.4%/functions 90.1%/lines 96.6% (≥80% em todos os eixos). `npm run
build`: sem erros/warnings. Escopo:
- `website/lib/api.ts`: `fetchDeals` migrado para aceitar `filters?: DealFilters`
  (`category`/`subcategory`/`minPrice`/`maxPrice`/`minDiscount`/`sort`), espelhando os
  `[FromQuery]` reais de `PublicController.GetDeals` (confirmados lendo o código, não a
  especificação técnica — sem divergência desta vez). Novo `fetchCategories()` para
  `GET /api/public/categories` (formato real confirmado em `CategoryTreeDto.cs`:
  `category`/`count`/`subcategories[{subcategory,count}]`). `fetchByCategory` removida.
- **Achado fora do que a especificação técnica havia mapeado**: `fetchByCategory` tinha um 2º
  call site não documentado — `lib/related-deals.ts` (usado por `app/oferta/[slug]/page.tsx` para
  "Mais ofertas"), além do já esperado `app/categoria/[categoria]/page.tsx`. Encontrado só ao
  rodar o Gate obrigatório de busca de testes afetados (passo g do processo) — `related-deals.ts`
  migrado para `fetchDeals(1, N, { category })` também, com `related-deals.test.ts` e
  `app/oferta/[slug]/page.test.tsx` ajustados.
- `website/lib/types.ts`: `Deal.platform` removido, `Deal.subcategory?` adicionado, novo tipo
  `CategoryTree`. `platform:` removido de todos os `buildDeal()`/mocks de teste do repo (7
  arquivos além dos diretamente alterados).
- `website/components/Header.tsx`: chips de plataforma removidos (CA 7.4).
- Novo `website/components/FilterBar.tsx` + `app/styles/filter-bar.css`, seguindo
  `ux-ui-spec-filterbar.md` à risca (classes BEM, tokens, estados). Decisão de implementação não
  prescrita pela spec: em vez de duplicar a marcação dos controles entre `.filter-bar__row`
  (desktop) e `.filter-bar__summary`+`.filter-bar__drawer` (mobile) e alternar via CSS
  `display:none` por media query (como a spec sugere), optei por um hook `useIsDesktop()`
  (`window.matchMedia`) que renderiza só UM dos dois layouts no DOM por vez — evita elementos
  duplicados com o mesmo papel/nome acessível (`role=combobox` "Categoria" apareceria 2x),
  problema real que apareceria tanto em testes quanto para leitores de tela. CSS mantido fiel à
  spec (inclusive as regras de media query, como fallback/documentação), mas a alternância real é
  via JS.
- `website/app/page.tsx` (Home): integra `FilterBar`, conecta filtros da URL a `fetchDeals`,
  estado vazio orientado a filtro com CTA "Ver todas as ofertas" (CA 7.5).
- `website/app/categoria/[categoria]/page.tsx`: migrado para `fetchDeals` com filtro `category`,
  URL pública inalterada.
- **2 bugs reais encontrados só na validação em browser real** (Playwright contra o site rodando
  em Docker, viewport mobile e desktop) — nenhum dos dois apareceu nos testes Jest/jsdom:
  1. Botão "Filtros" tinha um ícone via CSS `::before` (`content: "☰"`); browsers reais incluem
     conteúdo de pseudo-elemento no cálculo do nome acessível (jsdom não aplica CSS nenhum,
     então o teste Jest nunca via isso) — corrigido com `aria-label="Filtros"` explícito no botão.
  2. O FAB de reabertura usava `getBoundingClientRect()` de um elemento `position: sticky`, que
     por definição nunca "sai" da viewport enquanto grudado — o FAB nunca apareceria de verdade
     ao rolar. Trocado por um threshold simples de `window.scrollY`, com teste Jest novo cobrindo
     o comportamento (`fireEvent.scroll` + `Object.defineProperty(window, 'scrollY', ...)`).
- Validação Docker real (`db`+`api`+`website`, `.env`/`docker-compose.override.yml` locais
  temporários, removidos ao final): 5 produtos de categorias/preços/descontos variados via SQL
  direto no Postgres do container. Filtros exercitados via `curl` contra a API real
  (`category`+`subcategory` combinados, `minPrice`/`maxPrice`+`minDiscount`, `sort=price_asc`,
  categoria inexistente → 200 vazio, `GET /api/public/categories` com contagem correta, ausência
  de `platform` em 100% do JSON) e contra o HTML SSR do `website` real (`curl` na Home com
  querystring de filtros, categoria via `/categoria/{categoria}`, confirmando grid filtrado e
  ausência de qualquer chip/texto de plataforma no Header renderizado).
- Playwright (`test:visual`) estendido: 2 novos testes em `e2e/visual.spec.ts` cobrindo o
  `FilterBar` em mobile (resumo compacto + abrir o drawer, viewport padrão do projeto
  `mobile-chromium`) e desktop (`page.setViewportSize({width:1280,...})`, os 5 controles em linha
  única, sem drawer/summary). Screenshots gerados em
  `documentacoes/ISSUE-167-categorizacao-unificada/screenshots-filterbar/` (não commitados —
  artefato de validação, ambos os layouts inspecionados visualmente, CSS aplicado corretamente
  conforme a spec, sem quebra de layout).
- Ambiente Docker completamente removido ao final (`docker compose down -v`, imagens `api`/
  `website` locais, `.env` e `docker-compose.override.yml` apagados).

## Próximos passos
1. ~~LT faz o merge de #168 (PR #172) em `desenv`.~~ **Concluído.**
2. ~~Dev de #170 (backend-api-filtros).~~ **Concluído.**
3. ~~LT faz o merge de #170 (PR #173) em `desenv`.~~ **Concluído.**
4. ~~Dev de #169 (backend-ia-orcamento).~~ **Concluído.**
5. ~~LT faz o merge de #169 (PR #174) em `desenv`.~~ **Concluído — commit `03cb40e`, sub-issue
   #169 fechada.**
6. ~~Dev de #171 (frontend-filtros).~~ **Concluído — PR #175 mergeado em `desenv`.**
7. ~~LT faz o merge de #171 (PR #175) em `desenv`. Com as 4 sub-issues em `desenv_tasks_merged`, LT
   cria o PR `desenv→homolog`.~~ **Concluído — commit `0142d86`, sub-issue #171 fechada. PR de
   homologação #176 (`desenv→homolog`, merge commit) criado, satisfazendo design.md §5.2
   (#170+#171 no mesmo deploy).**
8. Sessão principal roda `/code-review` no PR #176 + spawna o agente Code Review.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 49906 | 22 | 158s |
| 2 | PM Fase 1 (validação técnica + levantamento, Gate 1) | PM | Sonnet | 60461 | 28 | 240s |
| 3 | PM Fase 2 (PRD + critérios de aceite) | PM | Sonnet | 51527 | 16 | 225s |
| 4 | Arquiteto (3 decisões técnicas + achado de dependência circular) | Arquiteto | Sonnet | 110481 | 49 | 673s |
| 5 | Líder Técnico (especificação técnica consolidada) | Líder Técnico | Sonnet | 100393 | 32 | 288s |
| 6 | Líder Técnico (task breakdown + 4 sub-issues, retomada rota normal) | Líder Técnico | Sonnet | 80395 | 23 | 220s |
| 7 | UX/UI (spec visual FilterBar, Sub-D) | UX/UI | Sonnet | 96003 | 10 | 279s |
| 8 | Dev #168 (backend-schema-collectors, migration + CategoryDetector + 3 collectors) | Dev .NET | Sonnet | 234329 | 134 | 1326s |
| 9 | Líder Técnico (merge PR #172 → desenv, fechamento #168) | Líder Técnico | Sonnet | 57329 | 17 | 138s |
| 10 | Dev #170 (backend-api-filtros, GetDeals+categories, remove Platform/GetByCategory) | Dev .NET | Sonnet | 153125 | 77 | 790s |
| 11 | Dev #169 (backend-ia-orcamento, ClaudeBudgetService + fallback IA + tokens reais) | Dev .NET | Sonnet | 234090 | 125 | 1158s |
| 12 | Líder Técnico (merge PR #173 → desenv, fechamento #170) | Líder Técnico | Sonnet | 66817 | 14 | 139s |
| 13 | Líder Técnico (merge PR #174 → desenv, fechamento #169) | Líder Técnico | Sonnet | 55626 | 12 | 163s |
| 14 | Dev #171 (frontend-filtros, FilterBar + migração api.ts/types.ts/Header.tsx, PR #175) | Dev Node.js | Sonnet | 298601 | 184 | 2417s |
| 15 | Líder Técnico (merge PR #175 → desenv, fechamento #171; PR homologação #176 desenv→homolog) | Líder Técnico | Sonnet | 57148 | 22 | 177s |

**Total acumulado:** 1.706.231 tokens · ~144 min proc.

---
_Mantido pela sessão principal. Última atualização: 2026-08-14 (recuperação de conteúdo perdido —
o LT sobrescreveu o arquivo inteiro na invocação de merge de #170; reconstruído a partir do commit
`1cc3003` + a atualização real feita pelo LT)._
