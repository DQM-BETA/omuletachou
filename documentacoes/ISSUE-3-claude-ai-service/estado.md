issue: 3
titulo: feat: Claude AI Service (Scoring + Geracao de Legendas)
rota: normal
etapa_atual: Dev — aguardando spawn
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-3-claude-ai-service
openspec_path: repos/omuletachou/openspec/changes/ISSUE-3-claude-ai-service
openspec_change: ~
tech_stacks:
  - .NET 8
  - Anthropic SDK
  - claude-haiku-4-5-20251001
ultimo_agente: lt
sub_issues:
  - "#33 (stack:dotnet, task_id:T-01)"
desenv_tasks_merged: []
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
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

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
