# Especificação técnica — ISSUE-9: Publisher Instagram (Meta Graph API)

## Contratos de API (Meta Graph API)

### 1. Criação do container de mídia
`POST https://graph.facebook.com/v21.0/{ig-user-id}/media`
Body (form ou querystring, seguindo o padrão já usado pelo `TelegramPublisher`/`YoutubePublisher`
de `FormUrlEncodedContent`):
```
media_type=REELS
video_url={URL_HTTPS_PUBLICA}
caption={LEGENDA_COM_DISCLOSURE}
access_token={ACCESS_TOKEN}
```
Resposta 200: `{ "id": "{creation-id}" }`. Não-2xx → falha imediata (sem tentar polling),
`ErrorMessage` com o corpo/status da resposta.

### 2. Polling de status
`GET https://graph.facebook.com/v21.0/{creation-id}?fields=status_code&access_token={ACCESS_TOKEN}`
- Intervalo fixo: 3 segundos (`Task.Delay`, sem backoff — igual ao padrão simples já usado no
  projeto, timeout controla o teto).
- Timeout total: 2 minutos, via `CancellationTokenSource.CancelAfter` (mesmo padrão do
  `TotalTimeout` do `YoutubePublisher`, adaptado para 2min em vez de 15min).
- `status_code` possíveis: `IN_PROGRESS` (continua polling), `FINISHED` (prossegue para etapa
  3), `FAILED`/`ERROR` (falha imediata, CA4), `EXPIRED` (tratar como falha).
- Timeout sem `FINISHED` → `Failed` com retry (CA5) — **diferente** do padrão "sem retry" usado
  para token/mídia ausente; aqui é `RegisterAttempt(false, ...)` simples (uma tentativa), não
  `FailPermanently`.

### 3. Publicação do container
`POST https://graph.facebook.com/v21.0/{ig-user-id}/media_publish`
Body: `creation_id={creation-id}&access_token={ACCESS_TOKEN}`
Resposta 200 com `{ "id": "{media-id}" }` → sucesso (`true`). Não-2xx → falha com
`ErrorMessage` descritivo.

### 4. Renovação de token (`fb_exchange_token`)
`GET https://graph.facebook.com/v21.0/oauth/access_token?grant_type=fb_exchange_token&client_id={APP_ID}&client_secret={APP_SECRET}&fb_exchange_token={CURRENT_TOKEN}`
Resposta 200: `{ "access_token": "...", "expires_in": <segundos> }` → persistir
`instagram.access_token` e `instagram.token_expires_at` (`DateTime.UtcNow + expires_in segundos`)
em `app_settings`, mesmo padrão `UpsertAppSettingAsync` do `YoutubePublisher`.
Falha (rede, HTTP não-2xx, resposta sem `access_token`) → retorna `null`, chamador aciona
`FailPermanently` + persiste `instagram.token_invalid=true` (CA14).

> Nota: `instagram.client_id`/`instagram.client_secret` (App ID/Secret da Meta App) precisam
> existir em `app_settings` para a renovação — mesmo padrão de credenciais do YouTube
> (`youtube.client_id`/`youtube.client_secret`). Chaves sugeridas:
> `instagram.app_id`, `instagram.app_secret`, `instagram.access_token`, `instagram.page_id`
> (ig-user-id), `instagram.token_expires_at`, `instagram.token_invalid`.

