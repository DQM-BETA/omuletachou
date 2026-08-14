---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Em Desenvolvimento — UX/UI (Sub-D) concluído, aguardando Dev(s) backend/frontend
ultimo_agente: ux-ui
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
  - "#168 (backend-schema-collectors, stack:dotnet, task_id:Sub-A) — bloqueante, sem dependências, pode iniciar já"
  - "#169 (backend-ia-orcamento, stack:dotnet, task_id:Sub-B) — depende de #168 mergeada em desenv"
  - "#170 (backend-api-filtros, stack:dotnet, task_id:Sub-C) — depende de #168 mergeada em desenv; paralelo a #169"
  - "#171 (frontend-filtros, stack:nodejs, task_id:Sub-D) — depende de #170 para contrato final; UX/UI concluído, spec disponível; pode iniciar api.ts/types.ts/Header.tsx em paralelo"
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
- `documentacoes/ISSUE-167-categorizacao-unificada/ux-ui-spec-filterbar.md` — **novo (esta
  invocação)**: spec visual completa do `FilterBar` (layout desktop/mobile, classes BEM, tokens,
  estados, heurísticas de Nielsen, fluxo de navegação).

## Próximos passos
1. Sessão principal spawna o **Dev de #168** (backend-schema-collectors — sem dependências, pode
   começar imediatamente) e, quando pronto, o **Dev de #171** (frontend-filtros — spec de UX/UI já
   disponível, pode iniciar `api.ts`/`types.ts`/`Header.tsx`/`FilterBar` em paralelo, contra mocks
   se #170 ainda não estiver pronta).
2. Após #168 mergeada em `desenv`: Dev(s) de #169 e #170 em paralelo.
3. Após #170 mergeada: Dev de #171 integra o `FilterBar` ao contrato final da API.
4. LT faz o merge de cada sub-issue conforme os Devs concluem (uma invocação por merge, sequencial).
   Quando as 4 estiverem em `desenv_tasks_merged`, LT cria o PR `desenv→homolog`.

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

**Total acumulado (planejamento, antes dos devs):** 549.166 tokens · ~39 min proc.
