---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Em Desenvolvimento — #168 mergeada em desenv; Dev #170 concluído (PR #173 aberto, aguardando merge do LT); Dev #169 em paralelo
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
  - "#169 (backend-ia-orcamento, stack:dotnet, task_id:Sub-B) — desbloqueada (#168 já em desenv)"
  - "#170 (backend-api-filtros, stack:dotnet, task_id:Sub-C) — desbloqueada (#168 já em desenv); paralelo a #169"
  - "#171 (frontend-filtros, stack:nodejs, task_id:Sub-D) — depende de #170 para contrato final; UX/UI concluído, spec disponível; pode iniciar api.ts/types.ts/Header.tsx em paralelo"
desenv_tasks_merged: ["#168"]
sub_issue_168_pr: "#172 (feature/ISSUE-168-schema-collectors -> desenv, MERGED squash, commit fc083f3; branch remota deletada; sub-issue #168 fechada)"
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
Demanda retomada pelo Gerente em rota `normal` ("pode seguir na rota normal") — toda a documentação
(PRD, critérios de aceite, design do Arquiteto, especificação técnica) já estava pronta da rodada
`backlog` anterior. **LT concluiu o task breakdown final nesta invocação**: `tasks.md` escrito e 4
sub-issues criadas no GitHub, revisando (e mantendo, sem alterações estruturais) a sugestão de
fatiamento já registrada em `especificacao-tecnica.md`.

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

## Decisão UX/UI (confirmada nesta invocação)
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
Branch `feature/ISSUE-168-schema-collectors` → PR #172 para `desenv`, **squash merge nesta
invocação do LT** (commit `fc083f3`), branch remota e local deletadas, sub-issue #168 fechada
com comentário de resumo. `dotnet test`: 383/383 passando. Stack Docker validada (build+boot
limpo, migration aplicada contra Postgres real, ambiente removido ao final).

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
repetido. Próxima sub-issue que semear `app_settings` (`claude.price_*`/`claude.usd_brl_rate` já
cobertos aqui) deve repetir a checagem "rodar a migration contra Postgres real", não só ler o
`HasData` do model.

**Também fora de escopo (observação, sem ação necessária)**: `YoutubePublisher.CategoryMap`
(`backend/src/AfiliadoBot.Infrastructure/Integrations/Social/YoutubePublisher.cs`) mapeia
`Product.Category` para IDs de categoria do YouTube, mas só conhece as 5 categorias antigas —
as 4 novas (Eletrodomésticos, Climatização, Ferramentas, e a subdivisão de Beleza) caem no
fallback `DefaultCategoryId = "22"`. Comportamento gracioso (sem erro), não coberto pelos CA
desta issue; mencionar ao LT/PM se a granularidade do vídeo do YouTube importar no futuro.

## Dev #170 — backend-api-filtros (CONCLUÍDO — aguardando merge do LT)
Branch `feature/ISSUE-170-api-filtros` (worktree, base `desenv` atualizada com #168) → PR #173
para `desenv` (NÃO mergeado por este Dev). `dotnet test`: 395/395 passando, sem regressão (380
pré-existentes + 15 novos casos desta sub-issue). Escopo:
- `PublicDealDto`: `Platform` removido (CA 5.1); `Subcategory` adicionado. DTO interno/dashboard
  (`ProductDtos.cs`) não tocado (CA 5.2/5.3 confirmados por teste, incluindo o de `GetBySlug`).
- `PublicController.GetDeals`: filtros `category`/`subcategory`/`minPrice`/`maxPrice`/
  `minDiscount`/`sort` (`price_asc`/`discount_desc`/`recent`, default `AiScore desc`), todos
  opcionais/combináveis (CA 6.1-6.6), seguindo a ordem dos 5 índices compostos de #168.
- Novo `GET /api/public/categories`: árvore `Category > [Subcategory]` com contagem (CA 6.7).
- `GetByCategory` (`/deals/category/{categoria}`) **removida nesta sub-issue** (decisão final do
  Dev, documentada no PR #173: segue design.md §5.2 sem reabrir a decisão — a remoção só afeta
  `desenv`, e o release `homolog→main` já está condicionado a esperar a Sub-D/#171 também pronta
  por `tasks.md`, então não há janela de produção quebrada).
- Validação Docker: stack subida (build+boot limpo, `/health` 200, sem exceção), produtos reais
  populados via SQL, endpoints exercitados via `curl` (filtros combinados, `minDiscount`+sort,
  categoria inexistente → 200 vazio, rota antiga → 404, árvore de categorias com contagem
  correta, ausência de `platform` confirmada em 100% do JSON). Ambiente removido ao final
  (`docker compose down -v` + imagem + `.env`/override locais apagados).

## Próximos passos
1. ~~LT faz o merge de #168 (PR #172) em `desenv`.~~ **Concluído.**
2. ~~Dev de #170 (backend-api-filtros).~~ **Concluído — PR #173 aberto, aguardando merge do LT.**
3. Dev de #169 (backend-ia-orcamento) **em andamento em paralelo** (branch
   `feature/ISSUE-169-ia-orcamento` já existe como worktree).
4. LT faz o merge de #170 (PR #173) em `desenv` (uma invocação por merge, sequencial — não
   simultâneo ao merge de #169 se ambos chegarem prontos ao mesmo tempo).
5. Após #170 mergeada: Dev de #171 integra o `FilterBar` ao contrato final da API.
6. Quando as 4 sub-issues estiverem em `desenv_tasks_merged`, LT cria o PR `desenv→homolog`.

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

**Total acumulado:** 993.949 tokens · ~76 min proc.

