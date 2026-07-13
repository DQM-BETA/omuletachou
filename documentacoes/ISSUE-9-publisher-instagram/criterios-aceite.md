# Critérios de aceite — ISSUE-9: Publisher Instagram (Meta Graph API)

## InstagramPublisher — fluxo de publicação (3 etapas)

**CA1 — Etapa 1: criação do container de mídia**
Given um item de `PublicationQueue` com `SocialNetwork = Instagram` cujo produto tem vídeo disponível (`MediaLocalPath` ou, na ausência, `MediaUrl`) e uma URL HTTPS pública válida para a mídia
When `InstagramPublisher.PublishAsync` é chamado
Then é feita a chamada `POST /{ig-user-id}/media` com `media_type=REELS`, `video_url` apontando para a URL pública e `caption` (já com disclosure anexado), e o `creation-id` retornado é capturado para a etapa seguinte.

**CA2 — Etapa 2: polling de status até FINISHED**
Given um `creation-id` retornado pela etapa 1
When `InstagramPublisher` inicia o polling `GET /{creation-id}?fields=status_code`
Then as chamadas são feitas a intervalos de 3 segundos até `status_code = FINISHED`, e o fluxo prossegue para a etapa 3 assim que o status é atingido.

**CA3 — Etapa 3: publicação do container**
Given um container com `status_code = FINISHED`
When `InstagramPublisher` executa a etapa final
Then é feita a chamada `POST /{ig-user-id}/media_publish` com o `creation_id`, e o método retorna sucesso (`true`) quando a publicação é confirmada pela API.

**CA4 — Polling: status FAILED**
Given um `creation-id` cujo polling retorna `status_code = FAILED`
When `InstagramPublisher.PublishAsync` está em andamento
Then a publicação falha imediatamente com `ErrorMessage` descritivo, sem prosseguir para a etapa de `media_publish`.

**CA5 — Polling: timeout de 2 minutos**
Given um `creation-id` cujo polling não atinge `status_code = FINISHED` dentro de 2 minutos (intervalos de 3s)
When o timeout é atingido
Then o item é marcado como `Failed`, elegível a retry no próximo ciclo do `PublisherJob` (respeitando `RetryCount` até 3).

## Mídia pública

**CA6 — URL pública acessível (caminho feliz)**
Given `MediaLocalPath` preenchido apontando para um arquivo servido via `/app/media`
When `InstagramPublisher.PublishAsync` monta a `video_url`
Then a URL pública correspondente (`https://api.omuletachou.com.br/media/{filename}`) é usada na chamada de criação do container.

**CA7 — Fallback para MediaUrl quando MediaLocalPath é nulo**
Given `MediaLocalPath` nulo e `MediaUrl` preenchido apontando para uma mídia pública válida
When `InstagramPublisher.PublishAsync` monta a `video_url`
Then a `MediaUrl` original é usada como fallback na chamada de criação do container.

**CA8 — URL de mídia inacessível**
Given uma URL de mídia (local ou `MediaUrl` de fallback) que não está acessível publicamente
When `InstagramPublisher.PublishAsync` é chamado
Then o método retorna `false` com mensagem de erro clara, sem tentar criar o container de mídia.

## Legenda e disclosure obrigatório

**CA9 — Caption gerada com tom Instagram**
Given um produto com `AiCaption`/legenda gerada via `GenerateCaptionAsync` para a rede Instagram
When a legenda é montada para a publicação
Then a legenda respeita o tom emocional, 3-5 hashtags, CTA e o limite de 300 caracteres definidos para Instagram.

**CA10 — Disclosure anexado automaticamente**
Given qualquer legenda gerada pelo `GenerateCaptionAsync` para a rede Instagram (com ou sem hashtags de disclosure já incluídas pela IA)
When a legenda final é montada pelo `InstagramPublisher`
Then a hashtag `#publi` ou `#publicidade` é anexada automaticamente ao final da legenda, garantindo presença do disclosure independentemente do que a IA gerou.

**CA11 — Disclosure não duplicado**
Given uma legenda gerada pela IA que já inclui `#publi` ou `#publicidade`
When a legenda final é montada pelo `InstagramPublisher`
Then o disclosure não é duplicado (apenas uma ocorrência da hashtag de disclosure na legenda final).

## Renovação de token

**CA12 — Renovação automática quando restam menos de 7 dias**
Given `instagram.token_expires_at` indicando menos de 7 dias de validade restante
When `InstagramPublisher.PublishAsync` é chamado
Then o token é renovado via `GET /oauth/access_token?grant_type=fb_exchange_token` antes de iniciar a etapa 1, e o novo `access_token`/`token_expires_at` são persistidos em `app_settings` sem intervenção manual.

