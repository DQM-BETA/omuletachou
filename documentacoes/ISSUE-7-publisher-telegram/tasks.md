# Task Breakdown — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## Decisão de particionamento

2 sub-issues, ambas stack `dotnet`, sequenciais (T-02 depende de T-01 mergeado em `desenv`):

- **T-01 — Hangfire + CollectorJob + fix DI**: infraestrutura de execução em background (storage, dashboard, auth, migration) + correção do bug de DI (achado do PM) + o job de orquestração de coleta que usa essa infra. Sem essa base, não há como registrar o `PublisherJob` como recurring job nem validar o dashboard.
- **T-02 — TelegramPublisher + PublisherJob + endpoints**: depende do padrão de `RecurringJob`/config do Hangfire estabelecido em T-01. Fecha o pipeline completo (coleta → processamento → publicação) e cobre a validação end-to-end (CA26).

Motivo de não separar em 3+ partes: o volume de CA por job é pequeno o suficiente para não justificar mais overhead de PR/merge; T-01 e T-02 são coesos internamente (cada um entrega um pedaço executável e testável do pipeline).

---

## T-01 — Hangfire (config) + fix DI + CollectorJob

### O que fazer
1. **Fix DI (`Program.cs`)**: registrar `MercadoLivreCollector` e `ShopeeCollector` também como `IPlatformCollector`, além do tipo concreto já existente, para que `IEnumerable<IPlatformCollector>` resolva os 3:
   ```csharp
   builder.Services.AddHttpClient<IPlatformCollector, AmazonCollector>(); // já existe
   builder.Services.AddHttpClient<MercadoLivreCollector>();               // já existe (tipo concreto, endpoint isolado)
   builder.Services.AddHttpClient<ShopeeCollector>();                     // já existe (tipo concreto, endpoint isolado)
   builder.Services.AddHttpClient<IPlatformCollector, MercadoLivreCollector>(); // NOVO — mesma instância nomeada, registro adicional pela interface
   builder.Services.AddHttpClient<IPlatformCollector, ShopeeCollector>();       // NOVO
   ```
   Nota: `AddHttpClient<TInterface, TImplementation>` e `AddHttpClient<TConcrete>` são registros independentes no DI container (nomes de HttpClient distintos internamente) — não há conflito. `IEnumerable<IPlatformCollector>` resolve as 3 implementações registradas pela interface. Os endpoints isolados existentes (`.../mercadolivre/trigger`, `.../shopee/trigger`) continuam resolvendo pelo tipo concreto, sem regressão.
2. **Migration `hangfire.dashboard_password`**: nova entrada em `app_settings`, seed `''` (vazio), seguindo o padrão idempotente já usado nas Issues #4/#5/#6 (`INSERT ... ON CONFLICT DO NOTHING` ou equivalente EF).
3. **Configuração Hangfire (`Program.cs`)**:
   - `AddHangfire(config => config.UsePostgreSqlStorage(connectionString))` — reaproveitar `ConnectionStrings:DefaultConnection`.
   - `AddHangfireServer(options => options.WorkerCount = 2)`.
   - `HangfireAuthFilter` (novo, `IDashboardAuthorizationFilter`): lê `hangfire.dashboard_password` de `app_settings`; se vazio, nega acesso e loga `Warning` na inicialização orientando a configurar a chave (CA23); se preenchido, valida via header/basic auth e permite quando correto (CA24).
   - `app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = new[] { new HangfireAuthFilter(...) } })`.
   - `RecurringJob.AddOrUpdate<CollectorJob>("collector-job", j => j.ExecuteAsync(default), () => cronCollector)` — cron lido de `app_settings.schedule.collector_cron`, default `0 6 * * *`.
   - `RecurringJob.AddOrUpdate<PublisherJob>("publisher-job", j => j.ExecuteAsync(default), () => cronPublisher)` — cron lido de `app_settings.schedule.publisher_cron`, default `0 9,12,15,18,20 * * *`. **Nota**: T-01 registra o recurring job de `PublisherJob` mesmo que a implementação venha em T-02 — se T-02 ainda não estiver mergeado, comentar/guardar essa linha para não quebrar o build (ver ordem de sequenciamento abaixo) ou registrar como TODO até T-02 mergear. Preferir: T-01 registra `CollectorJob` recurring; o registro do `PublisherJob` recurring migra para T-02 junto com a implementação do job (evita referenciar classe inexistente).
