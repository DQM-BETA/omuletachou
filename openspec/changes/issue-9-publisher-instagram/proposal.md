# Proposal — ISSUE-9: Publisher Instagram (Meta Graph API)

## Objetivo
Implementar `InstagramPublisher : ISocialPublisher` para publicar automaticamente Reels (vídeo) de produtos aprovados no Instagram Business/Creator via Meta Graph API, incluindo: fluxo assíncrono de 3 etapas (criar container de mídia → polling de status → publicar), servir a mídia em URL pública própria (sem CDN externa), renovação automática de token de longa duração (60 dias) e disclosure legal obrigatório na legenda (`#publi`/`#publicidade`). Corrigir, em paralelo, o `ProcessorJob` (Issue #6, já em produção) para não enfileirar a rede Instagram quando o produto não tiver mídia de vídeo — mesmo padrão do fix retroativo já aplicado para YouTube (Issue #8, CA17–CA19).

## Usuários afetados
- Operação/Marketing da omuletachou: depende da publicação automática ampliar alcance de tráfego de afiliados no Instagram.
- Operador responsável pelo onboarding manual do token Meta (gera o token de longa duração inicial e monitora o alerta de token inválido no dashboard).
- Time de suporte/ops que monitora falhas de publicação (`PublicationQueue.Status = Failed`).
- Indiretamente, a área jurídica/compliance — o disclosure `#publi`/`#publicidade` é proteção legal obrigatória (CONAR) para o operador do canal.

## Casos de uso principais
1. Item de fila (`PublicationQueue`) com `SocialNetwork = Instagram` e produto com vídeo disponível é processado pelo `PublisherJob` → `InstagramPublisher.PublishAsync` executa o fluxo de 3 etapas (create container → polling → publish) → Reel publicado no perfil Instagram Business/Creator configurado.
2. Token de acesso com menos de 7 dias de validade → `InstagramPublisher` renova via `GET /oauth/access_token` (`fb_exchange_token`) antes de publicar, persiste novo `access_token`/`token_expires_at` em `app_settings`, prossegue sem intervenção manual.
3. `ProcessorJob` monta a fila de publicação de um produto: para a rede Instagram, só cria a entrada de `PublicationQueue` se o produto tiver `MediaType == "video"` (considerando `MediaLocalPath` ou `MediaUrl` como fonte). As demais redes seguem a regra atual, inalterada.
4. Legenda gerada via `GenerateCaptionAsync` (Claude) com tom Instagram (emocional, 3-5 hashtags, CTA, máx. 300 chars), com `#publi`/`#publicidade` anexado automaticamente ao final — não fica a critério da IA omitir.

## Casos de exceção
1. **Renovação de token falha** (expirado/revogado): item marcado `Failed` sem retry, `instagram.token_invalid = true` em `app_settings`, alerta no dashboard (fora do escopo desta issue implementar o consumo do alerta).
2. **URL de mídia pública inacessível** (falha ao servir via `/app/media` e `MediaUrl` de fallback também inválida): `PublishAsync` retorna `false` com mensagem de erro clara.
3. **Polling retorna `status_code = FAILED` ou não atinge `FINISHED` em 2 minutos** (timeout): item marcado `Failed`, retry no próximo ciclo do `PublisherJob` (respeitando `RetryCount` até 3).
4. **Produto sem vídeo chega ao `InstagramPublisher` mesmo assim** (cenário legado/falha de regra, análogo ao CA16 do YouTube): falha sem retry, com `ErrorMessage` descritivo — fallback de segurança, o caminho normal é o `ProcessorJob` nunca enfileirar esse item.
5. **Rate limit (HTTP 429)**: segue o padrão de retry já existente no `PublisherJob` com backoff; não há tratamento especial adicional (volume de 5 posts/dia por produto está bem abaixo do limite de 25/24h).

## Regras de negócio
- Escopo restrito a Reels (vídeo) — feed image, carousel e stories ficam fora do escopo.
- Credenciais em `app_settings`: `instagram.access_token`, `instagram.page_id`, `instagram.token_expires_at`, `instagram.token_invalid`.
- Renovação automática: verificar validade a cada execução com margem de 7 dias; se `token_expires_at - now < 7 dias`, renovar via `GET /oauth/access_token?grant_type=fb_exchange_token` antes de publicar.
- Fluxo de publicação (3 etapas obrigatórias, síncronas em sequência):
  1. `POST /{ig-user-id}/media` com `media_type=REELS`, `video_url={url_https_publica}`, `caption`.
  2. Polling `GET /{creation-id}?fields=status_code` a cada 3 segundos, timeout total de 2 minutos, até `status_code = FINISHED`.
  3. `POST /{ig-user-id}/media_publish` com `creation_id`.
- Mídia servida via `UseStaticFiles` do ASP.NET Core (`/app/media/{filename}` → `https://api.omuletachou.com.br/media/{filename}`); fallback para `MediaUrl` original se `MediaLocalPath` for nulo. Retenção indefinida (débito técnico registrado, fora de escopo).
- Caption: `GenerateCaptionAsync` (Claude) com tom Instagram, `#publi`/`#publicidade` anexado automaticamente ao final da legenda gerada (regra obrigatória de disclosure, não pode ser omitida pela IA).
- Retry: `RetryCount` até 3 via `PublisherJob`, mesmo padrão dos demais publishers. Sem tratamento especial de rate limit além de backoff em 429.

## Correção retroativa no `ProcessorJob` (Issue #6, já em produção)
- **Escopo:** em `CreatePublicationQueueEntriesAsync`, estender a condição já introduzida para `SocialNetwork.Youtube` (Issue #8, CA17) para também cobrir `SocialNetwork.Instagram`: pular a criação da entrada de `PublicationQueue` (e a chamada de `GenerateCaptionAsync` para essa rede) se `product.MediaType != "video"`.
- **Natureza da mudança:** aditiva e isolada — reaproveita o mesmo padrão de filtro por rede já em produção para Youtube; não altera a lógica das demais redes (Telegram, TikTok, Facebook).
- **Risco avaliado:** BAIXO, mesmo racional do CA19 da Issue #8 — exige teste de regressão explícito confirmando que Telegram/TikTok/Facebook permanecem inalterados.

## Integrações externas
- Meta Graph API (Instagram Content Publishing) — `POST /{ig-user-id}/media`, `GET /{creation-id}`, `POST /{ig-user-id}/media_publish`.
- Meta OAuth (`GET /oauth/access_token`, `grant_type=fb_exchange_token`) para renovação de token.
- Sem CDN externa — mídia servida pelo próprio backend ASP.NET Core.

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: .NET 8 + Meta Graph API + `HttpClient` (sem ambiguidade de stack).
- Validação em conta real do Instagram é **obrigatória** antes do Gate 2 (não aceita apenas mock/análise de código).

## Definição de pronto
- `InstagramPublisher` implementado e registrado no DI, seguindo o padrão estrutural de `TelegramPublisher`/`YoutubePublisher`.
- Fluxo de 3 etapas (create → polling → publish) coberto por teste com mock da Meta Graph API.
- Renovação automática de token (e falha de renovação) coberta por teste.
- Disclosure obrigatório (`#publi`/`#publicidade`) anexado automaticamente e coberto por teste.
- Correção no `ProcessorJob` coberta por teste de regressão (demais redes inalteradas) + teste do novo comportamento para Instagram.
- Testes unitários com mock — `dotnet test` verde, sem chamadas reais à API.
- **Validação em conta real do Instagram**: Reel de teste publicado com sucesso na conta Creator configurada, confirmado visualmente no perfil, com legenda e disclosure corretos — obrigatório antes do Gate 2.
