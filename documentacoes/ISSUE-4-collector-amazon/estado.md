---
issue: 4
titulo: feat: Collector Amazon (PAAPI v5 + Scoring automatico)
rota: normal
etapa_atual: Gate 2 — aguardando aprovacao Gerente
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-4-collector-amazon
openspec_path: repos/omuletachou/openspec/changes/ISSUE-4-collector-amazon
openspec_change: ~
tech_stacks:
  - .NET 8
  - HttpClient
  - AWS Signature V4
  - PAAPI v5
ultimo_agente: lt
sub_issues: ["#37 (stack:dotnet, task_id:T-01)"]
desenv_tasks_merged: ["#37"]
pr_homologacao: 39
pr_release: 40
code_review_homolog_pr: 39
qa_status: aprovado
blockers: nenhum
---

## Contexto
- Dependência: Issues #2 (Domain/EF Core) e #3 (ClaudeAiService)
- Objetivo: coletar produtos da Amazon PAAPI v5 e aplicar scoring automático via IAiService
- Stack: .NET 8 + HttpClient (sem SDK AWS) + AWS Signature V4 manual
- Atenção: PAAPI exige 1 venda nos primeiros 180 dias ou acesso é revogado
- Sem ambiguidade arquitetural residual após Gate 1: chamada a `IAiService` segue via injeção direta no `AmazonCollector`, sem necessidade de orquestrador externo
- Task breakdown: task única (T-01) — escopo coeso, uma stack só (dotnet)

## Histórico de Etapas
Criada em 2026-07-06 pelo Coordenador.

| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparação Issue | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |
| 3 | PM Fase 2 | pm | concluido |
| 4 | Refinamento LT | LT | concluido |
| 5 | Sincronizar board | Coordenador | concluido |
| 6 | Dev T-01 #37 | dev-dotnet | concluido |
| 7 | Merge T-01 + PR homolog | LT | concluido |
| 8 | Code Review PR #39 | code-review | concluido — 32/32 testes, build ok, merge homolog realizado |
| 9 | QA homolog | qa | concluido — CAs ok |
| 10 | PR release homolog→main | LT | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
| 2 | PM Fase 1 | pm | sonnet | 32184 | 14 | 128s |
| 3 | PM Fase 2 | pm | sonnet | 38197 | 11 | 123s |
| 4 | Refinamento LT | lt | sonnet | 46787 | 15 | 102s |
| 5 | Sincronizar board | coordenador | haiku | 48367 | 43 | 268s |
| 6 | Dev T-01 #37 | dev-dotnet | sonnet | 90641 | 79 | 405s |
| 7 | Merge T-01 + PR homolog | lt | sonnet | 31470 | 8 | 81s |
| 8 | Code Review PR #39 | code-review | sonnet | 75064 | 24 | 244s |
| 9 | QA homolog | qa | sonnet | 61693 | 32 | 462s |
| 10 | PR release homolog→main | lt | sonnet | 31662 | 7 | 66s |
