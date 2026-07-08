# PRD — ISSUE-7: Publisher Telegram + Hangfire Scheduler

> Status: rascunho inicial (PM Fase 1). Contém pontos em aberto — ver "Perguntas Gate 1" na Issue #7.
> Este documento será revisado/completado na PM Fase 2, após respostas do Gerente.

## Objetivo
Implementar o publisher do Telegram (`TelegramPublisher`) e o job de publicação (`PublisherJob`), e configurar o Hangfire (storage Postgres, dashboard protegido, jobs recorrentes), validando o pipeline de ponta a ponta (coleta → processamento → publicação) via Docker.

## Usuários afetados
- Operador/administrador do sistema (acompanha publicações via dashboard Hangfire e canal Telegram de teste).
- Sistema (execução automática recorrente, sem intervenção humana no fluxo feliz).

## Contexto — o que já existe
- `ISocialPublisher` (interface): `Network` + `PublishAsync(PublicationQueue item, ct)`.
- `PublicationQueue` (entidade): `Status` (Scheduled/Published/Failed/ManualPending), `ScheduledAt`, `RetryCount`, `CanRetry` (RetryCount < 3 && Status == Failed), `RegisterAttempt(success, errorMessage)`.
- `ProcessorJob` (Issue #6, já implementado): processa produtos `Queued`, gera fila `PublicationQueue` com agendamento round-robin (horários fixos 9h/12h/15h/18h/20h), marca itens de Facebook como `ManualPending`.
- Collectors individuais (`AmazonCollector`, `MercadoLivreCollector`, `ShopeeCollector`) e endpoints manuais de trigger já existem em `Program.cs`.
- **`CollectorJob` NÃO existe no código** (confirmado por busca no repositório). A Issue #5 definiu a criação desse orquestrador como escopo de uma "issue futura de Scheduler" — presumivelmente esta Issue #7, mas o corpo da Issue #7 não descreve sua criação, apenas menciona registrá-lo no cron do Hangfire.
- **`HangfireAuthFilter` NÃO existe.** Nenhuma configuração de Hangfire existe hoje no projeto.

## Casos de uso principais
1. **Publicação automática via Telegram**: item `PublicationQueue` com `Status=Scheduled` e `ScheduledAt<=now` é publicado no canal Telegram configurado (vídeo ou imagem + legenda), e seu status é atualizado para `Published`.
2. **Falha de publicação**: erro no envio é registrado (`Status=Failed`, `ErrorMessage`, `RetryCount++`).
3. **Execução agendada (cron)**: Hangfire dispara periodicamente os jobs de coleta e publicação sem intervenção manual.
4. **Disparo manual via dashboard**: operador aciona jobs manualmente pelo `/hangfire` para validação/depuração.
5. **Validação end-to-end**: fluxo completo coleta → processamento → publicação verificado via Docker Compose + dashboard.

## Casos de exceção
- Credenciais do Telegram ausentes/inválidas em `app_settings`.
- Falha de rede/HTTP ao chamar a API do Telegram.
- Mídia local ausente (`MediaLocalPath` nulo) no momento da publicação.
- Nenhum item pendente no ciclo do `PublisherJob` (job não deve falhar, apenas não fazer nada).
- Falha isolada em um dos collectors (Amazon/ML/Shopee) durante execução orquestrada — não deve impedir os demais.

## Regras de negócio (conhecidas até o momento)
- `PublisherJob` busca itens `Status=Scheduled` com `ScheduledAt<=UtcNow`.
- Sucesso → `Status=Published`, `PublishedAt=UtcNow`.
- Falha → `Status=Failed`, `ErrorMessage` preenchido, `RetryCount` incrementado.
- Hangfire: storage Postgres, dashboard `/hangfire` protegido por senha (`app_settings`), `WorkerCount=2`.
- Crons configuráveis via `app_settings`: `schedule.collector_cron` (default `0 6 * * *`), `schedule.publisher_cron` (default `0 9,12,15,18,20 * * *`).
- **Em aberto (ver perguntas Gate 1)**: escopo exato do `CollectorJob`, comportamento de retry automático de itens `Failed`, tratamento de `ManualPending`, ordem de processamento no `PublisherJob`, fallback de `MediaUrl` quando `MediaLocalPath` for nulo.

## Integrações externas
- Telegram Bot API (`sendVideo`, `sendPhoto`) via `chat_id`/`channel_id` e `bot_token` configurados em `app_settings`.
- Hangfire.PostgreSql (usa a mesma connection string do Postgres já configurado).

## Restrições / prazo
- Depende das Issues #4/#5 (collectors) e #6 (ProcessorJob), já implementadas.
- Stack: .NET 8, Hangfire + Hangfire.PostgreSql, Telegram Bot API.
- Sem prazo explícito informado na Issue.

## Definição de pronto
- `TelegramPublisher` implementado e testado (envio de vídeo e imagem, com fallback de mídia se aplicável).
- `PublisherJob` implementado, cobrindo os critérios de aceite da Issue.
- Hangfire configurado: storage Postgres, dashboard protegido, recurring jobs registrados.
- Validação end-to-end via Docker Compose (dashboard Hangfire disparando os jobs em sequência e mensagem chegando no canal de teste).
- Escopo do `CollectorJob` resolvido e implementado (ou explicitamente excluído desta issue, com plano de onde será tratado).
- Perguntas do Gate 1 respondidas e refletidas neste PRD antes da PM Fase 2.
