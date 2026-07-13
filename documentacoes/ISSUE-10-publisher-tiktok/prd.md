# PRD — ISSUE-10: Publisher TikTok (Content Posting API)

## Status
Fechado (PM Fase 2) — requisitos consolidados com as respostas do Gate 1 (Gerente, https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959489840). Pronto para refinamento técnico (Líder Técnico).

## Objetivo
Implementar `TikTokPublisher : ISocialPublisher` para publicar automaticamente vídeos de produtos aprovados no TikTok via Content Posting API, no modo `FILE_UPLOAD` (init → upload chunked via PUT → polling de status), com validação client-side de duração e disclosure duplo de conteúdo comercial (`brand_content_toggle` + hashtag `#publi`), e `privacy_level` configurável (`SELF_ONLY` em dev/teste, `PUBLIC_TO_EVERYONE` alternado manualmente pelo operador após aprovação do app em produção).

## Contexto / dependências
- Depende das Issues #6 (`ProcessorJob`), #7 (`PublisherJob`/Hangfire + `TelegramPublisher` como padrão de referência), #8 (`YoutubePublisher` — padrão de upload resumable em chunks, estruturalmente similar ao `FILE_UPLOAD` do TikTok) e #9 (`InstagramPublisher` — padrão de disclosure via hashtag e fluxo assíncrono de 3 etapas), todas em produção (branch `main`).
- `PublisherJob` já resolve o publisher pela rede via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork` — a integração do `TikTokPublisher` é **aditiva**: basta registrar no DI, sem alterações no `PublisherJob`.
- `SocialNetwork.TikTok` já existe no enum, inclusive referenciado em testes de regressão anteriores (Issue #9, CA18).

## Usuários afetados
- Operação/Marketing da omuletachou: depende da publicação automática ampliar alcance de tráfego de afiliados no TikTok.
- Operador responsável pelo onboarding do app no TikTok Developer Portal — acompanha a aprovação do escopo `video.publish` e alterna manualmente `tiktok.privacy_level` de `SELF_ONLY` para `PUBLIC_TO_EVERYONE` quando o app for aprovado para produção.
- Time de suporte/ops que monitora falhas de publicação (`PublicationQueue.Status = Failed`).
- Área jurídica/compliance — o disclosure duplo (`brand_content_toggle` + `#publi`) é proteção legal (CONAR) obrigatória, não opcional.

## Casos de uso principais
1. Item de fila (`PublicationQueue`) com `SocialNetwork = TikTok` e vídeo válido é processado pelo `PublisherJob` → `TikTokPublisher.PublishAsync` executa:
   a. `POST /v2/post/publish/video/init/` com `title`, `privacy_level` (de `app_settings`), `brand_content_toggle: true`, `disable_duet: false`, `disable_comment: false` → retorna `upload_url` e `publish_id`;
   b. `PUT {upload_url}` com bytes do vídeo em chunks, headers `Content-Range` e `Content-Length`;
   c. Polling `POST /v2/post/publish/status/fetch/` a cada 15s, timeout total de 10 minutos, até `status = PUBLISH_COMPLETE`.