4. **`CollectorJob`** (`AfiliadoBot.Application/Jobs/CollectorJob.cs`, mesmo padrão de `ProcessorJob.cs`):
   - Recebe `IEnumerable<IPlatformCollector>` via DI.
   - `ExecuteAsync(CancellationToken ct = default)`: itera os collectors em sequência (ordem de resolução do DI é suficiente, não precisa ordenação explícita); para cada um, `try/catch` isolado — falha loga `Error` com nome do collector e exceção, sucesso loga `Information` com contagem de produtos coletados.
   - Ao final: se pelo menos 1 collector teve sucesso (não lançou exceção) → `BackgroundJob.Enqueue<ProcessorJob>(j => j.ExecuteAsync(default))`. Se todos falharam → não encadeia, loga `Warning`.
5. **Endpoints (`Program.cs`)**:
   - `POST /api/jobs/collector/trigger` → substituído: agora executa `CollectorJob` completo (injeta `CollectorJob`, chama `ExecuteAsync`), retorna sucesso.
   - `POST /api/jobs/collector/amazon/trigger` → **novo**, isolado, injeta `AmazonCollector` (tipo concreto — precisa registro `AddHttpClient<AmazonCollector>()` adicional se ainda não resolvível como concreto; verificar se `AddHttpClient<IPlatformCollector, AmazonCollector>()` já permite injetar `AmazonCollector` diretamente — em geral não, então adicionar `builder.Services.AddHttpClient<AmazonCollector>();` também), mesmo padrão dos endpoints ML/Shopee existentes.
   - `POST /api/jobs/collector/mercadolivre/trigger`, `.../shopee/trigger` → mantidos como estão.
   - `POST /api/jobs/processor/trigger` → mantido como está.

### Critérios de aceite cobertos
CA1, CA2, CA3, CA4, CA5, CA6, CA23, CA24, CA25 (parcial — só `CollectorJob` como recurring job nesta task; `PublisherJob` recurring registrado em T-02).

