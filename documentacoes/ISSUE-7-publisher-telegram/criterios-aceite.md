# Critérios de Aceite — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## CollectorJob

**CA1 — Orquestração sequencial bem-sucedida**
Given os 3 collectors (Amazon, MercadoLivre, Shopee) configurados e saudáveis
When `CollectorJob.ExecuteAsync()` é executado
Then os 3 são chamados em sequência e seus produtos são persistidos, e o `ProcessorJob` é enfileirado via `BackgroundJob.Enqueue` ao final.

**CA2 — Falha isolada em um collector não impede os demais**
Given o `AmazonCollector` lança exceção (ex.: erro de rede)
When `CollectorJob.ExecuteAsync()` é executado
Then a exceção é capturada e logada, `MercadoLivreCollector` e `ShopeeCollector` são executados normalmente, e o `ProcessorJob` é enfileirado ao final (falha parcial não bloqueia o encadeamento).

**CA3 — Falha total não encadeia ProcessorJob**
Given os 3 collectors lançam exceção
When `CollectorJob.ExecuteAsync()` é executado
Then nenhum produto é coletado e o `ProcessorJob` NÃO é enfileirado.

**CA4 — DI resolve os 3 collectors**
Given o `CollectorJob` recebe `IEnumerable<IPlatformCollector>` injetado
When o container de DI resolve a dependência
Then a lista contém exatamente 3 instâncias (`AmazonCollector`, `MercadoLivreCollector`, `ShopeeCollector`).

## Endpoints de trigger

**CA5 — Endpoint unificado do CollectorJob**
Given uma requisição `POST /api/jobs/collector/trigger`
When processada
Then o `CollectorJob` completo é executado (3 plataformas + encadeamento do ProcessorJob) e a resposta indica sucesso.

**CA6 — Endpoints isolados por plataforma mantidos**
Given requisições `POST /api/jobs/collector/amazon/trigger`, `.../mercadolivre/trigger`, `.../shopee/trigger`
When processadas
Then cada uma executa apenas o collector correspondente, sem encadear o `ProcessorJob`.

**CA7 — Endpoint do PublisherJob**
Given uma requisição `POST /api/jobs/publisher/trigger`
When processada
Then o `PublisherJob.ExecuteAsync()` é executado e a resposta indica sucesso.

## PublisherJob — seleção e ordenação

**CA8 — Seleciona itens Scheduled vencidos**
Given um item `PublicationQueue` com `Status=Scheduled` e `ScheduledAt<=UtcNow`
When `PublisherJob.ExecuteAsync()` é executado
Then o item é processado (publicação tentada).

**CA9 — Ignora itens Scheduled futuros**
Given um item `Status=Scheduled` com `ScheduledAt>UtcNow`
When `PublisherJob.ExecuteAsync()` é executado
Then o item NÃO é processado neste ciclo.

**CA10 — Reprocessa itens Failed com CanRetry=true**
Given um item `Status=Failed` com `RetryCount=1` (`CanRetry=true`)
When `PublisherJob.ExecuteAsync()` é executado
Then o item é reprocessado, `RetryCount` é incrementado, e o resultado atualiza `Status`.

**CA11 — Não reprocessa itens Failed com CanRetry=false**
Given um item `Status=Failed` com `RetryCount=3` (`CanRetry=false`)
When `PublisherJob.ExecuteAsync()` é executado
Then o item NÃO é selecionado nem reprocessado.

**CA12 — Ordenação por ScheduledAt, desempate por CreatedAt**
Given múltiplos itens elegíveis com `ScheduledAt` diferentes (e alguns iguais)
When `PublisherJob.ExecuteAsync()` é executado
Then o processamento segue `ORDER BY ScheduledAt ASC, CreatedAt ASC`.

**CA13 — Ignora itens ManualPending**
Given um item `Status=ManualPending` (Facebook)
When `PublisherJob.ExecuteAsync()` é executado
Then o item NÃO é selecionado nem processado.

