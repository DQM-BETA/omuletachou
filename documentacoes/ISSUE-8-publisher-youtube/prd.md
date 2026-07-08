# PRD — ISSUE-8: Publisher YouTube Shorts

## Status
Rascunho inicial (PM Fase 1) — aguardando respostas do Gate 1 (Gerente) antes de fechar regras de negócio definitivas.

## Objetivo
Implementar `YoutubePublisher : ISocialPublisher` para publicar automaticamente vídeos de produtos aprovados como YouTube Shorts, via YouTube Data API v3, com upload resumable (suporte a arquivos grandes) e renovação automática de token OAuth2.

## Contexto / dependências
- Depende das Issues #6 (ProcessorJob — download de mídia, `MediaLocalPath`/`MediaType`) e #7 (PublisherJob/Hangfire + `TelegramPublisher` como padrão de referência), ambas já em produção.
- `PublisherJob` já resolve o publisher pela rede via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork` — a integração do YouTube é **aditiva**: basta registrar `YoutubePublisher` no DI, sem alterações no `PublisherJob`.
- `SocialNetwork.Youtube` já existe no enum (`backend/src/AfiliadoBot.Domain/Enums/SocialNetwork.cs`).

## Usuários afetados
- Operação/Marketing da omuletachou: depende da publicação automática ampliar alcance de tráfego de afiliados para o canal do YouTube.
- Indiretamente, o time de suporte/ops que monitora falhas de publicação (`PublicationQueue.Status = Failed`).

## Casos de uso principais
1. Item de fila (`PublicationQueue`) com `SocialNetwork = Youtube` e produto com vídeo local disponível é processado pelo `PublisherJob` → `YoutubePublisher.PublishAsync` realiza upload resumable com metadados (title, description, tags, categoryId, privacyStatus) → vídeo publicado, classificado automaticamente como Short se atender aos critérios do próprio YouTube (≤60s, proporção 9:16).
2. Token de acesso expirado no momento da publicação → `YoutubePublisher` renova via `refresh_token` salvo em `app_settings`, persiste o novo `access_token`/`refresh_token` e prossegue com a publicação sem intervenção manual.
3. Vídeo grande (>50MB) → upload é feito em chunks (upload resumable) sem estourar timeout.

## Casos de exceção (a confirmar no Gate 1)
1. Produto sem vídeo (`MediaType != "video"` ou `MediaLocalPath`/`MediaUrl` nulos) chega à fila do YouTube — comportamento ainda não definido (ver pergunta 1 do Gate 1). Suspeita de lacuna no `ProcessorJob` (Issue #6): a entrada de fila para a rede YouTube talvez devesse nem ser criada quando não há vídeo.
2. Falha ao renovar `refresh_token` (expirado/revogado pelo usuário no Google) — comportamento de erro ainda não definido (pergunta 5).
3. Falha de upload (erro de rede, quota da API excedida, vídeo rejeitado pelo YouTube) — segue o padrão de retry já existente no `PublisherJob` (`RegisterAttempt`, até 3 tentativas), a menos que seja um erro não recuperável (ex.: token revogado).

## Regras de negócio levantadas da Issue
- `title`: primeiros 100 caracteres de `Product.Title`.
- `description`: legenda gerada por IA (`Product.AiCaption`, chamada de "caption" na fila).
- `tags`: fixo `["oferta", "desconto", "promocao", platform]`.
- `categoryId`: fixo `"26"` (Howto & Style) — a confirmar se deve ser sempre fixo ou mapear por `Product.Category` (pergunta 6).
- `privacyStatus`: `"public"`.
- Classificação como Short (≤60s, proporção 9:16) é automática do próprio YouTube com base no vídeo enviado — a confirmar se há alguma validação prévia esperada do lado do `YoutubePublisher` (pergunta 3).
- Credenciais em `app_settings`: `youtube.client_id`, `youtube.client_secret`, `youtube.refresh_token` (padrão já usado por `telegram.bot_token`/`telegram.channel_id`).

## Integrações externas
- YouTube Data API v3 (`Google.Apis.YouTube.v3` — NuGet).
- OAuth2 do Google (`https://oauth2.googleapis.com/token`) para renovação de `access_token`.
- Upload resumable: `POST https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable`.

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: .NET 8 + `Google.Apis.YouTube.v3` + OAuth2 (já definida na Issue, sem ambiguidade de arquitetura/stack).
- Testes devem usar mocks do cliente Google (sem chamadas reais à API do YouTube em CI).

## Definição de pronto
- `YoutubePublisher` implementado e registrado no DI, seguindo o mesmo padrão estrutural de `TelegramPublisher` (leitura de credenciais de `app_settings`, resolução de mídia local com fallback, tratamento de exceção compatível com `PublisherJob.RegisterAttempt`).
- Renovação automática de token funcionando e coberta por teste.
- Upload em chunks para arquivos grandes coberto por teste (mock).
- Testes unitários com mock do cliente Google — `dotnet test` verde, sem chamadas reais à API.
- Critérios de aceite (`criterios-aceite.md`) validados.
- Comportamento para os casos de exceção listados acima definido e documentado (dependente das respostas do Gate 1).

## Perguntas em aberto
Ver comentário "Gate 1 — Perguntas para o Gerente" postado na Issue #8. Resumo dos eixos:
1. Produto sem vídeo destinado ao YouTube — falhar vs. nunca enfileirar (lacuna no ProcessorJob?).
2. Fonte do arquivo de vídeo (`MediaLocalPath` vs. `MediaUrl` como fallback).
3. Quem valida duração/proporção do Short — o YouTube automaticamente ou o `YoutubePublisher`.
4. Tamanho de chunk do upload resumable e timeout do job.
5. Comportamento em caso de falha de renovação do `refresh_token` (revogado/expirado).
6. `categoryId` fixo "26" para todos os produtos ou mapeado por categoria.
