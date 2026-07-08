# PRD — ISSUE-7: Publisher Telegram + Hangfire Scheduler

> Status: consolidado (PM Fase 2), pós Gate 1. Ver histórico completo das perguntas/respostas do Gate 1 na Issue #7 (comentários).

## Objetivo
Implementar o publisher do Telegram (`TelegramPublisher`), o job de publicação (`PublisherJob`), o job orquestrador de coleta (`CollectorJob` — não existia no código, criação decidida no Gate 1 como escopo desta issue) e configurar o Hangfire (storage Postgres, dashboard protegido, jobs recorrentes), validando o pipeline de ponta a ponta (coleta → processamento → publicação) via Docker.

## Usuários afetados
- Operador/administrador do sistema (acompanha publicações via dashboard Hangfire e canal Telegram de teste; opera manualmente itens `ManualPending` do Facebook em outra tela, fora de escopo).
- Sistema (execução automática recorrente via cron, sem intervenção humana no fluxo feliz).

## Contexto — o que já existe
- `ISocialPublisher` (interface): `Network` + `PublishAsync(PublicationQueue item, ct)`.
- `PublicationQueue` (entidade): `Status` (Scheduled/Published/Failed/ManualPending), `ScheduledAt`, `RetryCount`, `CanRetry` (RetryCount < 3 && Status == Failed), `RegisterAttempt(success, errorMessage)`.
- `ProcessorJob` (Issue #6, já em produção): processa produtos `Queued`, gera fila `PublicationQueue` com agendamento round-robin (horários fixos 9h/12h/15h/18h/20h), marca itens de Facebook como `ManualPending`.
- Collectors: `AmazonCollector`, `MercadoLivreCollector`, `ShopeeCollector` — todos implementam `IPlatformCollector` (`CollectAsync(ct)`), já em produção.
- Endpoints manuais de trigger já existentes em `Program.cs`: `/api/jobs/collector/trigger` (hoje resolve só `IPlatformCollector` → Amazon, ver nota técnica abaixo), `/api/jobs/collector/mercadolivre/trigger`, `/api/jobs/collector/shopee/trigger`, `/api/jobs/processor/trigger`.
- **Nota técnica para o LT/Dev** (achado do PM, não é decisão de negócio): a DI atual registra `AddHttpClient<IPlatformCollector, AmazonCollector>()` — isso vincula a interface `IPlatformCollector` exclusivamente ao `AmazonCollector`. `MercadoLivreCollector` e `ShopeeCollector` são registrados apenas pelo tipo concreto (`AddHttpClient<MercadoLivreCollector>()` / `AddHttpClient<ShopeeCollector>()`), sem vínculo à interface. Se o `CollectorJob` for implementado recebendo `IEnumerable<IPlatformCollector>` sem ajustar essas 3 linhas de `Program.cs`, o DI resolve **apenas o Amazon** na lista — bug silencioso. É necessário registrar os 3 collectors também como `IPlatformCollector` (ex.: `AddHttpClient<IPlatformCollector, AmazonCollector>()`, idem para os outros dois, ou usar `TryAddEnumerable`/registro explícito de cada um como `IPlatformCollector`) para que `IEnumerable<IPlatformCollector>` resolva os 3. Isso é ajuste de configuração de DI, aditivo, sem risco de regressão nos endpoints manuais individuais existentes (que continuam referenciando os tipos concretos `MercadoLivreCollector`/`ShopeeCollector` diretamente) — **não configura ambiguidade arquitetural**, é detalhe de implementação para o refinamento técnico do LT.
- `HangfireAuthFilter` **não existe**. Nenhuma configuração de Hangfire existe hoje no projeto (dependência nova: `Hangfire.PostgreSql`, já prevista no CLAUDE.md do repo como parte da stack).
- `CollectorJob` **não existe** — criação é escopo confirmado desta issue (ver Gate 1).

## Casos de uso principais
1. **Coleta orquestrada (CollectorJob)**: dispara Amazon → MercadoLivre → Shopee em sequência (via `IEnumerable<IPlatformCollector>`, corrigido o registro DI). Falha em um collector não impede os demais — captura de exceção isolada por collector, log de erro, e prossegue para o próximo. Ao final, registra (log) quais plataformas tiveram sucesso e quais falharam.
2. **Encadeamento automático**: ao término do `CollectorJob` — mesmo com falhas parciais em collectors individuais —, enfileira o `ProcessorJob` via `BackgroundJob.Enqueue<ProcessorJob>(j => j.ExecuteAsync())`. Só não encadeia se o `CollectorJob` falhar por completo, sem coletar nada de nenhuma plataforma.
3. **Publicação automática via Telegram**: item `PublicationQueue` com `Status=Scheduled` e `ScheduledAt<=now`, OU `Status=Failed` com `CanRetry=true` (RetryCount < 3), é publicado no canal Telegram configurado (vídeo ou imagem + legenda). Sucesso → `Status=Published`.
4. **Retry automático de falhas**: ao pegar um item `Failed` com `CanRetry=true`, o `PublisherJob` incrementa `RetryCount` e tenta novamente. Se atingir `RetryCount=3`, o item fica `Failed` definitivo (`CanRetry=false`) e não é mais processado automaticamente.
5. **Execução agendada (cron)**: Hangfire dispara periodicamente `CollectorJob` (`schedule.collector_cron`, default `0 6 * * *`) e `PublisherJob` (`schedule.publisher_cron`, default `0 9,12,15,18,20 * * *`) sem intervenção manual.
6. **Disparo manual via dashboard/endpoints**: operador aciona jobs manualmente pelo `/hangfire` (protegido por senha) ou via endpoints REST, para validação/depuração — endpoints individuais mantidos + endpoint unificado novo do `CollectorJob` completo + endpoint novo do `PublisherJob`.
7. **Validação end-to-end**: fluxo completo coleta → processamento → publicação verificado via Docker Compose + dashboard Hangfire, com mensagem chegando no canal Telegram de teste.

## Casos de exceção
- Credenciais do Telegram ausentes/inválidas em `app_settings` → falha de publicação tratada como qualquer outra falha (Status=Failed, ErrorMessage, RetryCount++).
- Falha de rede/HTTP ao chamar a API do Telegram → idem.
- Mídia local ausente (`MediaLocalPath` nulo) no momento da publicação → fallback para `MediaUrl` (herdado da Issue #6). Se `MediaUrl` também nulo → publica só com legenda em texto, log Warning.
- Nenhum item pendente (`Scheduled` vencido ou `Failed` com `CanRetry=true`) no ciclo do `PublisherJob` → job não falha, apenas não faz nada.
- Falha isolada em um dos collectors (Amazon/ML/Shopee) durante execução orquestrada → não impede os demais; `CollectorJob` prossegue e loga o resultado por plataforma.
- `CollectorJob` falha por completo (nenhuma plataforma coletou nada) → não encadeia `ProcessorJob`.
- Senha do dashboard Hangfire (`hangfire.dashboard_password`) vazia → `HangfireAuthFilter` bloqueia acesso ao `/hangfire` e loga aviso na inicialização orientando a configurar a chave.
- Itens `ManualPending` (Facebook) → ignorados completamente pelo `PublisherJob`; tratados manualmente em outra tela (dashboard Angular `/facebook-manual`), **fora do escopo desta issue**.

## Regras de negócio (confirmadas no Gate 1)
### CollectorJob
- Orquestra `AmazonCollector`, `MercadoLivreCollector`, `ShopeeCollector` em sequência (via `IEnumerable<IPlatformCollector>`).
- Captura exceção por collector individualmente — falha de um não impede os demais.
- Ao final, registra (log) quais plataformas tiveram sucesso e quais falharam.
- Encadeia `BackgroundJob.Enqueue<ProcessorJob>(j => j.ExecuteAsync())` ao término, mesmo com falhas parciais. Só não encadeia se falhar por completo (zero sucesso).

### Endpoints (trigger manual)
- `POST /api/jobs/collector/trigger` → `CollectorJob` completo (todas as plataformas + encadeamento do ProcessorJob), **novo**.
- `POST /api/jobs/collector/amazon/trigger`, `/api/jobs/collector/mercadolivre/trigger`, `/api/jobs/collector/shopee/trigger` → mantidos, isolados (teste/diagnóstico por plataforma, sem encadear ProcessorJob). Nota: o endpoint atual `/api/jobs/collector/trigger` resolve hoje `IPlatformCollector` isolado (Amazon) — será substituído pelo novo comportamento (CollectorJob completo); o trigger isolado do Amazon precisa ganhar rota própria (`/api/jobs/collector/amazon/trigger`) para não quebrar a paridade com ML/Shopee.
- `POST /api/jobs/processor/trigger` → mantido (já existe).
- `POST /api/jobs/publisher/trigger` → **novo**.

### PublisherJob
- Busca itens com `Status=Scheduled AND ScheduledAt<=UtcNow` OU `Status=Failed AND CanRetry=true` (RetryCount<3).
- Ordenação: `ORDER BY ScheduledAt ASC, CreatedAt ASC` (mais antigo primeiro; empate por CreatedAt).
- Para cada item: resolve o `ISocialPublisher` da rede (`Network`) e chama `PublishAsync`.
- Item `Failed` reprocessado: incrementa `RetryCount` antes/durante a tentativa.
- Sucesso → `Status=Published`, `PublishedAt=UtcNow`.
- Falha → `Status=Failed`, `ErrorMessage` preenchido, `RetryCount` incrementado. Se `RetryCount` atinge 3 → `CanRetry=false` (definitivo, não reprocessado automaticamente).
- Itens `ManualPending` (Facebook) → nunca selecionados por este job.
- Fallback de mídia: `MediaLocalPath` nulo → usa `MediaUrl`. Ambos nulos → publica só com legenda de texto (log Warning).

### TelegramPublisher
- `TelegramPublisher : ISocialPublisher`, em `AfiliadoBot.Infrastructure/Integrations/Social/`.
- Vídeo: `POST https://api.telegram.org/bot{token}/sendVideo` com `chat_id`, arquivo de vídeo (multipart), `caption`, `parse_mode: HTML`.
- Imagem: `POST .../sendPhoto` com `chat_id`, arquivo de imagem (multipart), `caption`, `parse_mode: HTML`.
- Credenciais lidas de `app_settings`: `telegram.bot_token`, `telegram.channel_id`.

### Hangfire
- Storage: `Hangfire.PostgreSql`, mesma connection string do Postgres já configurado.
- `AddHangfireServer(options => options.WorkerCount = 2)`.
- Dashboard `/hangfire` protegido por `HangfireAuthFilter`, senha lida de `app_settings.hangfire.dashboard_password`.
- Nova chave `hangfire.dashboard_password` em `app_settings` — **seed vazio**. Se vazio, `HangfireAuthFilter` bloqueia o acesso ao dashboard e loga um aviso (Warning) na inicialização orientando o operador a configurar a senha (nova migration necessária, ver abaixo).
- `RecurringJob.AddOrUpdate<CollectorJob>` com cron de `app_settings.schedule.collector_cron` (default `0 6 * * *`).
- `RecurringJob.AddOrUpdate<PublisherJob>` com cron de `app_settings.schedule.publisher_cron` (default `0 9,12,15,18,20 * * *`).

## Integrações externas
- Telegram Bot API (`sendVideo`, `sendPhoto`) via `chat_id`/`channel_id` e `bot_token` configurados em `app_settings`.
- Hangfire.PostgreSql (reaproveita a connection string do Postgres já configurado — sem infraestrutura de banco nova).

## Restrições / prazo
- Depende das Issues #4/#5 (collectors) e #6 (ProcessorJob), já implementadas e em produção (main).
- Stack: .NET 8, Hangfire + Hangfire.PostgreSql, Telegram Bot API — Hangfire.PostgreSql já listado no CLAUDE.md do repo desde o início da stack definida, não é infraestrutura surpresa.
- Sem prazo explícito informado na Issue.

## Migration necessária
- Nova chave `app_settings`: `hangfire.dashboard_password`, seed com valor vazio (`''`). Seguir o padrão de migration já usado para outras chaves de `app_settings` nas Issues #4/#5/#6 (seed idempotente via `INSERT ... ON CONFLICT DO NOTHING` ou equivalente EF).

## Ajuste de configuração DI (achado técnico do PM, não requer decisão de negócio)
- `Program.cs`: registrar `MercadoLivreCollector` e `ShopeeCollector` também como `IPlatformCollector` no DI (hoje só `AmazonCollector` está vinculado à interface via `AddHttpClient<IPlatformCollector, AmazonCollector>()`), para que `CollectorJob` receba os 3 via `IEnumerable<IPlatformCollector>`. Ajuste aditivo, sem impacto nos endpoints manuais isolados existentes.

## Definição de pronto
- `CollectorJob` implementado: orquestra os 3 collectors via `IEnumerable<IPlatformCollector>` (com DI corrigido), captura exceção isolada por collector, loga sucesso/falha por plataforma, encadeia `ProcessorJob` ao final (exceto falha total).
- `TelegramPublisher` implementado e testado (envio de vídeo e imagem, com fallback de mídia).
- `PublisherJob` implementado: processa `Scheduled` vencidos + `Failed` com `CanRetry=true`, ordenação `ScheduledAt ASC, CreatedAt ASC`, retry até 3 tentativas.
- Hangfire configurado: storage Postgres, dashboard protegido por `HangfireAuthFilter` (bloqueia se senha vazia, loga aviso), recurring jobs (`CollectorJob`, `PublisherJob`) registrados com crons configuráveis.
- Endpoints: `/api/jobs/collector/trigger` (unificado, novo), `/api/jobs/collector/amazon/trigger` (novo, paridade com ML/Shopee), `/api/jobs/collector/mercadolivre/trigger`, `/api/jobs/collector/shopee/trigger`, `/api/jobs/processor/trigger` (mantidos), `/api/jobs/publisher/trigger` (novo).
- Migration da chave `hangfire.dashboard_password` (seed vazio) criada e aplicada.
- Validação end-to-end via Docker Compose: dashboard Hangfire disparando os jobs em sequência (Collector → Processor → Publisher) e mensagem chegando no canal Telegram de teste.
- Critérios de aceite (ver `criterios-aceite.md`) cobertos por testes automatizados.