## PublisherJob — publicação e retry

**CA14 — Sucesso de publicação**
Given credenciais válidas e mídia disponível
When `PublishAsync` retorna sucesso
Then `Status=Published` e `PublishedAt=UtcNow`.

**CA15 — Falha de publicação, ainda com retry disponível**
Given uma falha no envio (ex.: erro HTTP) e `RetryCount` atual = 0
When `PublisherJob` processa o item
Then `Status=Failed`, `ErrorMessage` preenchido, `RetryCount=1`, `CanRetry=true`.

**CA16 — Terceira falha consecutiva esgota o retry**
Given um item que já falhou 2 vezes (`RetryCount=2`)
When a 3ª tentativa também falha
Then `RetryCount=3`, `Status=Failed`, `CanRetry=false` (não mais elegível para reprocessamento automático).

**CA17 — Nenhum item pendente não falha o job**
Given nenhum item `Scheduled` vencido nem `Failed` com `CanRetry=true`
When `PublisherJob.ExecuteAsync()` é executado
Then o job conclui sem erro e sem efeitos colaterais.

## TelegramPublisher — mídia

**CA18 — Publicação de vídeo**
Given um item com `MediaType=Video` e `MediaLocalPath` preenchido
When `TelegramPublisher.PublishAsync` é chamado
Then é feito `POST .../sendVideo` com `chat_id`, arquivo multipart, `caption`, `parse_mode=HTML`.

**CA19 — Publicação de imagem**
Given um item com `MediaType=Image` e `MediaLocalPath` preenchido
When `TelegramPublisher.PublishAsync` é chamado
Then é feito `POST .../sendPhoto` com `chat_id`, arquivo multipart, `caption`, `parse_mode=HTML`.

**CA20 — Fallback para MediaUrl**
Given `MediaLocalPath` nulo e `MediaUrl` preenchida
When `TelegramPublisher.PublishAsync` é chamado
Then a mídia é enviada a partir de `MediaUrl` (upload remoto/URL, conforme suporte da API do Telegram).

**CA21 — Sem mídia disponível**
Given `MediaLocalPath` e `MediaUrl` ambos nulos
When `TelegramPublisher.PublishAsync` é chamado
Then a publicação é feita apenas com legenda em texto, e um log Warning é registrado.

**CA22 — Credenciais ausentes**
Given `app_settings.telegram.bot_token` ou `telegram.channel_id` ausentes/vazios
When `TelegramPublisher.PublishAsync` é chamado
Then a publicação falha de forma tratada (exceção capturada pelo `PublisherJob`, item marcado `Failed`).

## Hangfire

**CA23 — Dashboard bloqueado com senha vazia**
Given `app_settings.hangfire.dashboard_password` vazio
When a aplicação inicia
Then um log Warning é emitido orientando a configurar a senha, e o acesso a `/hangfire` é bloqueado (`HangfireAuthFilter` nega).

**CA24 — Dashboard acessível com senha configurada**
Given `app_settings.hangfire.dashboard_password` preenchido
When uma requisição a `/hangfire` é feita com a senha correta
Then o acesso é permitido.

**CA25 — Recurring jobs registrados**
Given a aplicação inicializada
When o dashboard `/hangfire` é consultado
Then `CollectorJob` e `PublisherJob` aparecem como recurring jobs, com os crons de `app_settings.schedule.collector_cron` / `schedule.publisher_cron` (ou defaults `0 6 * * *` / `0 9,12,15,18,20 * * *`).

## Validação end-to-end

**CA26 — Fluxo completo via Docker Compose**
Given o ambiente subido via `docker compose up -d`
When o operador dispara `CollectorJob` manualmente pelo dashboard
Then produtos são salvos em `products`, a fila `publication_queue` é populada ao encadear o `ProcessorJob`, e ao disparar o `PublisherJob` a mensagem chega no canal Telegram de teste.
