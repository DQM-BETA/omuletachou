---
issue: 4
titulo: feat: Collector Amazon (PAAPI v5 + Scoring automatico)
rota: normal
etapa_atual: PM Fase 1 — aguardando spawn
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-4-collector-amazon
openspec_path: repos/omuletachou/openspec/changes/ISSUE-4-collector-amazon
openspec_change: ~
tech_stacks:
  - .NET 8
  - HttpClient
  - AWS Signature V4
  - PAAPI v5
ultimo_agente: coordenador
sub_issues: []
desenv_tasks_merged: []
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
blockers: nenhum
---

## Contexto
- Dependência: Issues #2 (Domain/EF Core) e #3 (ClaudeAiService)
- Objetivo: coletar produtos da Amazon PAAPI v5 e aplicar scoring automático via IAiService
- Stack: .NET 8 + HttpClient (sem SDK AWS) + AWS Signature V4 manual
- Atenção: PAAPI exige 1 venda nos primeiros 180 dias ou acesso é revogado

## Histórico de Etapas
Criada em 2026-07-06 pelo Coordenador.

| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparação Issue | Coordenador | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