## Schema de dados
Nenhuma migration nova necessária — reaproveita:
- `app_settings` (chave/valor genérico, já existente desde a Issue #2) para as chaves acima.
- `Product.MediaType`, `Product.MediaLocalPath`, `Product.MediaUrl`, `Product.AiCaption`
  (já existentes, Issue #6).
- `PublicationQueue.RegisterAttempt(bool, string?)` / `RetryCount` / `ErrorMessage` (já
  existentes, Issue #7).

## Padrões obrigatórios (aditivos, seguindo #7/#8)
1. **`ISocialPublisher`**: `InstagramPublisher.Network => SocialNetwork.Instagram`;
   `PublishAsync(PublicationQueue item, CancellationToken ct)`.
2. **`FailPermanently`** (idêntico ao do `YoutubePublisher`, duplicar o helper privado — os
   publishers não compartilham base class hoje, manter consistência com o padrão já
   estabelecido em vez de introduzir uma abstração nova fora de escopo): usado em (a) produto
   sem vídeo (fallback de segurança, CA15) e (b) falha de renovação de token (CA14).
3. **Timeout de polling ≠ `FailPermanently`**: timeout de 2min (CA5) usa `RegisterAttempt`
   simples (permite retry até `RetryCount < 3`), diferente dos dois casos acima que esgotam o
   retry imediatamente. Não confundir os dois padrões de falha.
4. **Resolução de mídia pública** (URL, não download): diferente do `YoutubePublisher` (que
   baixa o arquivo para upload binário), o Instagram consome `video_url` — não é necessário
   `IMediaStorage.DownloadAsync`. Regra:
   - `MediaLocalPath` preenchido → montar URL pública `{BaseUrl}/media/{Path.GetFileName(MediaLocalPath)}`
     (`BaseUrl` vindo de `app_settings` `api.public_base_url` ou config já existente —
     confirmar chave com o Dev; se não existir, criar `api.public_base_url` em `app_settings`).
   - `MediaLocalPath` nulo → fallback `Product.MediaUrl` (URL original, assumida pública).
   - Nenhum dos dois → falha `false` sem tentar criar container (CA8).
5. **`UseStaticFiles`**: confirmado por leitura direta de `Program.cs` que **NÃO está
   configurado** hoje. Adicionar:
   ```csharp
   app.UseStaticFiles(new StaticFileOptions
   {
       FileProvider = new PhysicalFileProvider(mediaRootPath), // mesmo root usado por LocalMediaStorage
       RequestPath = "/media",
   });
   ```
   posicionado antes de `app.UseHangfireDashboard` (ordem de middleware). Confirmar com o Dev o
   path raiz usado por `LocalMediaStorage` (`backend/src/AfiliadoBot.Infrastructure/Storage/LocalMediaStorage.cs`)
   para garantir que `RequestPath=/media` aponta para o mesmo diretório físico de onde
   `MediaLocalPath` é salvo.
6. **Disclosure (`#publi`/`#publicidade`)** — decisão registrada: **pós-processamento isolado
   dentro do `InstagramPublisher`**, NÃO alteração do prompt/serviço `ClaudeAiService`.
   Justificativa:
   - `GenerateCaptionAsync` é compartilhado por todas as redes (Telegram, YouTube, Facebook,
     Instagram) — qualquer mudança no prompt/lógica de pós-processamento ali tem risco de
     regressão nas demais redes já em produção.
   - CA10/CA11 exigem uma **garantia determinística** ("independentemente do que a IA gerou") —
     depender do LLM seguir a instrução do prompt não é uma garantia, é uma tendência. Uma
     checagem de string pura (regex/`Contains`, case-insensitive) no `InstagramPublisher` é
     100% determinística e testável sem mock de IA.
   - Isolamento: a lógica de disclosure vive inteiramente no publisher específico, sem tocar
     código usado por outras redes — menor superfície de risco, consistente com o racional de
     risco BAIXO já usado para o fix do `ProcessorJob`.
   Implementação sugerida: método privado `AppendDisclosureIfMissing(string caption)` — regex
   case-insensitive por `#publi\b` ou `#publicidade\b`; se nenhuma ocorrência, anexar
   `" #publi"` ao final; se já existir (qualquer uma das duas), não duplicar (CA11).
7. **`FailPermanently` fallback sem vídeo**: mensagem padronizada, ex.
   `"Produto sem mídia de vídeo, não aplicável ao Instagram"` (mesmo padrão do
   `NoVideoErrorMessage` do `YoutubePublisher`).

## Fix retroativo no `ProcessorJob`
Generalizar a condição existente (linha ~247 de `ProcessorJob.cs`):
```csharp
// Antes:
if (network == SocialNetwork.Youtube && !HasVideoAvailable(product))

// Depois:
if ((network == SocialNetwork.Youtube || network == SocialNetwork.Instagram) && !HasVideoAvailable(product))
```
`HasVideoAvailable(product)` já existe (método privado estático, linha ~270) e é
network-agnóstico — reaproveitar sem duplicar lógica. Nenhuma outra alteração necessária no
`ProcessorJob`.

## Testes obrigatórios (mock da Meta Graph API / `HttpClient`, sem chamadas reais)
Seguir o padrão de `YoutubePublisherTests.cs` (mock de `HttpMessageHandler`/`HttpClient`
injetado). Cobrir no mínimo: CA1-CA20 do `criterios-aceite.md`, exceto CA20 (validação manual
em conta real, fora do CI). Teste de regressão explícito no `ProcessorJobTests` (ou equivalente)
para CA18 (Telegram/Youtube/TikTok/Facebook inalterados).

## CA20 — pré-requisito de execução (fora do código)
Validação em conta real do Instagram exige credenciais reais de teste (`instagram.access_token`,
`instagram.app_id`, `instagram.app_secret`, `instagram.page_id`) já provisionadas pelo Gerente
via onboarding manual do App Meta (confirmado no Gate 1). O Dev deve solicitar essas credenciais
ao Gerente antes de fechar a sub-issue — sem elas, CA20 não pode ser executado e o Gate 2 não
pode ser solicitado.
