# Proposal — ISSUE-10: Publisher TikTok (Content Posting API)

## Objetivo
Implementar `TikTokPublisher : ISocialPublisher` para publicar automaticamente vídeos de produtos aprovados no TikTok via Content Posting API, no modo `FILE_UPLOAD` (init → upload chunked via PUT → polling de status), com validação client-side de duração, disclosure duplo de conteúdo comercial (`brand_content_toggle` + hashtag `#publi`) e `privacy_level` configurável por ambiente (sandbox/teste vs. produção).

## Usuários afetados
- Operação/Marketing da omuletachou: depende da publicação automática ampliar alcance de tráfego de afiliados no TikTok.
- Operador responsável pelo onboarding do app no TikTok Developer Portal (acompanha aprovação do escopo `video.publish` e alterna manualmente `tiktok.privacy_level` de `SELF_ONLY` para `PUBLIC_TO_EVERYONE` quando o app for aprovado para produção).
- Time de suporte/ops que monitora falhas de publicação (`PublicationQueue.Status = Failed`).
- Indiretamente, a área jurídica/compliance — o disclosure (`brand_content_toggle` + `#publi`) é proteção legal obrigatória (CONAR) para o operador do canal.

## Casos de uso principais
1. Item de fila (`PublicationQueue`) com `SocialNetwork = TikTok` e vídeo válido (MP4/WebM, duração dentro do intervalo parametrizado) é processado pelo `PublisherJob` → `TikTokPublisher.PublishAsync` executa o fluxo de 3 etapas: (a) `POST /v2/post/publish/video/init/` com `title`, `privacy_level` (lido de `app_settings`), `brand_content_toggle = true`, `disable_duet: false`, `disable_comment: false` → retorna `upload_url` e `publish_id`; (b) `PUT {upload_url}` com os bytes do vídeo em chunks, headers `Content-Range` e `Content-Length`; (c) polling `POST /v2/post/publish/status/fetch/` com `publish_id` a cada 15s até `status = PUBLISH_COMPLETE` (timeout 10 min).
2. Legenda gerada via `GenerateCaptionAsync` (Claude) com `#publi` anexado automaticamente ao final — mesmo padrão determinístico já usado no Instagram (Issue #9), não dependente do LLM.
3. Validação de duração client-side: antes de iniciar o fluxo de upload, o `TikTokPublisher` verifica se a duração do vídeo está entre `tiktok.min_duration_seconds` e `tiktok.max_duration_seconds` (seed 3s/600s). Fora do intervalo, marca `Failed` sem retry e sem consumir chamada de upload.
4. Ambiente de desenvolvimento/teste usa conta sandbox/unaudited do TikTok Developer Portal (publica apenas para contas de teste cadastradas no app) com `tiktok.privacy_level = SELF_ONLY`. Quando o app for aprovado pelo TikTok para produção, o operador troca manualmente para `PUBLIC_TO_EVERYONE` em `app_settings` — sem necessidade de deploy.

## Casos de exceção
1. **Vídeo fora do intervalo de duração** (< `min_duration_seconds` ou > `max_duration_seconds`): item marcado `Failed` sem retry, com `ErrorMessage` "Vídeo fora do intervalo de duração aceito pelo TikTok (Xs-Ymin)".
2. **Polling retorna `status = FAILED`**: publicação falha com `ErrorMessage` descritivo, sem retry adicional além do padrão do `PublisherJob`.
3. **Polling não atinge `PUBLISH_COMPLETE` em 10 minutos** (timeout): item marcado `Failed`, elegível a retry no próximo ciclo do `PublisherJob` (respeitando `RetryCount` até 3).
4. **Rate limit (HTTP 429)** durante init ou upload: backoff exponencial (3 tentativas, 2s/4s/8s), consistente com o padrão já usado em Telegram/YouTube/Instagram. O limite de 6 publicações/minuto por `access_token` é bem acima do volume esperado do sistema — sem tratamento especial adicional além do backoff padrão.
5. **App ainda não aprovado para produção pelo TikTok** (aprovação de escopo `video.publish` pendente, prazo de 3-7 dias sem garantia): não bloqueia o desenvolvimento nem o Gate 2 desta issue — ver critério de validação real na Definição de Pronto abaixo.

## Regras de negócio
- Escopo: upload de vídeo via `FILE_UPLOAD` (init + PUT chunked + polling). `PULL_FROM_URL` fora de escopo.
- Credenciais/config em `app_settings`: `tiktok.client_key`, `tiktok.client_secret`, `tiktok.access_token`, `tiktok.privacy_level` (default `SELF_ONLY`), `tiktok.min_duration_seconds` (default 3), `tiktok.max_duration_seconds` (default 600).
- Fluxo de publicação (3 etapas obrigatórias, síncronas em sequência):
  1. `POST /v2/post/publish/video/init/` com `title`, `privacy_level` (de `app_settings`), `brand_content_toggle: true`, `disable_duet: false`, `disable_comment: false` → retorna `upload_url` e `publish_id`.
  2. `PUT {upload_url}` com bytes do vídeo em chunks, headers `Content-Range: bytes 0-{size-1}/{size}` e `Content-Length: {size}`.
  3. Polling `POST /v2/post/publish/status/fetch/` com `publish_id` a cada 15s, timeout total de 10 minutos, até `status = PUBLISH_COMPLETE`.
- Validação client-side de duração (3s-10min, parametrizável) obrigatória antes de iniciar o upload — proporção e resolução não precisam de validação prévia (TikTok adapta a exibição automaticamente).
- Disclosure duplo, determinístico (não depende da IA): `brand_content_toggle = true` no payload do `/video/init` (classifica como conteúdo comercial de marca) + hashtag `#publi` anexada automaticamente ao final da legenda gerada pelo `GenerateCaptionAsync`.
- Refresh automático de `access_token`: detectar 401 → renovar via `refresh_token` → persistir novo token em `app_settings` → repetir a chamada.
- Retry: backoff exponencial em 429 (3 tentativas, 2s/4s/8s), `RetryCount` até 3 via `PublisherJob`, mesmo padrão dos demais publishers.

## Integrações externas
- TikTok Content Posting API (`v2/post/publish/video/init/`, upload endpoint via PUT chunked, `v2/post/publish/status/fetch/`).
- TikTok OAuth2 (refresh de `access_token` via `refresh_token`).

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: .NET 8 + TikTok Content Posting API + `HttpClient` (sem ambiguidade de stack — fluxo de upload chunked estruturalmente similar ao já implementado para YouTube, Issue #8).
- Aprovação do app TikTok for Developers para acesso pleno à Content Posting API pode levar de 3 a 7 dias, sem garantia de estar concluída antes do desenvolvimento. **Isso NÃO bloqueia o Gate 2 desta issue** (decisão explícita do Gerente no Gate 1, diferente da Issue #9 onde essa dispensa só ocorreu manualmente no próprio Gate 2).

## Definição de pronto
- `TikTokPublisher` implementado e registrado no DI, seguindo o padrão estrutural de `TelegramPublisher`/`YoutubePublisher`/`InstagramPublisher`.
- Fluxo de 3 etapas (init → upload chunked → polling) coberto por teste com mock da TikTok Content Posting API.
- Validação de duração (dentro e fora do intervalo parametrizado) coberta por teste.
- Disclosure duplo (`brand_content_toggle` + `#publi`) coberto por teste.
- Refresh automático de token (e falha de renovação) coberto por teste.
- Testes unitários com mock — `dotnet test` verde, sem chamadas reais à API.
- Desenvolvimento e testes executados contra conta sandbox/unaudited do TikTok Developer Portal, com `tiktok.privacy_level = SELF_ONLY` (publicação restrita a contas de teste cadastradas).
- **Validação em conta real de produção do TikTok**: registrada como débito de validação pendente (equivalente ao CA20 da Issue #9), condicionada à aprovação do app pelo TikTok — **explicitamente NÃO bloqueante para o Gate 2 desta vez** (decisão do Gerente no Gate 1, ver critério de aceite dedicado em `criterios-aceite.md`).
