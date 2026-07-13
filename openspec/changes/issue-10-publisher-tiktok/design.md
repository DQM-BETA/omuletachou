# Design resumido — ISSUE-10: Publisher TikTok (Content Posting API)

> Escrito pelo Líder Técnico (sem escalada ao Arquiteto — PM Fase 2 avaliou sem ambiguidade
> arquitetural). Resumo estrutural; decisões de negócio já fechadas em `prd.md`/`criterios-aceite.md`.

## Visão geral
`TikTokPublisher : ISocialPublisher` — mesmo contrato dos publishers existentes (`TelegramPublisher`,
`YoutubePublisher`, `InstagramPublisher`), registrado no DI como mais uma implementação de
`IEnumerable<ISocialPublisher>`. Nenhuma mudança no `PublisherJob` (orquestração genérica já
existente). Fluxo `FILE_UPLOAD` de 3 etapas contra a TikTok Content Posting API v2, via `HttpClient`
direto (sem SDK), replicando o padrão HTTP manual já usado em `YoutubePublisher` (upload resumable)
e `InstagramPublisher` (polling assíncrono).

## Componentes envolvidos
- `AfiliadoBot.Infrastructure.Integrations.Social.TikTokPublisher` (novo)
- `AfiliadoBot.Infrastructure.Integrations.Social.SocialDisclosureHelper` (novo, compartilhado —
  ver decisão de reuso abaixo)
- `AfiliadoBot.Infrastructure.Media.Mp4DurationReader` (novo, ver decisão de duração abaixo)
- Migration `SeedTikTokCredentials` (novo `app_settings`)
- DI: registro do `TikTokPublisher` (`Program.cs`/`DependencyInjection.cs`, mesmo ponto onde os
  outros publishers são registrados)

Sem UI/mobile envolvido — 100% backend.

## Fluxo de dados
1. `PublisherJob` seleciona item `PublicationQueue` com `SocialNetwork = TikTok` e chama
   `TikTokPublisher.PublishAsync(item, ct)`.
2. Carrega credenciais de `app_settings` (`tiktok.access_token`, `tiktok.client_key`,
   `tiktok.client_secret`, `tiktok.refresh_token`, `tiktok.privacy_level`,
   `tiktok.min_duration_seconds`, `tiktok.max_duration_seconds`).
3. Resolve a mídia local do vídeo (mesmo padrão de `YoutubePublisher.ResolveMediaSourceAsync`:
   usa `Product.MediaLocalPath` se existir em disco, senão baixa `Product.MediaUrl` via
   `IMediaStorage` para um arquivo temporário).
4. Lê a duração do vídeo local (`Mp4DurationReader`) e valida contra
   `min_duration_seconds`/`max_duration_seconds` — fora do intervalo, falha sem retry (CA7/CA8),
   antes de qualquer chamada à API.
5. Monta a legenda final via `SocialDisclosureHelper.AppendIfMissing(caption, "#publi")`.
6. Etapa 1 — `POST /v2/post/publish/video/init/` (com retry 429) → `upload_url` + `publish_id`.
7. Etapa 2 — `PUT {upload_url}` em chunks (mesmo tamanho de chunk usado no `YoutubePublisher`,
   8MB) com `Content-Range`/`Content-Length` (com retry 429 por chunk).
8. Etapa 3 — polling `POST /v2/post/publish/status/fetch/` a cada 15s, timeout total 10min, até
   `status = PUBLISH_COMPLETE` (ou `FAILED`/timeout).
9. 401 em qualquer chamada → `RefreshAccessTokenAsync` via `tiktok.refresh_token`
   (`grant_type=refresh_token`), persiste novo `access_token`/`refresh_token` em `app_settings`,
   repete a chamada original — mesmo padrão de `YoutubePublisher.RefreshAccessTokenAsync`.

## Decisões técnicas documentadas (pedidas no escopo)

### 1. Duração do vídeo — sem lib externa
Não há biblioteca de metadata de vídeo no projeto (`Xabe.FFmpeg`/`NReco` exigiriam binário nativo
`ffmpeg` no container Docker/ARM — infraestrutura adicional fora do escopo desta sub-issue e do
papel do Dev). Decisão: implementar um leitor mínimo e dependency-free do átomo MP4 `moov/mvhd`
(`Mp4DurationReader.GetDurationSeconds(string filePath)`), que lê `timescale` e `duration` do
`mvhd` box (v0 ou v1) e calcula `duration / timescale` em segundos. Cobre o caso real do pipeline
(vídeos MP4 gerados/baixados pelo `ProcessorJob`). Se o parsing falhar (arquivo corrompido/formato
inesperado), tratar como duração desconhecida → falha sem retry com mensagem descritiva (mesma
categoria de erro do CA7/CA8, não é um novo caminho de exceção).

### 2. Disclosure duplo — extrair helper compartilhado (reuso, não duplicação)
A lógica de "anexar `#publi`/`#publicidade` ao final da legenda sem duplicar" hoje está duplicada
como método privado em `InstagramPublisher` (`AppendDisclosureIfMissing` + `DisclosureRegex`).
Decisão: extrair para `SocialDisclosureHelper` (classe estática, `Infrastructure/Integrations/Social/`)
com o mesmo regex e mesma assinatura comportamental, usado por `InstagramPublisher` (refatorado,
sem mudança de comportamento — os testes de regressão do Instagram devem continuar passando
inalterados) e `TikTokPublisher`. Reuso justificado: é a mesma regra determinística, mesmo
hashtag, mesma API pública — duplicar criaria drift de manutenção (correção futura no Instagram
não propagaria ao TikTok).

### 3. Retry em 429 — novo padrão local, sem Polly
Não existe Polly nem qualquer wrapper de retry HTTP no projeto hoje (`Telegram`/`YouTube`/
`Instagram` não tratam 429 explicitamente). Decisão: implementar um helper privado simples em
`TikTokPublisher` (`SendWithRetryAsync`, backoff fixo 2s/4s/8s, 3 tentativas, só para HTTP 429) —
não introduzir a dependência do Polly para um único publisher; se um quarto publisher precisar do
mesmo padrão futuramente, aí sim vale extrair. Aplicado nas 3 chamadas (init, PUT de cada chunk,
polling).

### 4. Credenciais / refresh de token
`tiktok.access_token` e `tiktok.open_id` já existem em `app_settings` (seed inicial, id 18/19).
Faltam: `tiktok.client_key`, `tiktok.client_secret`, `tiktok.refresh_token`, `tiktok.privacy_level`
(seed `SELF_ONLY`), `tiktok.min_duration_seconds` (seed `3`), `tiktok.max_duration_seconds` (seed
`600`) — nova migration `SeedTikTokCredentials`. A TikTok API v2 (Content Posting API) exige OAuth2
com renovação via `refresh_token` (`POST https://open.tiktokapis.com/v2/oauth/token/` com
`grant_type=refresh_token`), confirmado no PRD/CA16 — mesmo padrão de `YoutubePublisher`
(`RefreshAccessTokenAsync`, chamado proativamente em 401).

## Stack
.NET 8, `HttpClient` direto, EF Core (migration), xUnit + mocks de `HttpClient`
(`HttpMessageHandler` fake, mesmo padrão dos testes existentes de `YoutubePublisherTests`/
`InstagramPublisherTests`). Sem UI.
