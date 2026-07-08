# Estado — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## Campos principais
issue: 7
repo: omuletachou
titulo: feat: Publisher Telegram + Hangfire Scheduler
rota: normal
etapa_atual: PM Fase 1 — aguardando spawn
docs_path: repos/omuletachou/documentacoes/ISSUE-7-publisher-telegram
openspec_path: repos/omuletachou/openspec/changes/ISSUE-7-publisher-telegram
ultimo_agente: coordenador
status_comment_id: 4913934382

## Contexto
Stack: .NET 8, Hangfire.PostgreSql, Telegram Bot API
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #4/#5 (collectors), #6 (ProcessorJob)

### Ambiguidade identificada (sinalizada ao PM Fase 1)
A Issue #7 menciona `RecurringJob.AddOrUpdate<CollectorJob>` no Hangfire, mas a classe `CollectorJob` **não existe** no código atualmente (confirmado via busca). A Issue #5 deixou explicitamente a criação do `CollectorJob` orquestrador para "issue futura de Scheduler", que é esta Issue #7.

**Esclarecimento necessário:**
1. O `CollectorJob` deve ser criado nesta Issue (orquestrando os collectors Amazon/ML/Shopee em sequência, conforme decidido na Issue #5)?
2. Deve o `CollectorJob` também encadear o `ProcessorJob` ao finalizar (conforme decidido na Issue #6: "CollectorJob encadeia ProcessorJob via BackgroundJob.Enqueue ao finalizar")?

Estas perguntas devem ser resolvidas no **PM Fase 1** ou escalonadas para o orquestrador se houver ambiguidade técnica permanente.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4-5 | — | — | — |
