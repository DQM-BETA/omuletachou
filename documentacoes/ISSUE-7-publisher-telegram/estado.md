# Estado — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## Campos principais
issue: 7
repo: omuletachou
titulo: feat: Publisher Telegram + Hangfire Scheduler
rota: normal
etapa_atual: Dev — aguardando spawn T-01
docs_path: repos/omuletachou/documentacoes/ISSUE-7-publisher-telegram
openspec_path: repos/omuletachou/openspec/changes/ISSUE-7-publisher-telegram
ultimo_agente: lt
status_comment_id: 4913934382

## Contexto
Stack: .NET 8, Hangfire.PostgreSql, Telegram Bot API
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #4/#5 (collectors), #6 (ProcessorJob) — já em produção (main)

### Gate 1 — respondido pelo Gerente (2026-07-08)
Perguntas postadas em https://github.com/DQM-BETA/omuletachou/issues/7#issuecomment-4913960250, respostas completas em https://github.com/DQM-BETA/omuletachou/issues/7#issuecomment-4913977805. Resumo:
1. `CollectorJob` é escopo desta issue — orquestra Amazon/ML/Shopee em sequência, exceção capturada por collector isoladamente.
2. Encadeia `ProcessorJob` via `BackgroundJob.Enqueue` ao final, mesmo com falha parcial (não encadeia só se falhar por completo).
3. Endpoints: mantém os 3 individuais (Amazon ganha rota própria `/api/jobs/collector/amazon/trigger` para paridade) + `/api/jobs/collector/trigger` (unificado) + `/api/jobs/publisher/trigger` (novo).
4. `PublisherJob` processa `Scheduled` vencidos + `Failed` com `CanRetry=true` (RetryCount<3); ao atingir 3, `CanRetry=false`.
5. `ManualPending` (Facebook) fora do escopo — tratado em `/facebook-manual` (outra issue).
6. Nova chave `hangfire.dashboard_password` em `app_settings`, seed vazio, bloqueia acesso + loga aviso se vazio.
7. `ORDER BY ScheduledAt ASC, CreatedAt ASC`.
8. Fallback `MediaUrl` quando `MediaLocalPath` nulo (herdado da Issue #6), confirmado para esta issue.

### PM Fase 2 — concluída (2026-07-08)
PRD consolidado em `documentacoes/ISSUE-7-publisher-telegram/prd.md` e critérios de aceite (CA1–CA26, Given/When/Then) em `documentacoes/ISSUE-7-publisher-telegram/criterios-aceite.md`. `openspec/changes/ISSUE-7-publisher-telegram/proposal.md` criado com o resumo.

**Achado técnico do PM (não é ambiguidade de negócio, documentado no PRD para o LT):** a DI atual em `Program.cs` registra `AddHttpClient<IPlatformCollector, AmazonCollector>()`, vinculando `IPlatformCollector` só ao Amazon. `MercadoLivreCollector`/`ShopeeCollector` são registrados apenas como tipo concreto. Se `CollectorJob` usar `IEnumerable<IPlatformCollector>` sem corrigir essas 3 linhas, resolve só 1 collector (bug silencioso). Precisa de ajuste aditivo no DI — sem risco de regressão nos endpoints manuais isolados existentes.

**Avaliação de ambiguidade arquitetural: NÃO escalado ao Arquiteto.**
- `IEnumerable<IPlatformCollector>` vs. injeção de 3 tipos concretos: decisão técnica direta, resolvida pelo PM (usar `IEnumerable<IPlatformCollector>`, mais escalável; ajuste de DI é aditivo).
- Configuração do Hangfire: biblioteca já prevista na stack (`Hangfire.PostgreSql` no CLAUDE.md do repo desde o início) — configuração padrão, não infraestrutura nova/surpresa.
- Nenhum risco de regressão identificado nos collectors/ProcessorJob já em produção: `CollectorJob` só orquestra chamadas existentes; encadeamento ao `ProcessorJob` é aditivo (enfileiramento Hangfire), sem alterar lógica interna desses componentes.
- Segue direto para o Líder Técnico (refinamento técnico + task breakdown), sem passar pelo Arquiteto.

### Líder Técnico — refinamento concluído (2026-07-08)
Task breakdown documentado em `documentacoes/ISSUE-7-publisher-telegram/tasks.md`. Sem UI — pipeline segue direto para os Devs (sem UX/UI).

Decisão de particionamento: 2 sub-issues sequenciais, ambas stack `dotnet`:
- **T-01 (#59)**: Hangfire (config, dashboard, HangfireAuthFilter, migration seed `hangfire.dashboard_password`) + fix do bug de DI (achado do PM — registrar ML/Shopee também como `IPlatformCollector`) + `CollectorJob` (orquestração + encadeamento `ProcessorJob`) + endpoints de collector.
- **T-02 (#60)**: depende de T-01 mergeado em `desenv`. `TelegramPublisher` + `PublisherJob` (retry, ordenação, fallback de mídia) + registro do recurring job do `PublisherJob` + endpoint `/api/jobs/publisher/trigger` + validação end-to-end (CA26).

## Sub-issues
sub_issues: [59 (T-01, stack:dotnet), 60 (T-02, stack:dotnet, depende de #59)]
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — perguntas Gate 1 postadas |
| 3 | Gate 1 | Gerente | concluido — respostas postadas em 2026-07-08 |
| 4 | PM Fase 2 | pm | concluido — PRD consolidado, criterios-aceite.md criado, sem ambiguidade arquitetural, segue para LT |
| 5 | Refinamento LT | lt | concluido — tasks.md criado, sub-issues #59 (T-01) e #60 (T-02) criadas no GitHub |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 46902 | 23 | 115s |
| 2 | PM Fase 1 | pm | sonnet | 42520 | 16 | 123s |
| 4 | PM Fase 2 | pm | sonnet | 57788 | 22 | 188s |
| 5 | Refinamento LT | lt | sonnet | 65434 | 17 | 158s |
