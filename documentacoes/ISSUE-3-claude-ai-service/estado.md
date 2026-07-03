issue: 3
titulo: feat: Claude AI Service (Scoring + Geracao de Legendas)
rota: normal
etapa_atual: QA
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-3-claude-ai-service
openspec_path: repos/omuletachou/openspec/changes/ISSUE-3-claude-ai-service
openspec_change: ~
tech_stacks:
  - .NET 8
  - Anthropic SDK
  - claude-haiku-4-5-20251001
ultimo_agente: code-review
sub_issues:
  - "#33 (stack:dotnet, task_id:T-01)"
desenv_tasks_merged: ["#33"]
pr_homologacao: 35
pr_release: ~
code_review_homolog_pr: 35
qa_status: ~
blockers: nenhum

## Contexto
- Dependência: Issue #2 (Domain, EF Core, Schema)
- Objetivo: implementar serviço de IA que avalia produtos (scoring) e gera legendas personalizadas por rede social
- Custo estimado: ~R$ 10/mês para 3.000 chamadas/mês
- Stack: .NET 8 + Anthropic.SDK + claude-haiku-4-5-20251001
- Decisões consolidadas (PM Fase 2): dois métodos em IAiService (ScoreProductAsync + GenerateCaptionAsync), persona Mulet com regras por rede, criterios de scoring obrigatorios, fallback de scoring via min_score_fallback, fallback de legenda via template fixo

## Histórico de Etapas
Criada em 2026-07-03 pelo Coordenador.

| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | PM Fase 1 | PM | concluido |
| 2 | PM Fase 2 | PM | concluido |
| 3 | Refinamento LT | LT | concluido |
| 4 | Dev T-01 #33 | dev-dotnet | concluido |
| 5 | Merge T-01 + PR homolog | LT | concluido |
| 6 | Code Review PR #35 | code-review | concluido — 25/25 testes, build ok, merge homolog aprovado |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
| 4 | Dev T-01 #33 | dev-dotnet | sonnet | 51135 | 59 | 366s |
| 5 | Merge T-01 + PR homolog | lt | sonnet | 23927 | 10 | 85s |
| 6 | Code Review PR #35 | code-review | sonnet | - | - | - |
