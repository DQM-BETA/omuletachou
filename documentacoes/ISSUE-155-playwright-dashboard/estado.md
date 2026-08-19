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
- #232 — stack:angular, task_id:T-01 — Configurar Playwright (test:visual) no dashboard

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | — | — | — |
| 2 | Refinamento (rapido) — spec técnica + sub-issue #232 | LT | Sonnet | — | — | — |

_Atualizado: 2026-08-19_
