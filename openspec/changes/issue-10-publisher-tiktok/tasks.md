# Tasks — ISSUE-10: Publisher TikTok (Content Posting API)

> Única sub-issue (escopo coeso, sem UI). Dev lê só este arquivo + a sub-issue no GitHub.
> Contexto técnico completo: `documentacoes/ISSUE-10-publisher-tiktok/especificacao-tecnica.md` e
> `openspec/changes/issue-10-publisher-tiktok/design.md`.

## T-01 — TikTokPublisher (Content Posting API, FILE_UPLOAD) + validação de duração + disclosure duplo + refresh de token

### O que fazer
1. Migration `SeedTikTokCredentials`: adicionar em `app_settings` — `tiktok.client_key`,
   `tiktok.client_secret`, `tiktok.refresh_token` (seed `""`), `tiktok.privacy_level` (seed
   `SELF_ONLY`), `tiktok.min_duration_seconds` (seed `3`), `tiktok.max_duration_seconds`
   (seed `600`). Ids seguintes ao maior já usado (41+). NÃO recriar `tiktok.access_token`/
   `tiktok.open_id` (já existem, ids 18/19).
2. `Mp4DurationReader` (`AfiliadoBot.Infrastructure/Media/`): leitor mínimo do átomo MP4
   `moov/mvhd` (v0 e v1), sem dependência externa — `TryGetDurationSeconds(path, out seconds)`.
3. `SocialDisclosureHelper` (`AfiliadoBot.Infrastructure/Integrations/Social/`): extrair de
   `InstagramPublisher` a lógica de anexar `#publi`/`#publicidade` sem duplicar
   (`AppendIfMissing(caption, hashtag = "#publi")` + regex compartilhado). Refatorar
   `InstagramPublisher` para usar o helper — **comportamento inalterado**, os testes de
   `InstagramPublisherTests` relacionados a disclosure devem continuar passando sem modificação.
4. `TikTokPublisher : ISocialPublisher` (`AfiliadoBot.Infrastructure/Integrations/Social/`):
   - `Network => SocialNetwork.TikTok`.
   - Carrega credenciais de `app_settings` (chaves da especificação técnica).
   - Resolve mídia local do vídeo (mesmo padrão `ResolveMediaSourceAsync` do `YoutubePublisher`
     — `MediaLocalPath` ou download via `IMediaStorage`).
   - Valida duração via `Mp4DurationReader` ANTES de qualquer chamada à API — fora do intervalo
     `[min_duration_seconds, max_duration_seconds]` → `FailPermanently` com
     `"Vídeo fora do intervalo de duração aceito pelo TikTok (Xs-Ymin)"` (X/Y = valores
     configurados, sem chamada de upload).
   - Monta legenda final via `SocialDisclosureHelper.AppendIfMissing(product.AiCaption, "#publi")`.
   - Etapa 1: `POST /v2/post/publish/video/init/` com `brand_content_toggle: true` (sempre),
     `privacy_level` de `app_settings`, `disable_duet: false`, `disable_comment: false` →
     captura `upload_url`/`publish_id`.
   - Etapa 2: `PUT {upload_url}` em chunks de 8MB com `Content-Range`/`Content-Length` corretos.
   - Etapa 3: polling `POST /v2/post/publish/status/fetch/` a cada 15s, timeout total 10min —
     `PUBLISH_COMPLETE` → sucesso; `FAILED` → falha imediata com `ErrorMessage` descritivo;
     timeout → `Failed` elegível a retry (respeita `RetryCount` até 3, não usar
     `FailPermanently` aqui).
   - 401 em qualquer chamada → `RefreshAccessTokenAsync` via `tiktok.refresh_token`
     (`POST /v2/oauth/token/`, `grant_type=refresh_token`), persiste novo
     `access_token`/`refresh_token` em `app_settings`, repete a chamada original. Falha ao
     renovar → `FailPermanently` com mensagem descritiva (mesmo padrão do `YoutubePublisher`).
   - 429 em qualquer chamada (init/PUT/polling) → `SendWithRetryAsync` local (3 tentativas,
     2s/4s/8s), esgotadas as tentativas → falha (elegível a retry padrão do `PublisherJob`, não
     `FailPermanently`).
   - Fallback de segurança: produto sem mídia de vídeo → `FailPermanently` com
     `"Produto sem mídia de vídeo, não aplicável ao TikTok"` (mesmo padrão dos demais).
5. Registrar `TikTokPublisher` no DI (mesmo ponto de registro dos demais `ISocialPublisher`).
6. Testes (`AfiliadoBot.Tests/Integrations/TikTokPublisherTests.cs`), mock de `HttpClient` via
   `HttpMessageHandler` fake, cobrindo os cenários abaixo — sem chamadas reais à API.

### Critérios de aceite (Given/When/Then — ver `criterios-aceite.md` para o texto completo)
- CA1 a CA5: fluxo de 3 etapas (init, upload chunked, polling), status `FAILED` e timeout de 10min.
- CA6 a CA10: validação de duração (dentro do intervalo, abaixo do mínimo, acima do máximo,
  limites parametrizáveis via `app_settings`, proporção/resolução não validadas).
- CA11 a CA13: disclosure duplo (`brand_content_toggle=true` sempre, `#publi` anexado, sem
  duplicar quando já presente).
- CA14 e CA15: `privacy_level` configurável (`SELF_ONLY` seed, `PUBLIC_TO_EVERYONE` manual).
- CA16: refresh automático de token em 401.
- CA17 e CA18: backoff exponencial em 429 (3 tentativas, 2s/4s/8s) e `RetryCount` até 3.
- CA19: suíte com mocks, sem chamadas reais à API do TikTok — `dotnet test` verde.
- **CA20: explicitamente NÃO bloqueante para o Gate 2 desta issue** (débito de validação real,
  decisão do Gerente no Gate 1) — não é responsabilidade do Dev resolver ou testar nesta rodada.

### Contexto técnico
- Design: `openspec/changes/issue-10-publisher-tiktok/design.md`
- Especificação técnica: `documentacoes/ISSUE-10-publisher-tiktok/especificacao-tecnica.md`
- PRD: `documentacoes/ISSUE-10-publisher-tiktok/prd.md`
- Critérios de aceite completos: `documentacoes/ISSUE-10-publisher-tiktok/criterios-aceite.md`
- Stack: .NET 8, `HttpClient` direto (sem SDK), EF Core (migration)
- Repo: DQM-BETA/omuletachou — branch base `desenv`
- Referências de padrão no código: `YoutubePublisher.cs` (upload resumable em chunks, refresh de
  token), `InstagramPublisher.cs` (polling assíncrono, disclosure — será refatorado para usar o
  helper compartilhado), `PublisherJob.cs` (orquestração — NÃO precisa mudar)
