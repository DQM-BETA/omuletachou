---
issue: 155
titulo: "chore: Configurar Playwright (test:visual) no dashboard — Gate Visual do QA nunca dispara"
rota: rapido
etapa_atual: "Em Desenvolvimento"
ultimo_agente: lt
openspec_change: ~
tech_stacks:
  - Angular
  - Playwright
repos:
  omuletachou: dashboard
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-155-playwright-dashboard
openspec_path: ~
sub_issues: ["#232 (stack:angular, task_id:T-01)"]
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: IC_kwDOTMlfyM8AAAABPnxeBw
---

## Escopo
Configurar Playwright (`test:visual` script no `package.json`, config básica, screenshots por rota principal) no `dashboard` (Angular), seguindo o padrão já usado com sucesso em `website` (Issue #154/#156, mesmo repo).

## Referências
- `.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md`
- Issue #154 (achado original) / #156 (implementação de referência no `website`)
- Padrão de implementação: `website/playwright.config.ts`, `website/e2e/visual.spec.ts`, `website/e2e/helpers.ts`

## Rota
`rapido` — mudança simples, padrão já provado no mesmo repo (`website`), sem ambiguidade de requisito. Pulou PM Fase 1, Arquiteto, UX/UI, Gate 1; mantém Dev+testes, CR leve, QA, Gate 2.

## Refinamento (LT)
Especificação técnica completa em `especificacao-tecnica.md` (mesmo diretório). Task breakdown mínimo: 1 sub-issue de stack `angular` cobrindo toda a config (script, playwright.config.ts, .gitignore, testes de screenshot para /login e /products, nota sobre auth via sessionStorage sem depender de subir a API).

## Sub-issues
- #232 — stack:angular, task_id:T-01 — Configurar Playwright (test:visual) no dashboard — **feito, PR #233 (feature→desenv)**

## Dev (sub-issue #232)
- Branch `feature/ISSUE-232-playwright-dashboard` (worktree), PR #233: https://github.com/DQM-BETA/omuletachou/pull/233
- `@playwright/test@^1.62.1` instalado em `dashboard/`; script `test:visual`; `dashboard/playwright.config.ts` (mesmo padrão de `website/`, projeto `chromium` desktop); `dashboard/.gitignore` atualizado (`/screenshots`, `/playwright-report`, `/test-results`); `dashboard/e2e/visual.spec.ts` (8 specs: `/login` + 7 rotas autenticadas) + `dashboard/e2e/helpers.ts`.
- **Achado durante a implementação (ajuste sobre a spec técnica):** a Nota de autenticação da spec assumia que chamadas de API com token dummy falhariam "silenciosamente". Na prática, se a API .NET estiver de fato no ar localmente, ela responde 401 real ao token dummy inválido, e `authInterceptor` trata qualquer 401 fora de `/api/auth/login` como sessão expirada — disparando logout + redirect para `/login`, quebrando o screenshot da rota autenticada. Solução: `blockApiCalls` (`e2e/helpers.ts`) aborta as chamadas `/api/**` via `page.route`, tornando o teste determinístico independente do estado da API local.
- **CA-5 (documentar em `dashboard/CLAUDE.md`/`CLAUDE.md` do repo):** não realizável como especificado — edição/criação de qualquer arquivo `CLAUDE.md` é bloqueada por permissão de ferramenta (trava dura), independente do path. Documentado em `dashboard/README.md` (seção "Running visual tests") como alternativa equivalente.
- Testes: `npm test` (Karma) 140/140 passando (baseline igual, sem regressão). `npm run test:visual` (Playwright real) 8/8 passando — screenshots gerados e inspecionados visualmente (Material Design aplicado, sidenav/tabelas/formulários estilizados, estados de erro com feedback visível, sem CSS quebrado).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | — | — | — |
| 2 | Refinamento (rapido) — spec técnica + sub-issue #232 | LT | Sonnet | — | — | — |
| 3 | Dev (sub-issue #232, PR #233) | Dev Angular | Sonnet | — | — | — |

_Atualizado: 2026-08-19_
