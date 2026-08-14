---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Em Desenvolvimento — task breakdown concluído, aguardando UX/UI (Sub-D) e Dev(s) backend (Sub-A) em paralelo
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
sub_issues:
  - "#168 (backend-schema-collectors, stack:dotnet, task_id:Sub-A) — bloqueante, sem dependências, pode iniciar já"
  - "#169 (backend-ia-orcamento, stack:dotnet, task_id:Sub-B) — depende de #168 mergeada em desenv"
  - "#170 (backend-api-filtros, stack:dotnet, task_id:Sub-C) — depende de #168 mergeada em desenv; paralelo a #169"
  - "#171 (frontend-filtros, stack:nodejs, task_id:Sub-D) — depende de #170 para contrato final; aguardar mockup UX/UI antes do FilterBar; pode iniciar api.ts/types.ts/Header.tsx em paralelo com UX/UI"
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

## Documentação produzida (definitivo, dentro do repo)
- `openspec/changes/issue-167-categorizacao-unificada/proposal.md` — PRD.
- `documentacoes/ISSUE-167-categorizacao-unificada/criterios-aceite.md` — 27 cenários Given/When/Then.
- `openspec/changes/issue-167-categorizacao-unificada/design.md` — design técnico do Arquiteto.
- `documentacoes/ISSUE-167-categorizacao-unificada/especificacao-tecnica.md` — plano técnico
  executável completo.
- `openspec/changes/issue-167-categorizacao-unificada/tasks.md` — **novo (esta invocação)**: task
  breakdown final com as 4 sub-tarefas (critérios de aceite + contexto técnico + ordem de
  dependência), referenciado pelas 4 sub-issues.

## Próximos passos
1. Sessão principal spawna **UX/UI** (mockup do `FilterBar`, ver decisão acima) e, em paralelo, o
   **Dev de #168** (backend-schema-collectors — sem dependências, pode começar imediatamente).
2. Após #168 mergeada em `desenv`: Dev(s) de #169 e #170 em paralelo.
3. Após UX/UI concluído e #170 mergeada: Dev de #171.
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
| 6 | Líder Técnico (task breakdown + 4 sub-issues, retomada rota normal) | Líder Técnico | Sonnet | TBD | TBD | TBD |

**Total acumulado (backlog, sem dev):** 372.768 tokens · ~24 min proc. (linha 6 a ser preenchida pela
sessão principal com o `<usage>` deste HANDOFF).
