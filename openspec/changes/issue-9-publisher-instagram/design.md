# Design (resumido) — ISSUE-9: Publisher Instagram (Meta Graph API)

> PM Fase 2 concluiu sem escalar ao Arquiteto (sem ambiguidade arquitetural). Este design.md é o
> resumo técnico produzido pelo Líder Técnico para preencher a lacuna, seguindo o padrão
> estrutural já validado em produção pelas Issues #7 (`TelegramPublisher`) e #8 (`YoutubePublisher`).

## Visão geral da solução
`InstagramPublisher : ISocialPublisher` é uma nova implementação registrada no DI e resolvida
genericamente pelo `PublisherJob` via `IEnumerable<ISocialPublisher>` (nenhuma mudança no
`PublisherJob` — puramente aditivo, mesmo padrão de #7/#8). Fluxo interno de 3 chamadas HTTP
sequenciais contra a Meta Graph API (create container → poll → publish), sem SDK oficial (mesma
decisão já tomada para o YouTube: chamadas HTTP diretas via `HttpClient` dão controle fino sobre
polling/timeout que os SDKs não expõem).

Em paralelo, fix retroativo isolado no `ProcessorJob` (código já em produção, Issue #6):
generalizar o filtro `HasVideoAvailable` (hoje só cobre `SocialNetwork.Youtube`) para também
cobrir `SocialNetwork.Instagram`.

## Componentes envolvidos
- `AfiliadoBot.Infrastructure/Integrations/Social/InstagramPublisher.cs` (novo)
- `AfiliadoBot.Application/Jobs/ProcessorJob.cs` (alteração aditiva na condição de filtro de rede)
- `AfiliadoBot.API/Program.cs` (adicionar `app.UseStaticFiles` para `/app/media` → `/media`,
  ausente hoje — confirmado por leitura direta do arquivo)
- `AfiliadoBot.Infrastructure/Services/ClaudeAiService.cs` — **NÃO alterado**. O prompt de tom
  Instagram (`SocialNetwork.Instagram => "Tom emocional... 3 a 5 hashtags..."`) já existe desde
  #3/#6; o disclosure `#publi`/`#publicidade` é decisão de pós-processamento isolado dentro do
  `InstagramPublisher` (ver especificacao-tecnica.md, seção Disclosure) — não no serviço
  compartilhado, para não introduzir risco de regressão nas demais redes.
- DI: registro de `InstagramPublisher` em `Program.cs` (mesmo padrão de `YoutubePublisher`/
  `TelegramPublisher` com `HttpClient` tipado).

## Stack
.NET 8, `HttpClient` (chamadas diretas à Meta Graph API — sem SDK), EF Core 8 (`AppSettings`,
`Product`, `PublicationQueue`), sem novas dependências de infraestrutura.

## Fluxo de dados (direto)
1. `PublisherJob` seleciona `InstagramPublisher` para itens com `SocialNetwork = Instagram`.
2. `InstagramPublisher.PublishAsync`:
   a. Carrega credenciais de `app_settings` (`instagram.access_token`, `instagram.page_id`,
      `instagram.token_expires_at`, `instagram.token_invalid`).
   b. Verifica expiração (margem 7 dias) → renova via `fb_exchange_token` se necessário,
      persiste novo token/expiração; falha de renovação → `FailPermanently` + `token_invalid=true`.
   c. Fallback de segurança: se produto sem vídeo, `FailPermanently` (mesmo padrão do CA16/YouTube).
   d. Resolve `video_url` pública: `MediaLocalPath` servido via `/app/media/{filename}` →
      URL absoluta pública; fallback `MediaUrl` original.
   e. Monta caption via `product.AiCaption` (já gerado pelo `ProcessorJob`/`GenerateCaptionAsync`)
      + disclosure anexado/deduplicado no `InstagramPublisher`.
   f. `POST /media` (create container) → captura `creation-id`.
   g. Polling `GET /{creation-id}?fields=status_code` a cada 3s, timeout total 2min.
   h. `POST /media_publish` com `creation_id`.
3. `ProcessorJob.CreatePublicationQueueEntriesAsync`: filtro `HasVideoAvailable` generalizado
   para `network == SocialNetwork.Youtube || network == SocialNetwork.Instagram`.

Sem decisões de arquitetura em aberto — este documento apenas registra por escrito o que já
estava implícito no PRD/CA, para consistência do processo (LT sempre produz design.md antes do
task breakdown).
