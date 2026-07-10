# PRD — ISSUE-9: Publisher Instagram (Meta Graph API)

## Status
Fechado (PM Fase 2) — requisitos consolidados com as respostas do Gate 1 (Gerente, https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4935151980). Pronto para refinamento técnico (Líder Técnico).

## Objetivo
Implementar `InstagramPublisher : ISocialPublisher` para publicar automaticamente Reels (vídeo) de produtos aprovados no Instagram Business/Creator via Meta Graph API, com fluxo assíncrono de 3 etapas (criar container de mídia → polling de status → publicar), mídia servida em URL pública própria e renovação automática do token de longa duração (60 dias). Corrigir, em paralelo, uma lacuna no `ProcessorJob` (Issue #6, já em produção) que hoje enfileira produtos para o Instagram mesmo sem vídeo disponível — mesmo fix já aplicado ao YouTube na Issue #8 (CA17–CA19).

## Contexto / dependências
- Depende das Issues #6 (`ProcessorJob` — download de mídia, `MediaLocalPath`/`MediaType`), #7 (`PublisherJob`/Hangfire + `TelegramPublisher` como padrão de referência) e #8 (`YoutubePublisher` — padrão de renovação de token e do fix de fila por tipo de mídia), todas em produção (branch `main`).
- `PublisherJob` já resolve o publisher pela rede via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork` — a integração do `InstagramPublisher` é **aditiva**: basta registrar no DI, sem alterações no `PublisherJob`.
- `SocialNetwork.Instagram` já existe no enum (`backend/src/AfiliadoBot.Domain/Enums/SocialNetwork.cs`).
- `Program.cs` precisa expor `app.UseStaticFiles` mapeando `/app/media` → `/media`, requisito de infraestrutura da própria API do Instagram (exige URL HTTPS pública para a mídia).

## Usuários afetados
- Operação/Marketing da omuletachou: depende da publicação automática ampliar alcance de tráfego de afiliados no Instagram.
- Operador responsável pelo onboarding manual do App Meta e do token de longa duração inicial, e por reconfigurar manualmente em caso de falha de renovação (alerta no dashboard de Settings).
- Time de suporte/ops que monitora falhas de publicação (`PublicationQueue.Status = Failed`).
- Área jurídica/compliance — o disclosure `#publi`/`#publicidade` é proteção legal (CONAR) obrigatória para o operador do canal, não opcional.

## Casos de uso principais
1. Item de fila (`PublicationQueue`) com `SocialNetwork = Instagram` e produto com vídeo disponível (`MediaLocalPath` ou, na ausência, `MediaUrl`) é processado pelo `PublisherJob` → `InstagramPublisher.PublishAsync` executa:
   a. `POST /{ig-user-id}/media` com `media_type=REELS`, `video_url={url_https_publica}`, `caption` (com disclosure já anexado);
   b. Polling `GET /{creation-id}?fields=status_code` a cada 3s, timeout total de 2 minutos, até `status_code = FINISHED`;
   c. `POST /{ig-user-id}/media_publish` com `creation_id`.
   Reel publicado no perfil configurado.
2. Token de acesso com menos de 7 dias de validade (`instagram.token_expires_at`) → `InstagramPublisher` renova via `GET /oauth/access_token?grant_type=fb_exchange_token` antes de publicar, persiste novo `access_token`/`token_expires_at` em `app_settings`, prossegue sem intervenção manual.
3. `ProcessorJob` monta a fila de publicação de um produto: para a rede Instagram, só cria a entrada de `PublicationQueue` se o produto tiver `MediaType == "video"` (considerando `MediaLocalPath` ou `MediaUrl` como fonte). As demais redes (Telegram, Youtube, TikTok, Facebook) seguem a regra atual, inalterada.
4. Legenda gerada via `GenerateCaptionAsync` (Claude) com prompt específico para Instagram (tom emocional, 3-5 hashtags, CTA forte, máx. 300 caracteres) — `#publi`/`#publicidade` anexado automaticamente ao final, sempre, independentemente do que a IA gerar.
5. **Validação em conta real (definição de pronto, obrigatória antes do Gate 2):** publicar um Reel de teste na conta Creator/Business configurada, confirmar visualmente que apareceu no perfil, e validar que a legenda com a hashtag de disclosure foi aplicada corretamente.

## Casos de exceção
1. **Renovação de token falha** (token já expirado ou revogado no momento da tentativa): item marcado `Failed` sem retry automático, `instagram.token_invalid = true` persistido em `app_settings`, alerta exibido na tela de Settings do dashboard orientando reconfiguração manual (consumo do alerta fora do escopo desta issue, mesmo padrão do YouTube).
2. **URL de mídia pública inacessível**: `PublishAsync` retorna `false` com mensagem de erro clara. Se `MediaLocalPath` for nulo, usa `MediaUrl` original como fallback (assumindo que a URL da plataforma de origem também é pública).
3. **Polling retorna `status_code = FAILED`**: publicação falha com `ErrorMessage` descritivo.
4. **Polling não atinge `FINISHED` em 2 minutos** (timeout): item marcado `Failed`, retry no próximo ciclo do `PublisherJob` (respeitando `RetryCount` até 3).
5. **Produto sem vídeo chega ao `InstagramPublisher` mesmo assim** (cenário legado/falha de regra, análogo ao CA16 da Issue #8): fallback de segurança — falha sem retry, com `ErrorMessage` descritivo. O caminho normal é o `ProcessorJob` nunca enfileirar esse item para Instagram.
6. **Rate limit (HTTP 429)**: segue o padrão de retry já existente no `PublisherJob` com backoff. Não há tratamento especial adicional — volume esperado (5 posts/dia por produto) está bem abaixo do limite de 25 publicações/24h da Graph API.

## Regras de negócio
- Escopo restrito a Reels (vídeo). Feed image, carousel e Stories ficam fora do escopo desta issue.
- Credenciais em `app_settings`: `instagram.access_token`, `instagram.page_id`, `instagram.token_expires_at`, `instagram.token_invalid`.
- Renovação: verificar validade a cada execução do `InstagramPublisher` com margem de 7 dias (`token_expires_at - now < 7 dias` → renovar antes de publicar). Chamada: `GET /oauth/access_token?grant_type=fb_exchange_token`.
- Fluxo de publicação — 3 etapas obrigatórias, sequenciais:
  1. `POST /{ig-user-id}/media` — `media_type=REELS`, `video_url`, `caption`.
  2. Polling `GET /{creation-id}?fields=status_code` — intervalo 3s, timeout total 2min.
  3. `POST /{ig-user-id}/media_publish` — `creation_id`.
- Mídia pública: `UseStaticFiles` do ASP.NET Core, `/app/media/{filename}` → `https://api.omuletachou.com.br/media/{filename}`. Fallback: `MediaUrl` original da plataforma se `MediaLocalPath` for nulo. Retenção indefinida no volume Docker `media_files` nesta fase (sem purge automático — débito técnico registrado, fora de escopo).
- Caption: `GenerateCaptionAsync` (Claude) com tom Instagram (emocional, 3-5 hashtags, CTA, máx. 300 chars). Disclosure `#publi`/`#publicidade` **anexado automaticamente ao final da legenda gerada** — regra obrigatória de compliance (CONAR), não fica a critério da IA omitir.
- Retry: `RetryCount` até 3 via `PublisherJob`, mesmo padrão dos demais publishers (`RegisterAttempt`). Sem tratamento especial de rate limit além de backoff padrão em 429.

## Correção retroativa no `ProcessorJob` (Issue #6, já em produção)
- **Escopo:** em `CreatePublicationQueueEntriesAsync`, estender a condição já existente para `SocialNetwork.Youtube` (introduzida na Issue #8, CA17) de forma a também cobrir `SocialNetwork.Instagram`: pular a criação da entrada de `PublicationQueue` (e a chamada de `GenerateCaptionAsync` para essa rede) se `product.MediaType != "video"` (considerando ausência de `MediaLocalPath` e `MediaUrl` como "sem vídeo").
- **Natureza da mudança:** filtro aditivo e isolado, reaproveitando o mesmo padrão condicional já aplicado ao Youtube — não altera a lógica de habilitação/credenciais/agendamento das demais redes (Telegram, TikTok, Facebook), nem o fluxo de `EnsureAffiliateLinkAsync`, `EnsureCategory`, `EnsureSlug` ou o round-robin de agendamento.
- **Risco avaliado:** BAIXO — mesmo racional de risco da Issue #8. Por isso o PM **não** escalou esta correção para o Arquiteto.
- **Importante para o LT/Dev:** por se tratar de código já em produção, a alteração exige teste de regressão explícito garantindo que Telegram, Youtube, TikTok e Facebook permanecem com o comportamento atual inalterado (mesmo espírito do CA19 da Issue #8, agora estendido para cobrir também a presença do filtro de Youtube já em produção).

## Integrações externas
- Meta Graph API (Instagram Content Publishing): `POST /{ig-user-id}/media`, `GET /{creation-id}?fields=status_code`, `POST /{ig-user-id}/media_publish`.
- Meta OAuth: `GET /oauth/access_token?grant_type=fb_exchange_token` para renovação de token de longa duração.
- Sem CDN externa — mídia servida pelo próprio backend via `UseStaticFiles`.

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: .NET 8 + Meta Graph API + `HttpClient` (já definida na Issue, sem ambiguidade de arquitetura/stack).
- Testes devem usar mocks da Meta Graph API (sem chamadas reais em CI).
- Polling: intervalo fixo de 3s, timeout total de 2min — não configuráveis nesta issue.
- **Validação em conta real do Instagram é obrigatória antes do Gate 2** — mock/análise de código isoladamente não são suficientes para fechar a definição de pronto (ver CA formal correspondente em `criterios-aceite.md`).

## Definição de pronto
- `InstagramPublisher` implementado e registrado no DI, seguindo o padrão estrutural de `TelegramPublisher`/`YoutubePublisher` (leitura de credenciais de `app_settings`, resolução de mídia com fallback, tratamento de exceção compatível com `PublisherJob.RegisterAttempt`).
- Fluxo de 3 etapas (create container → polling → publish) implementado e coberto por teste com mock da Meta Graph API, incluindo cenários de `FAILED` e timeout de 2min.
- Renovação automática de token (margem de 7 dias) e falha de renovação (`Failed` sem retry + `instagram.token_invalid=true`) cobertas por teste.
- Disclosure obrigatório (`#publi`/`#publicidade`) anexado automaticamente ao final da legenda e coberto por teste (não pode ser omitido mesmo se a IA já incluir hashtags próprias).
- Validação de URL pública com fallback para `MediaUrl` coberta por teste.
- Correção no `ProcessorJob` implementada e coberta por teste de regressão (Telegram/Youtube/TikTok/Facebook inalterados) + teste do novo comportamento (Instagram só entra na fila com vídeo disponível).
- Fallback de segurança no `InstagramPublisher` para item sem vídeo (falha sem retry, mensagem padronizada) coberto por teste.
- Testes unitários com mock da Meta Graph API — `dotnet test` verde, sem chamadas reais à API.
- **Validação em conta real do Instagram (não-negociável):** Reel de teste publicado com sucesso na conta Creator/Business configurada, confirmado visualmente no perfil (aparição do Reel), e legenda com hashtag de disclosure validada. Sem essa validação, o Gate 2 não pode ser solicitado ao Gerente.
- Critérios de aceite (`criterios-aceite.md`) validados.

## Perguntas do Gate 1 — respondidas
Ver comentários "📋 Levantamento de requisitos — PM Fase 1" (https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4927260892) e "Respostas do Gerente — Gate 1" (https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4935151980) na Issue #9. Todas as 6 perguntas foram respondidas e incorporadas às regras de negócio acima.

## Avaliação de ambiguidade arquitetural
**Sem ambiguidade arquitetural.** Stack, protocolo de integração (Meta Graph API) e padrão de credenciais já estão definidos na Issue e nas respostas do Gate 1, sem decisão de infraestrutura em aberto. O fluxo de polling assíncrono com timeout (create → poll → publish) é uma novidade de *padrão de implementação* em relação a Telegram/YouTube, mas é equivalente em natureza ao upload em chunks do YouTube (Issue #8): uma sequência de chamadas HTTP contra uma API externa já definida, sem introduzir nova dependência de infraestrutura, storage, fila ou serviço externo além do `HttpClient` já usado pelos publishers existentes. Fica registrado para o Líder Técnico decidir o detalhe de implementação (ex.: uso de `Task.Delay` vs. abstração de polling reutilizável), mas não é decisão de arquitetura — segue direto para refinamento técnico do Líder Técnico, sem escalar ao Arquiteto.
