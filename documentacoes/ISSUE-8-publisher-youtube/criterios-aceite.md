# Critérios de aceite — ISSUE-8: Publisher YouTube Shorts

## YoutubePublisher — publicação bem-sucedida

**CA1 — Publicação com vídeo local disponível**
Given um item de `PublicationQueue` com `SocialNetwork = Youtube` cujo produto tem `MediaLocalPath` apontando para um arquivo de vídeo válido
When `YoutubePublisher.PublishAsync` é chamado
Then o upload resumable é realizado com os bytes do arquivo local, com metadados (`title`, `description`, `tags`, `categoryId`, `privacyStatus`) corretamente montados, e o método retorna sucesso.

**CA2 — Publicação com fallback para MediaUrl (download sob demanda)**
Given um item cujo produto tem `MediaLocalPath` nulo mas `MediaUrl` preenchido apontando para um vídeo válido
When `YoutubePublisher.PublishAsync` é chamado
Then o conteúdo de `MediaUrl` é baixado para um stream/arquivo temporário antes do upload, o upload resumable prossegue normalmente, e o método retorna sucesso.

**CA3 — Classificação automática como Short**
Given um vídeo vertical/quadrado com duração dentro dos critérios do YouTube
When o upload é concluído
Then o vídeo é classificado como Short pelo próprio YouTube — o `YoutubePublisher` não realiza nenhuma validação prévia de proporção/duração antes do envio.

**CA4 — Vídeo fora dos critérios de Short**
Given um vídeo que não atende aos critérios de Short (duração/proporção)
When o upload é concluído com sucesso
Then o vídeo é publicado normalmente (não é tratado como erro pelo `YoutubePublisher`).

## Metadados

**CA5 — Title truncado**
Given `Product.Title` com mais de 100 caracteres
When os metadados do vídeo são montados
Then `title` contém exatamente os primeiros 100 caracteres de `Product.Title`.

**CA6 — Description a partir da legenda de IA**
Given um produto com `AiCaption` preenchido
When os metadados são montados
Then `description` recebe o valor de `Product.AiCaption`.

**CA7 — Tags fixas**
Given qualquer publicação
When os metadados são montados
Then `tags` contém `["oferta", "desconto", "promocao", "youtube"]` (platform = "youtube").

**CA8 — Mapeamento de categoryId por Product.Category**
Given produtos com `Category` = "Eletrônicos", "Casa e Cozinha", "Beleza e Cuidados Pessoais", "Moda", "Brinquedos", "Geral"
When os metadados são montados
Then `categoryId` é, respectivamente, "28", "26", "26", "26", "24", "22".

**CA9 — Fallback de categoryId para categoria não mapeada**
Given um produto com `Category` que não consta no dicionário de mapeamento
When os metadados são montados
Then `categoryId` é "22" (People & Blogs).

**CA10 — privacyStatus fixo**
Given qualquer publicação
When os metadados são montados
Then `privacyStatus` é sempre `"public"`.

## Renovação de token

**CA11 — Renovação automática de access_token expirado**
Given `access_token` expirado (ou ausente) e `refresh_token` válido em `app_settings`
When `PublishAsync` é chamado
Then o `YoutubePublisher` renova o token via `https://oauth2.googleapis.com/token`, persiste o novo `access_token` em `app_settings` e prossegue com a publicação sem intervenção manual.

**CA12 — Falha na renovação do refresh_token**
Given `refresh_token` expirado ou revogado no Google (renovação falha)
When `PublishAsync` é chamado
Then o item é marcado como `Failed` sem retry automático, e `youtube.token_invalid = true` é persistido em `app_settings`.

## Upload por chunks

**CA13 — Upload em chunks de 8MB**
Given um arquivo de vídeo maior que 8MB
When o upload resumable é executado
Then o arquivo é enviado em chunks fixos de 8MB (múltiplos de 256KB), sem erro de timeout.

**CA14 — Timeout de chunk**
Given um chunk que não completa o envio em 5 minutos
When o timeout de chunk é atingido
Then o upload é abortado e o item é marcado como `Failed`.

**CA15 — Timeout total do upload**
Given um upload cujo tempo total excede 15 minutos
When o timeout total é atingido
Then o upload é abortado e o item é marcado como `Failed`.

## Fallback de segurança — item sem vídeo

**CA16 — YoutubePublisher recebe item sem vídeo (fallback de segurança)**
Given um item de `PublicationQueue` com `SocialNetwork = Youtube` cujo produto não tem `MediaType = "video"` nem `MediaLocalPath`/`MediaUrl` válidos apontando para vídeo (cenário legado/falha de regra — não deveria ocorrer após a correção do ProcessorJob)
When `YoutubePublisher.PublishAsync` é chamado
Then o item é marcado como `Failed` sem retry, com `ErrorMessage = "Produto sem mídia de vídeo, não aplicável ao YouTube"`.

## Correção no ProcessorJob (Issue #6, código em produção)

**CA17 — ProcessorJob não enfileira Youtube sem vídeo**
Given um produto com `Status = Queued`, rede Youtube habilitada (`networks.youtube.enabled = true`) e credenciais presentes, mas `MediaType != "video"` (ou sem `MediaLocalPath`/`MediaUrl`)
When `ProcessorJob.ExecuteAsync` monta as entradas de `PublicationQueue` para esse produto
Then nenhuma entrada de `PublicationQueue` com `SocialNetwork = Youtube` é criada para esse produto, e `GenerateCaptionAsync` não é chamado para a rede Youtube.

**CA18 — ProcessorJob enfileira Youtube normalmente quando há vídeo**
Given um produto com `Status = Queued`, rede Youtube habilitada e credenciais presentes, e `MediaType = "video"` com `MediaLocalPath` ou `MediaUrl` preenchidos
When `ProcessorJob.ExecuteAsync` monta as entradas de `PublicationQueue`
Then uma entrada de `PublicationQueue` com `SocialNetwork = Youtube` é criada normalmente, com `ScheduledAt` no slot round-robin correspondente.

**CA19 — Regressão: demais redes não afetadas pela correção**
Given um produto elegível para Telegram, Instagram, TikTok e Facebook (habilitadas, com credenciais, independentemente de ter vídeo ou não)
When `ProcessorJob.ExecuteAsync` monta as entradas de `PublicationQueue`
Then o comportamento de criação de entradas para essas redes permanece idêntico ao existente antes da correção (nenhuma regressão introduzida pelo filtro adicional específico do Youtube).

## Testes

**CA20 — Testes com mocks, sem chamadas reais**
Given a suíte de testes do projeto
When `dotnet test` é executado
Then todos os testes relacionados a `YoutubePublisher` e à correção no `ProcessorJob` passam usando mocks do cliente Google e do `HttpClient`, sem nenhuma chamada real à API do YouTube ou ao endpoint OAuth2 do Google.