2. Validação de duração client-side (3s-10min, parametrizável via `tiktok.min_duration_seconds`/`tiktok.max_duration_seconds`) executada antes de iniciar o upload — evita gastar uma chamada de init/upload para um vídeo que a API rejeitaria de qualquer forma.
3. Legenda gerada via `GenerateCaptionAsync` (Claude) com `#publi` anexado automaticamente ao final — mesmo padrão determinístico do Instagram (Issue #9), não dependente do LLM. Combinado com `brand_content_toggle = true` no payload da API, forma o disclosure duplo exigido para conteúdo de afiliado.
4. Ambiente de desenvolvimento/teste roda contra conta sandbox/unaudited do TikTok Developer Portal (publica apenas para contas de teste cadastradas no app), com `tiktok.privacy_level = SELF_ONLY`. Quando o app for aprovado pelo TikTok para produção, o operador troca manualmente para `PUBLIC_TO_EVERYONE` — sem deploy.
5. **Débito de validação (definição de pronto, explicitamente NÃO bloqueante para o Gate 2 desta vez):** publicar um vídeo de teste na conta TikTok real assim que o app for aprovado, confirmar visualmente a publicação e o disclosure aplicado. Diferente da Issue #9, essa validação já nasce marcada como não-bloqueante — decisão explícita do Gerente no Gate 1.

## Casos de exceção
1. **Vídeo fora do intervalo de duração** (< `min_duration_seconds` ou > `max_duration_seconds`): item marcado `Failed` sem retry, `ErrorMessage` "Vídeo fora do intervalo de duração aceito pelo TikTok (Xs-Ymin)", sem consumir chamada de upload.
2. **Polling retorna `status = FAILED`**: publicação falha com `ErrorMessage` descritivo.
3. **Polling não atinge `PUBLISH_COMPLETE` em 10 minutos** (timeout): item marcado `Failed`, retry no próximo ciclo do `PublisherJob` (respeitando `RetryCount` até 3).
4. **Access token expirado (401)**: renovado automaticamente via `refresh_token`, novo token persistido em `app_settings`, chamada original repetida.
5. **Rate limit (HTTP 429)**: backoff exponencial (3 tentativas, 2s/4s/8s), mesmo padrão de Telegram/YouTube/Instagram. Limite de 6 publicações/minuto por `access_token` está bem acima do volume esperado — sem tratamento especial adicional.
6. **App ainda não aprovado para produção** (aprovação de escopo `video.publish` pendente, prazo de 3-7 dias sem garantia): não bloqueia o desenvolvimento nem o Gate 2 — ver CA20 em `criterios-aceite.md`.

## Regras de negócio
- Escopo: `FILE_UPLOAD` (init + PUT chunked + polling). `PULL_FROM_URL` fora de escopo.
- Credenciais/config em `app_settings`: `tiktok.client_key`, `tiktok.client_secret`, `tiktok.access_token`, `tiktok.privacy_level` (default `SELF_ONLY`), `tiktok.min_duration_seconds` (default 3), `tiktok.max_duration_seconds` (default 600).
- Fluxo de publicação — 3 etapas obrigatórias, sequenciais:
  1. `POST /v2/post/publish/video/init/` — `title`, `privacy_level`, `brand_content_toggle: true`, `disable_duet: false`, `disable_comment: false`.
  2. `PUT {upload_url}` — chunks com `Content-Range`/`Content-Length`.
  3. Polling `POST /v2/post/publish/status/fetch/` — intervalo 15s, timeout total 10min.
- Validação de duração client-side obrigatória (3s-10min, parametrizável) — proporção/resolução não precisam de validação prévia.
- Disclosure duplo, determinístico: `brand_content_toggle = true` no `/video/init` + `#publi` anexado automaticamente ao final da legenda gerada.
- Refresh automático de `access_token` em 401 via `refresh_token`, persistido em `app_settings`.
- Retry: `RetryCount` até 3 via `PublisherJob`, backoff exponencial em 429 (3 tentativas, 2s/4s/8s), mesmo padrão dos demais publishers.

## Integrações externas
- TikTok Content Posting API: `POST /v2/post/publish/video/init/`, `PUT {upload_url}` (chunked), `POST /v2/post/publish/status/fetch/`.
- TikTok OAuth2 (refresh de `access_token` via `refresh_token`).

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: .NET 8 + TikTok Content Posting API + `HttpClient` — sem ambiguidade de stack. Fluxo de upload chunked estruturalmente similar ao já implementado para YouTube (Issue #8).
- Testes devem usar mocks da TikTok Content Posting API (sem chamadas reais em CI).
- Aprovação do app TikTok for Developers pode levar de 3 a 7 dias, sem garantia de estar concluída antes do desenvolvimento. **Isso NÃO bloqueia o Gate 2 desta issue** — decisão explícita do Gerente no Gate 1 (diferente da Issue #9, onde a dispensa equivalente só ocorreu manualmente já no Gate 2).

## Definição de pronto
- `TikTokPublisher` implementado e registrado no DI, seguindo o padrão estrutural de `TelegramPublisher`/`YoutubePublisher`/`InstagramPublisher`.
- Fluxo de 3 etapas (init → upload chunked → polling) coberto por teste com mock da TikTok Content Posting API, incluindo cenários de `FAILED` e timeout de 10min.
- Validação de duração (dentro e fora do intervalo parametrizado) coberta por teste.
- Disclosure duplo (`brand_content_toggle` + `#publi`) coberto por teste.
- Refresh automático de token (401 → renovação → repetição) coberto por teste.
- Testes unitários com mock — `dotnet test` verde, sem chamadas reais à API.
- Desenvolvimento e testes executados contra conta sandbox/unaudited do TikTok Developer Portal, com `tiktok.privacy_level = SELF_ONLY`.
- **Validação em conta real de produção do TikTok:** registrada como débito de validação pendente (CA20 em `criterios-aceite.md`), condicionada à aprovação do app pelo TikTok. **Explicitamente NÃO bloqueante para o Gate 2 desta issue** — decisão do Gerente no Gate 1, para não repetir a confusão da Issue #9 (onde essa dispensa só foi esclarecida manualmente no próprio Gate 2).
- Critérios de aceite (`criterios-aceite.md`) validados.

## Perguntas do Gate 1 — respondidas
Ver comentários "Levantamento de requisitos — Publisher TikTok (PM Fase 1)" (https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959140533) e "Respostas do Gerente — Gate 1" (https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959489840) na Issue #10. Todas as 6 perguntas foram respondidas e incorporadas às regras de negócio acima.

## Avaliação de ambiguidade arquitetural
**Sem ambiguidade arquitetural.** Stack, protocolo de integração (TikTok Content Posting API) e padrão de credenciais já estão definidos na Issue e nas respostas do Gate 1. O fluxo `FILE_UPLOAD` (init + PUT chunked + polling) é estruturalmente equivalente ao upload resumable já implementado para YouTube na Issue #8 — uma sequência de chamadas HTTP contra uma API externa já definida, sem nova dependência de infraestrutura, storage ou serviço externo além do `HttpClient` já usado pelos publishers existentes. O disclosure duplo (`brand_content_toggle` no payload da API + hashtag `#publi` na legenda) é uma novidade em relação ao Instagram (que usa só hashtag), mas trata-se de **decisão de negócio, não arquitetural**: é apenas mais um parâmetro determinístico no payload já existente do `/video/init`, sem impacto em componentes, camadas ou integrações novas. Segue direto para refinamento técnico do Líder Técnico, sem escalar ao Arquiteto.
