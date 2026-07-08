# Estado — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## Campos principais
issue: 7
repo: omuletachou
titulo: feat: Publisher Telegram + Hangfire Scheduler
rota: normal
etapa_atual: QA
docs_path: repos/omuletachou/documentacoes/ISSUE-7-publisher-telegram
openspec_path: repos/omuletachou/openspec/changes/ISSUE-7-publisher-telegram
ultimo_agente: lt
status_comment_id: 4913934382
pr_homologacao: 63

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

### Líder Técnico — merge T-01 (#59) concluído (2026-07-08)
PR #61 (feature/59-collectorjob-hangfire → desenv) mergeado via squash. Sub-issue #59 fechada. Aguardando T-02 (#60) para depois criar PR desenv→homolog conjunto (as duas sub-issues compõem a mesma issue-pai #7).

### Líder Técnico — merge T-02 (#60) concluído (2026-07-08)
PR #62 (feature/60-telegram-publisher → desenv) mergeado via squash. Sub-issue #60 fechada. Todas as sub-issues de #7 concluídas — PR desenv→homolog #63 criado (release conjunta T-01+T-02).

## Sub-issues
sub_issues: [59 (T-01, stack:dotnet) — concluída/mergeada, 60 (T-02, stack:dotnet, depende de #59) — concluída/mergeada]
desenv_tasks_merged: [59, 60]

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — perguntas Gate 1 postadas |
| 3 | Gate 1 | Gerente | concluido — respostas postadas em 2026-07-08 |
| 4 | PM Fase 2 | pm | concluido — PRD consolidado, criterios-aceite.md criado, sem ambiguidade arquitetural, segue para LT |
| 5 | Refinamento LT | lt | concluido — tasks.md criado, sub-issues #59 (T-01) e #60 (T-02) criadas no GitHub |
| 6 | Dev T-01 (#59) | dev-dotnet | concluido — PR #61 (feature/59-collectorjob-hangfire → desenv) aberto, aguardando merge do LT |
| 7 | Merge T-01 (#59) | lt | concluido — PR #61 mergeado (squash) em desenv, sub-issue #59 fechada; aguardando spawn de Dev para T-02 (#60) |
| 8 | Dev T-02 (#60) | dev-dotnet | concluido — PR #62 (feature/60-telegram-publisher → desenv) aberto, aguardando merge do LT |
| 9 | Merge T-02 (#60) + PR release | lt | concluido — PR #62 mergeado (squash) em desenv, sub-issue #60 fechada; PR #63 (desenv→homolog) criado |
| 10 | Merge PR #63 desenv->homolog | lt | concluido — PR #63 mergeado (merge commit) em homolog, autorizado pelo Gerente; pronto para QA |

### Dev T-01 (#59) — implementacao concluida (2026-07-08)
- Fix DI: `MercadoLivreCollector`/`ShopeeCollector` agora resolviveis via `IPlatformCollector` (alem do tipo concreto); `AmazonCollector` ganhou registro concreto adicional para o endpoint isolado.
- Hangfire configurado: storage Postgres (`UsePostgreSqlStorage`), `AddHangfireServer(WorkerCount=2)`, dashboard `/hangfire` protegido por `HangfireAuthFilter` (nega acesso com senha vazia/incorreta, loga `Warning` na inicializacao se vazia).
- Migration `SeedHangfireDashboardPassword` (Id=32, seed vazio) aplicada e validada em Docker.
- `CollectorJob` criado (`AfiliadoBot.Application/Jobs/CollectorJob.cs`): orquestra os 3 collectors via `IEnumerable<IPlatformCollector>`, falha isolada por collector (log `Error`, nao interrompe os demais), encadeia `ProcessorJob` via `IBackgroundJobClient.Enqueue` se ao menos 1 sucesso.
- Recurring job do `CollectorJob` registrado via `IRecurringJobManager` (nao a API estatica `RecurringJob` — esta depende de `JobStorage.Current`, que so inicializa quando o DI resolve `JobStorage` pela 1a vez; usar a API estatica direto no `Program.cs` quebrava o boot com `InvalidOperationException`).
- Endpoints: `/api/jobs/collector/trigger` (unificado, `CollectorJob` completo) + `/api/jobs/collector/amazon/trigger` (novo) + ML/Shopee/processor mantidos.
- Testes novos: `CollectorJobTests` (4 casos — orquestracao, resiliencia a falha parcial, encadeamento condicional) e `HangfireAuthFilterTests` (4 casos — bloqueia senha vazia/nao configurada/incorreta, autoriza senha correta). Total 88/88 passando (80 pre-existentes + 8 novos).
- Nota tecnica de infra (documentada no PR, nao bloqueou): Hangfire e desligado nos testes de integracao via env var de processo (`Hangfire__Enabled=false`, setada no `static ctor` de `CustomWebApplicationFactory`) — `AddHangfire`/`UsePostgreSqlStorage` conecta de forma sincrona ao Postgres para preparar o schema, o que quebraria o host de teste (EF InMemory, sem Postgres real). Quando desligado, um `IBackgroundJobClient` no-op e registrado para o `CollectorJob` continuar resolvivel via DI.
- Boot Docker validado: `docker compose up -d --build` limpo, `/health` 200, `/hangfire` 401 (bloqueado, senha vazia por padrao — esperado), `POST /api/jobs/collector/trigger` 200 (3 collectors chamados via DI, falha de credenciais ausentes logada isoladamente por plataforma, `ProcessorJob` corretamente NAO enfileirado por falha total).
- PR: https://github.com/DQM-BETA/omuletachou/pull/61 (feature/59-collectorjob-hangfire → desenv) — MERGEADO em 2026-07-08 (squash).

### Dev T-02 (#60) — implementacao concluida (2026-07-08)
- `TelegramPublisher` criado (`AfiliadoBot.Infrastructure/Integrations/Social/TelegramPublisher.cs`), implementa `ISocialPublisher`: le `telegram.bot_token`/`telegram.channel_id` de `app_settings`, lanca `InvalidOperationException` se ausentes (capturada pelo `PublisherJob`). Detecta midia por `Product.MediaType` (`video`→`sendVideo`, senao `sendPhoto`); fallback `MediaLocalPath` (multipart via arquivo local) → `MediaUrl` (URL direta no campo de midia do multipart) → sem midia (`sendMessage` com `text=caption`, log `Warning`). `caption`=`Product.AiCaption`, `parse_mode=HTML`.
- `PublisherJob` criado (`AfiliadoBot.Application/Jobs/PublisherJob.cs`): busca `PublicationQueue` com `(Status=Scheduled && ScheduledAt<=UtcNow) || (Status=Failed && RetryCount<3)`, ordenado por `ScheduledAt ASC, CreatedAt ASC` (query explicita, `CanRetry` computada nao usada em LINQ-to-SQL). Resolve `ISocialPublisher` via `IEnumerable<ISocialPublisher>` filtrado por `Network==item.SocialNetwork`. Sucesso/falha delegados a `item.RegisterAttempt(...)` (regra de status/retry ja na entidade, nao duplicada no job). `SaveChangesAsync` por item.
- Recurring job do `PublisherJob` registrado via `IRecurringJobManager` em `Program.cs` (mesmo padrao do `CollectorJob`, T-01), cron de `schedule.publisher_cron` (default `0 9,12,15,18,20 * * *`).
- Endpoint `POST /api/jobs/publisher/trigger` adicionado.
- DI: `AddHttpClient<ISocialPublisher, TelegramPublisher>()` e `AddScoped<PublisherJob>()`.
- Testes novos: `PublisherJobTests` (10 casos — selecao Scheduled/Failed, ordenacao, ManualPending ignorado, sucesso/falha/retry esgotado, nenhum item pendente) e `TelegramPublisherTests` (6 casos — video/foto/texto, fallback MediaLocalPath→MediaUrl, credenciais ausentes). Total 104/104 passando (88 pre-existentes + 16 novos).
- Boot Docker validado: `docker compose up -d --build` limpo, `/health` 200, logs confirmam leitura de `schedule.publisher_cron` e registro do recurring job `publisher-job`, `POST /api/jobs/publisher/trigger` 200 (fila vazia, sem erro).
- PR: https://github.com/DQM-BETA/omuletachou/pull/62 (feature/60-telegram-publisher → desenv) — MERGEADO em 2026-07-08 (squash).

### Líder Técnico — PR release desenv→homolog #63 criado (2026-07-08)
Todas as sub-issues de #7 (T-01 #59, T-02 #60) mergeadas em `desenv`. PR #63 (desenv→homolog) criado consolidando as duas entregas (Hangfire + CollectorJob + TelegramPublisher + PublisherJob). Aguardando Code Review (2 camadas: `/code-review` plugin + agente Code Review).

### Líder Técnico — merge PR #63 desenv→homolog concluído (2026-07-08)
Code Review (2 camadas) aprovado, boot Docker validado 2x, 104/104 testes. Gerente autorizou explicitamente o merge no chat da sessão principal. PR #63 mergeado via merge commit (NUNCA squash) em `homolog` (commit bd09557, `9d0d04c..bd09557`). Pronto para QA validar em homolog.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 46902 | 23 | 115s |
| 2 | PM Fase 1 | pm | sonnet | 42520 | 16 | 123s |
| 4 | PM Fase 2 | pm | sonnet | 57788 | 22 | 188s |
| 5 | Refinamento LT | lt | sonnet | 65434 | 17 | 158s |
| 6 | Dev T-01 #59 | dev-dotnet | sonnet | 167767 | 122 | 1015s |
| 7 | Merge T-01 (#59) | lt | sonnet | 41660 | 14 | 85s |
| 8 | Dev T-02 #60 | dev-dotnet | sonnet | 127642 | 68 | 428s |
| 9 | Merge T-02 + PR homolog | lt | sonnet | 44973 | 13 | 141s |
| 10 | Code Review PR #63 | code-review | sonnet | 101400 | 27 | 259s |
| 11 | Merge PR #63 desenv->homolog | lt | sonnet | 44738 | 11 | 116s |