**CA13 — Token com validade suficiente não é renovado**
Given `instagram.token_expires_at` indicando 7 dias ou mais de validade restante
When `InstagramPublisher.PublishAsync` é chamado
Then nenhuma chamada de renovação é feita, e o fluxo de publicação prossegue diretamente com o token atual.

**CA14 — Falha na renovação do token**
Given uma tentativa de renovação que falha (token já expirado ou revogado pela Meta)
When `InstagramPublisher.PublishAsync` é chamado
Then o item é marcado como `Failed` sem retry automático, e `instagram.token_invalid = true` é persistido em `app_settings`.

## Fallback de segurança — item sem vídeo

**CA15 — InstagramPublisher recebe item sem vídeo (fallback de segurança)**
Given um item de `PublicationQueue` com `SocialNetwork = Instagram` cujo produto não tem `MediaType = "video"` nem `MediaLocalPath`/`MediaUrl` válidos apontando para vídeo (cenário legado/falha de regra — não deveria ocorrer após a correção do `ProcessorJob`)
When `InstagramPublisher.PublishAsync` é chamado
Then o item é marcado como `Failed` sem retry, com `ErrorMessage` descritivo indicando ausência de mídia de vídeo aplicável ao Instagram.

## Correção no ProcessorJob (Issue #6, código em produção)

**CA16 — ProcessorJob não enfileira Instagram sem vídeo**
Given um produto com `Status = Queued`, rede Instagram habilitada (`networks.instagram.enabled = true`) e credenciais presentes, mas `MediaType != "video"` (ou sem `MediaLocalPath`/`MediaUrl`)
When `ProcessorJob.ExecuteAsync` monta as entradas de `PublicationQueue` para esse produto
Then nenhuma entrada de `PublicationQueue` com `SocialNetwork = Instagram` é criada para esse produto, e `GenerateCaptionAsync` não é chamado para a rede Instagram.

**CA17 — ProcessorJob enfileira Instagram normalmente quando há vídeo**
Given um produto com `Status = Queued`, rede Instagram habilitada e credenciais presentes, e `MediaType = "video"` com `MediaLocalPath` ou `MediaUrl` preenchidos
When `ProcessorJob.ExecuteAsync` monta as entradas de `PublicationQueue`
Then uma entrada de `PublicationQueue` com `SocialNetwork = Instagram` é criada normalmente, com `ScheduledAt` no slot round-robin correspondente.

**CA18 — Regressão: demais redes não afetadas pela correção (Telegram, Youtube, TikTok, Facebook)**
Given um produto elegível para Telegram, Youtube, TikTok e Facebook (habilitadas, com credenciais, com ou sem vídeo conforme já definido para cada rede — incluindo o filtro de vídeo já em produção para Youtube desde a Issue #8)
When `ProcessorJob.ExecuteAsync` monta as entradas de `PublicationQueue`
Then o comportamento de criação de entradas para essas redes permanece idêntico ao existente antes da correção desta issue (nenhuma regressão introduzida pelo filtro adicional específico do Instagram).

## Testes automatizados

**CA19 — Testes com mocks, sem chamadas reais**
Given a suíte de testes do projeto
When `dotnet test` é executado
Then todos os testes relacionados a `InstagramPublisher` e à correção no `ProcessorJob` passam usando mocks da Meta Graph API e do `HttpClient`, sem nenhuma chamada real à API do Instagram ou ao endpoint OAuth da Meta.

## Validação em conta real (definição de pronto — não-negociável)

**CA20 — Publicação real validada visualmente (obrigatório antes do Gate 2)**
Given a conta Instagram Business/Creator configurada em produção/homologação com `access_token` e `page_id` válidos
When um Reel de teste é publicado via `InstagramPublisher` contra a API real da Meta (fora do CI, execução manual/assistida)
Then o Reel aparece de fato no perfil do Instagram (confirmação visual, não apenas resposta HTTP 200 da API), e a legenda publicada contém a hashtag de disclosure (`#publi` ou `#publicidade`) corretamente aplicada ao final do texto.
Este critério não pode ser satisfeito apenas por testes com mock ou análise estática de código — é um requisito de definição de pronto explicitamente exigido pelo Gerente no Gate 1, e deve ser evidenciado (print/link do post) antes de solicitar o Gate 2.
