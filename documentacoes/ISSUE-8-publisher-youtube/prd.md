# PRD — ISSUE-8: Publisher YouTube Shorts

## Status
Fechado (PM Fase 2) — requisitos consolidados com as respostas do Gate 1 (Gerente). Pronto para refinamento técnico (Líder Técnico).

## Objetivo
Implementar `YoutubePublisher : ISocialPublisher` para publicar automaticamente vídeos de produtos aprovados como YouTube Shorts, via YouTube Data API v3, com upload resumable (suporte a arquivos grandes) e renovação automática de token OAuth2. Corrigir, em paralelo, uma lacuna no `ProcessorJob` (Issue #6, já em produção) que hoje enfileira produtos para o YouTube mesmo sem vídeo disponível.

## Contexto / dependências
- Depende das Issues #6 (`ProcessorJob` — download de mídia, `MediaLocalPath`/`MediaType`) e #7 (`PublisherJob`/Hangfire + `TelegramPublisher` como padrão de referência), ambas já em produção (branch `main`).
- `PublisherJob` já resolve o publisher pela rede via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork` — a integração do `YoutubePublisher` é **aditiva**: basta registrar no DI, sem alterações no `PublisherJob`.
- `SocialNetwork.Youtube` já existe no enum (`backend/src/AfiliadoBot.Domain/Enums/SocialNetwork.cs`).
- `ProcessorJob.NetworkSettings` já lista a rede Youtube (`networks.youtube.enabled`, credencial `youtube.access_token` — a revisar/alinhar com as chaves reais de credencial do publisher, ver "Regras de negócio").

## Usuários afetados
- Operação/Marketing da omuletachou: depende da publicação automática ampliar alcance de tráfego de afiliados para o canal do YouTube.
- Indiretamente, o time de suporte/ops que monitora falhas de publicação (`PublicationQueue.Status = Failed`) e o alerta de token inválido no dashboard.

## Casos de uso principais
1. Item de fila (`PublicationQueue`) com `SocialNetwork = Youtube` e produto com vídeo disponível (`MediaLocalPath` ou, na ausência, `MediaUrl`) é processado pelo `PublisherJob` → `YoutubePublisher.PublishAsync` realiza upload resumable com metadados (title, description, tags, categoryId, privacyStatus) → vídeo publicado. O YouTube classifica automaticamente como Short se atender aos critérios do próprio YouTube (≤180s desde out/2024, proporção vertical/quadrada); se não atender, publica como vídeo normal — isso não é erro do publisher.
2. Token de acesso expirado no momento da publicação → `YoutubePublisher` renova via `refresh_token` salvo em `app_settings`, persiste o novo `access_token` e prossegue com a publicação sem intervenção manual.
3. Vídeo grande (>50MB) → upload é feito em chunks fixos de 8MB (upload resumable) sem estourar timeout.
4. `ProcessorJob` monta a fila de publicação de um produto: para a rede Youtube, só cria a entrada de `PublicationQueue` se o produto tiver mídia de vídeo disponível (`MediaType == "video"`, considerando `MediaLocalPath` ou `MediaUrl` como fonte). As demais redes (Telegram, Instagram, TikTok, Facebook) seguem a regra atual, inalterada.

## Casos de exceção
1. **Produto sem vídeo chega ao `YoutubePublisher` mesmo assim** (dado legado anterior à correção do `ProcessorJob`, ou falha da regra): falhar sem retry com `ErrorMessage = "Produto sem mídia de vídeo, não aplicável ao YouTube"`. Este é um fallback de segurança — o caminho normal é o `ProcessorJob` nunca enfileirar esse item para o YouTube.
2. **Falha ao renovar `refresh_token`** (expirado ou revogado no Google): marcar item como `Failed` sem retry automático **e** setar `youtube.token_invalid = true` em `app_settings` (consumido pelo dashboard para alerta visual — fora do escopo desta issue implementar o consumo).
3. **Falha de upload por timeout de chunk** (>5min por chunk): abortar e marcar `Failed`. Falha por timeout total (>15min): idem.
4. **Falha de upload por erro de rede/quota da API/vídeo rejeitado pelo YouTube (não relacionado a token)**: segue o padrão de retry já existente no `PublisherJob` (`RegisterAttempt`, até 3 tentativas).
5. **`MediaLocalPath` nulo e `MediaUrl` presente**: `YoutubePublisher` baixa o conteúdo de `MediaUrl` para um stream/arquivo temporário antes de iniciar o upload resumable (diferente do Telegram, que aceita a URL remota diretamente na chamada da API — a API do YouTube exige os bytes do arquivo).

## Regras de negócio
- `title`: primeiros 100 caracteres de `Product.Title`.
- `description`: `Product.AiCaption` (legenda gerada por IA, mesmo campo usado pelo Telegram).
- `tags`: fixo `["oferta", "desconto", "promocao", platform]`.
- `categoryId`: **mapeado** por `Product.Category` via dicionário fixo (não é mais valor fixo "26"):
  | Product.Category | YouTube categoryId | Nome |
  |---|---|---|
  | Eletrônicos | 28 | Science & Technology |
  | Casa e Cozinha | 26 | Howto & Style |
  | Beleza e Cuidados Pessoais | 26 | Howto & Style |
  | Moda | 26 | Howto & Style |
  | Brinquedos | 24 | Entertainment |
  | Geral | 22 | People & Blogs |
  | (categoria não mapeada — fallback) | 22 | People & Blogs |
- `privacyStatus`: `"public"`.
- Classificação como Short é automática do YouTube com base no vídeo enviado — o `YoutubePublisher` **não valida** proporção/duração antes do envio.
- Fonte do arquivo: `Product.MediaLocalPath` com fallback para `Product.MediaUrl`. Se usar `MediaUrl`, baixar para stream/arquivo temporário antes do upload (API do YouTube exige bytes, não aceita URL remota).
- Upload resumable em chunks fixos de 8MB (múltiplo de 256KB, recomendação oficial do Google).
- Timeout: 5 min por chunk (aborta e marca `Failed` se exceder); 15 min para o upload total.
- Credenciais em `app_settings`: `youtube.client_id`, `youtube.client_secret`, `youtube.refresh_token` (padrão já usado por `telegram.bot_token`/`telegram.channel_id`). **Nota:** `ProcessorJob.NetworkSettings` hoje referencia `youtube.access_token` como chave de credencial para decidir se a rede está habilitada — o LT deve avaliar se essa chave precisa ser ajustada para `youtube.refresh_token` (ou outra combinação), já que o `access_token` é renovado em runtime e não é uma credencial de configuração estável.
- Falha ao renovar `refresh_token`: `Failed` sem retry + `youtube.token_invalid = true` em `app_settings`.

## Correção retroativa no `ProcessorJob` (Issue #6, já em produção)
- **Escopo:** em `CreatePublicationQueueEntriesAsync`, ao iterar `NetworkSettings`, adicionar uma condição adicional **somente para `SocialNetwork.Youtube`**: pular a criação da entrada de `PublicationQueue` (e a chamada de `GenerateCaptionAsync` para essa rede) se `product.MediaType != "video"` (considerando ausência de `MediaLocalPath` e `MediaUrl` como "sem vídeo").
- **Natureza da mudança:** filtro aditivo e isolado — um `if` adicional na decisão de criar (ou não) a entrada de fila para a rede YouTube especificamente. Não altera a lógica de habilitação/credenciais/agendamento das demais redes (Telegram, Instagram, TikTok, Facebook), nem o fluxo de `EnsureAffiliateLinkAsync`, `EnsureCategory`, `EnsureSlug` ou o round-robin de agendamento.
- **Risco avaliado:** BAIXO. É uma mudança pequena, isolada por rede, sem retrofit de comportamento existente. Por isso o PM **não** escalou esta correção para o Arquiteto — segue direto para o refinamento técnico do Líder Técnico junto com o restante da issue.
- **Importante para o LT/Dev:** por se tratar de código já em produção (branch `main`), a alteração exige atenção redobrada em cobertura de teste de regressão para as demais redes (garantir que o comportamento delas permanece idêntico) além do teste do novo comportamento para Youtube.

## Nota sobre download de mídia sob demanda (MediaUrl → stream temporário)
O `YoutubePublisher` precisa de um padrão ainda não usado pelos publishers existentes: quando `MediaLocalPath` está vazio e apenas `MediaUrl` está disponível, é necessário baixar o conteúdo para um arquivo/stream temporário antes do upload (a API do YouTube não aceita referência de URL remota, diferente do Telegram, que passa a URL direto no multipart). Esta é uma decisão de **implementação** (não de arquitetura): usa o `HttpClient` já injetado no publisher (mesmo padrão de outras integrações HTTP do projeto) para baixar os bytes; não introduz nova dependência de infraestrutura, storage ou serviço externo além do que já existe (`IMediaStorage`/`HttpClient`). Fica registrado aqui para que o LT decida, no refinamento técnico, se reaproveita `IMediaStorage.DownloadAsync` (já usado pelo `ProcessorJob` para popular `MediaLocalPath`) ou implementa um download local ao publisher — não é bloqueio de PRD, é detalhe de task breakdown.

## Integrações externas
- YouTube Data API v3 (`Google.Apis.YouTube.v3` — NuGet).
- OAuth2 do Google (`https://oauth2.googleapis.com/token`) para renovação de `access_token`.
- Upload resumable: `POST https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable`.

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: .NET 8 + `Google.Apis.YouTube.v3` + OAuth2 (já definida na Issue, sem ambiguidade de arquitetura/stack).
- Testes devem usar mocks do cliente Google (sem chamadas reais à API do YouTube em CI).
- Chunk de upload fixo em 8MB; timeout de 5min/chunk e 15min total — não configuráveis nesta issue.

## Definição de pronto
- `YoutubePublisher` implementado e registrado no DI, seguindo o mesmo padrão estrutural de `TelegramPublisher` (leitura de credenciais de `app_settings`, resolução de mídia local com fallback para download sob demanda, tratamento de exceção compatível com `PublisherJob.RegisterAttempt`).
- Renovação automática de token funcionando e coberta por teste.
- Upload em chunks de 8MB para arquivos grandes coberto por teste (mock), incluindo os cenários de timeout de chunk (5min) e timeout total (15min).
- Dicionário de mapeamento `Product.Category → categoryId` implementado e coberto por teste, incluindo o caso de fallback (categoria não mapeada → 22).
- Correção no `ProcessorJob` implementada e coberta por teste de regressão (demais redes inalteradas) + teste do novo comportamento (Youtube só entra na fila com vídeo disponível).
- Fallback de segurança no `YoutubePublisher` para item sem vídeo (falha sem retry, mensagem padronizada) coberto por teste.
- Comportamento de falha de refresh_token (Failed sem retry + flag `youtube.token_invalid=true`) coberto por teste.
- Testes unitários com mock do cliente Google — `dotnet test` verde, sem chamadas reais à API.
- Critérios de aceite (`criterios-aceite.md`) validados.

## Perguntas do Gate 1 — respondidas
Ver comentários "Gate 1 — Perguntas para o Gerente" e "Gate 1 — Respostas do Gerente" na Issue #8. Todas as 6 perguntas foram respondidas e incorporadas às regras de negócio acima.
