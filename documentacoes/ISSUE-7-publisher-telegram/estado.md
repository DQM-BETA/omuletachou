# Estado — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## Campos principais
issue: 7
repo: omuletachou
titulo: feat: Publisher Telegram + Hangfire Scheduler
rota: normal
etapa_atual: PM Fase 2
docs_path: repos/omuletachou/documentacoes/ISSUE-7-publisher-telegram
openspec_path: repos/omuletachou/openspec/changes/ISSUE-7-publisher-telegram
ultimo_agente: pm
status_comment_id: 4913934382

## Contexto
Stack: .NET 8, Hangfire.PostgreSql, Telegram Bot API
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #4/#5 (collectors), #6 (ProcessorJob)

### Ambiguidade identificada (confirmada pelo PM via busca no código)
A Issue #7 menciona `RecurringJob.AddOrUpdate<CollectorJob>` no Hangfire, mas a classe `CollectorJob` **não existe** no código (confirmado via busca em `backend/src`). A Issue #5 deixou explicitamente a criação do `CollectorJob` orquestrador para "issue futura de Scheduler", que é esta Issue #7. A Issue #6 decidiu que "CollectorJob encadeia ProcessorJob via BackgroundJob.Enqueue ao finalizar" — responsabilidade também nunca implementada. `HangfireAuthFilter` também não existe; nenhuma configuração de Hangfire existe hoje no projeto.

Perguntas de Gate 1 postadas na Issue #7 (comentário https://github.com/DQM-BETA/omuletachou/issues/7#issuecomment-4913960250), cobrindo:
1. Escopo do `CollectorJob` (criação + orquestração dos 3 collectors)
2. Encadeamento `CollectorJob` → `ProcessorJob` via `BackgroundJob.Enqueue`
3. Futuro dos endpoints manuais de trigger já existentes
4. Retry automático de itens `Failed` com `CanRetry=true`
5. Itens `ManualPending` (Facebook) — fora de escopo?
6. Chave de app_settings para senha do dashboard Hangfire
7. Ordem de processamento no PublisherJob (ScheduledAt mais antigo primeiro)
8. Fallback de `MediaUrl` quando `MediaLocalPath` for nulo (decidido na Issue #6, confirmar aplicação aqui)

Aguardando resposta do Gerente para prosseguir com PM Fase 2.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — perguntas Gate 1 postadas |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 46902 | 23 | 115s |
| 2 | PM Fase 1 | pm | sonnet | 42520 | 16 | 123s |