### Contexto técnico
- docs: `documentacoes/ISSUE-7-publisher-telegram/prd.md`, `criterios-aceite.md`
- design: sem design.md formal (LT escreveu resumo direto no PRD — sem ambiguidade arquitetural)
- stack: .NET 8, Hangfire + Hangfire.PostgreSql, EF Core 8
- repo: DQM-BETA/omuletachou, branch base `desenv`
- padrão de Job: `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`
- entidade: `backend/src/AfiliadoBot.Domain/Entities/PublicationQueue.cs`
- DI atual: `backend/src/AfiliadoBot.Api/Program.cs`
- migrations existentes: `backend/src/AfiliadoBot.Infrastructure/Data/Migrations/` (seguir padrão das Issues #4/#5/#6 para seed de `app_settings`)

---

## T-02 — TelegramPublisher + PublisherJob + endpoints (depende de T-01 mergeado)

### O que fazer
1. **`TelegramPublisher`** (`AfiliadoBot.Infrastructure/Integrations/Social/TelegramPublisher.cs`), implementa `ISocialPublisher`:
   - `Network => SocialNetwork.Telegram`.
   - `PublishAsync(PublicationQueue item, CancellationToken ct)`: lê `telegram.bot_token`/`telegram.channel_id` de `app_settings`; se ausentes/vazios, lança exceção tratável (CA22 — capturada pelo `PublisherJob`, que marca `Failed`).
   - Mídia: `MediaType=Video` + `MediaLocalPath` preenchido → `POST https://api.telegram.org/bot{token}/sendVideo` multipart (`chat_id`, arquivo, `caption`, `parse_mode: HTML`) (CA18). `MediaType=Image` + `MediaLocalPath` → `.../sendPhoto` idem (CA19).
   - Fallback: `MediaLocalPath` nulo + `MediaUrl` preenchida → envia a partir da URL (CA20). Ambos nulos → publica só texto (`sendMessage` ou `sendPhoto`/`sendVideo` sem arquivo, conforme suporte da API — usar `sendMessage` com `text=caption` quando não há mídia), log `Warning` (CA21).
2. **`PublisherJob`** (`AfiliadoBot.Application/Jobs/PublisherJob.cs`, mesmo padrão de `ProcessorJob`/`CollectorJob`):
   - Query: `Status == Scheduled && ScheduledAt <= UtcNow` OU `Status == Failed && CanRetry` (usar a property computada `CanRetry` da entidade, ou replicar a condição `RetryCount < 3 && Status == Failed` na query EF, já que `CanRetry` não é mapeável diretamente por LINQ-to-SQL — confirmar se EF Core 8 traduz a property; se não, usar a condição explícita).
   - `OrderBy(x => x.ScheduledAt).ThenBy(x => x.CreatedAt)` (CA12).
   - Exclui `ManualPending` implicitamente (não bate no filtro `Scheduled`/`Failed`) (CA13).
   - Para cada item: resolve `ISocialPublisher` pela `SocialNetwork` do item (via `IEnumerable<ISocialPublisher>`, buscar o que casa `Network == item.SocialNetwork`), chama `PublishAsync`.
   - Sucesso → `item.RegisterAttempt(true)` (já seta `Published`/`PublishedAt`) (CA14).
   - Falha (exceção capturada) → `item.RegisterAttempt(false, ex.Message)` (já incrementa `RetryCount`, seta `Failed`) (CA15, CA16 — `CanRetry` vira `false` automaticamente quando `RetryCount` chega a 3, via property computada na entidade).
   - Nenhum item elegível → retorna sem erro, sem side-effects (CA17).
   - `SaveChangesAsync` após cada item (ou em lote ao final — preferir por item para não perder progresso em caso de falha do job inteiro).
3. **Registrar recurring job do `PublisherJob`** em `Program.cs` (a linha que ficou pendente/comentada em T-01): `RecurringJob.AddOrUpdate<PublisherJob>("publisher-job", j => j.ExecuteAsync(default), () => cronPublisher)`, cron de `app_settings.schedule.publisher_cron`, default `0 9,12,15,18,20 * * *` (CA25 completo).
4. **Endpoint novo**: `POST /api/jobs/publisher/trigger` → injeta `PublisherJob`, chama `ExecuteAsync`, retorna sucesso (CA7).
5. **DI (`Program.cs`)**: registrar `TelegramPublisher` como `ISocialPublisher` (`builder.Services.AddHttpClient<ISocialPublisher, TelegramPublisher>()`, mesmo padrão dos collectors) e `PublisherJob` (`AddHttpClient<PublisherJob>()` ou `AddScoped`, seguindo o padrão de `ProcessorJob`).
6. **Validação end-to-end (CA26)**: subir `docker compose up -d`, disparar `CollectorJob` pelo dashboard `/hangfire`, confirmar produtos em `products`, fila `publication_queue` populada (via encadeamento `ProcessorJob`), disparar `PublisherJob` e confirmar mensagem no canal Telegram de teste. Documentar evidência (prints/log) no PR.

### Critérios de aceite cobertos
CA7, CA8, CA9, CA10, CA11, CA12, CA13, CA14, CA15, CA16, CA17, CA18, CA19, CA20, CA21, CA22, CA25 (completo), CA26.

### Contexto técnico
- docs: `documentacoes/ISSUE-7-publisher-telegram/prd.md`, `criterios-aceite.md`
- stack: .NET 8, Hangfire, Telegram Bot API (multipart HTTP)
- repo: DQM-BETA/omuletachou, branch base `desenv` (após T-01 mergeado)
- interface: `ISocialPublisher` (`Network` + `PublishAsync`)
- entidade: `PublicationQueue.RegisterAttempt(success, errorMessage)` já implementa a lógica de status/retry — reutilizar, não duplicar regra de negócio no Job
- padrão de Job/HttpClient: `ProcessorJob.cs`, `CollectorJob.cs` (T-01)
- DI/config Hangfire: `Program.cs` (T-01)
