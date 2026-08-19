---
issue: 155
titulo: "chore: Configurar Playwright (test:visual) no dashboard — Gate Visual do QA nunca dispara"
rota: rapido
etapa_atual: "Aguardando LT (refinamento)"
ultimo_agente: coordenador
openspec_change: ~
tech_stacks:
  - Angular
  - Playwright
repos:
  omuletachou: dashboard
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-155-playwright-dashboard
openspec_path: ~
sub_issues: []
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
Configurar Playwright (`test:visual` script no `package.json`, config básica, screenshots por rota principal) no `dashboard` (Angular), seguindo o padrão já usado com sucesso em `dqm-digital-app`.

## Referências
- `.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md`
- Issue #154 (achado original)
- Padrão de implementação: `dqm-digital-app` (já tem Playwright configurado)

## Rota
`rapido` — mudança simples, padrão já provado, sem ambiguidade de requisito. Pula PM Fase 1, Arquiteto, UX/UI, Gate 1; mantém Dev+testes, CR leve, QA, Gate 2.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | — | — | — |

_Atualizado: 2026-08-19_
