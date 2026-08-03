# Critérios de Aceite — ISSUE-130: fix: Legenda de IA nunca é persistida

## Migration — `PublicationQueue.Caption`

**CA1 — Coluna adicionada com default seguro**
Given a migration `ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL DEFAULT ''` aplicada
When o schema do banco é inspecionado após a migration
Then a tabela `publication_queue` possui a coluna `caption`, tipo texto, `NOT NULL`, default `''`, e nenhuma linha existente quebra a constraint.

**CA2 — Itens legados (pré-migration) permanecem com caption vazia, sem backfill**
Given itens de `PublicationQueue` já existentes antes da migration
When a migration é aplicada
Then esses itens passam a ter `Caption=''` (nenhum processamento retroativo é executado), e nenhuma comunicação/registro além de uma linha no PR/changelog é exigido.

## ProcessorJob — geração e persistência

**CA3 — Legenda gerada é persistida em `PublicationQueue.Caption`**
Given um produto elegível para publicação em uma rede social (ex.: Telegram)
When `ProcessorJob` enfileira o item em `PublicationQueue` e chama `GenerateCaptionAsync(product, network, ct)`
Then o retorno da chamada é persistido em `Caption` do item de `PublicationQueue` correspondente (não descartado), e `Caption` não é vazio quando a chamada retorna sucesso.

**CA4 — Legenda persistida corresponde à rede social do item**
Given um produto agendado para publicação em múltiplas redes (ex.: Telegram e Instagram, com prompts/textos diferentes por rede)
When `ProcessorJob` processa o enfileiramento
Then cada item de `PublicationQueue` tem `Caption` correspondente ao mock/resultado de `GenerateCaptionAsync` daquela rede específica — sem um item sobrescrever o outro (elimina o bug de campo único em `Product.AiCaption`).

**CA5 — `Product.AiCaption` deixa de ser a fonte de verdade**
Given a correção aplicada
When qualquer publisher ou tela do dashboard precisa da legenda de IA para publicação
Then a leitura é sempre de `PublicationQueue.Caption` do item correspondente — `Product.AiCaption`, se mantido, não é lido por nenhum publisher nem pelo Facebook Manual.

**CA6 — Falha na geração não quebra o enfileiramento (comportamento existente preservado)**
Given `GenerateCaptionAsync` lança exceção ou esgota retries
When `ProcessorJob` processa o item
Then o comportamento de erro/retry já existente no `ProcessorJob` é mantido inalterado (fora de escopo desta correção redesenhar a política de retry); nenhuma regressão nesse fluxo.

## Publishers automáticos — leitura da nova fonte

**CA7 — TelegramPublisher lê `PublicationQueue.Caption`**
Given um item `PublicationQueue` com `Caption` preenchida para a rede Telegram
When `TelegramPublisher` monta e publica o post
Then o texto do post usa `PublicationQueue.Caption` do item, não `product.AiCaption`.

**CA8 — YoutubePublisher lê `PublicationQueue.Caption`**
Given um item `PublicationQueue` com `Caption` preenchida para a rede YouTube
When `YoutubePublisher` monta e publica o post (descrição do vídeo)
Then o texto usa `PublicationQueue.Caption` do item, não `product.AiCaption`.

**CA9 — InstagramPublisher lê `PublicationQueue.Caption`**
Given um item `PublicationQueue` com `Caption` preenchida para a rede Instagram
When `InstagramPublisher` monta e publica o post
Then o texto usa `PublicationQueue.Caption` do item, não `product.AiCaption`.

**CA10 — TikTokPublisher lê `PublicationQueue.Caption`**
Given um item `PublicationQueue` com `Caption` preenchida para a rede TikTok
When `TikTokPublisher` monta e publica o post
Then o texto usa `PublicationQueue.Caption` do item, não `product.AiCaption`.

**CA11 — Caption vazia (item legado) é tratada sem quebrar a publicação**
Given um item legado `PublicationQueue` com `Caption=''` (pré-migration, sem backfill)
When qualquer um dos 4 publishers processa esse item
Then a publicação ocorre normalmente sem legenda (comportamento equivalente ao atual para esse caso), sem exceção não tratada.

## Facebook Manual (dashboard)

**CA12 — `ProductDetailDto` expõe a legenda de IA por rede (ao menos Facebook)**
Given um produto com item de `PublicationQueue` gerado para a rede Facebook
When o backend monta `ProductDetailDto` para esse produto
Then o DTO inclui a `Caption` real de `PublicationQueue` correspondente à rede Facebook (não a descrição original do produto).

**CA13 — `ProductDetail` (frontend) consome a nova legenda**
Given o backend retorna `ProductDetailDto` com a caption de Facebook preenchida
When a tela de publicação manual do Facebook (Facebook Manual) renderiza o botão "copiar legenda"
Then o texto copiado é a legenda de IA real (de `PublicationQueue.Caption`), não `post.product.description`.

**CA14 — Ausência de caption de Facebook (item legado) tem fallback claro**
Given um produto sem item de `PublicationQueue` para a rede Facebook (ou com `Caption=''`, legado)
When o operador abre o Facebook Manual para esse produto
Then a UI não quebra — exibe a legenda vazia ou um fallback explícito (não silenciosamente a descrição original disfarçada de legenda de IA).

## Cobertura de teste — `ProcessorJobTests.cs`

**CA15 — Teste valida persistência, não apenas chamada de mock**
Given um cenário de teste onde `GenerateCaptionAsync` é mockado para retornar uma legenda específica por rede
When `ProcessorJob.ExecuteAsync()` é executado no teste
Then o teste verifica que `PublicationQueue.Caption` do item persistido é igual ao valor retornado pelo mock (não apenas `Times.Once`/`Times.Never` na chamada do mock).

**CA16 — Teste cobre múltiplas redes com captions distintas**
Given um produto agendado para 2+ redes sociais no mesmo teste, com mocks de `GenerateCaptionAsync` retornando valores diferentes por rede
When `ProcessorJob.ExecuteAsync()` é executado
Then cada item de `PublicationQueue` persistido tem `Caption` correspondente à rede correta, comprovando que não há sobrescrita entre redes.

**CA17 — Regressão: teste antigo de "não chamar quando não elegível" continua válido**
Given um produto não elegível para geração de legenda (cenário já coberto antes da correção)
When `ProcessorJob.ExecuteAsync()` é executado
Then `GenerateCaptionAsync` não é chamado e nenhum item de `PublicationQueue` recebe `Caption` fora do default vazio — teste original de `Times.Never` é preservado, mas complementado pela asserção de estado persistido.

## Changelog / registro histórico

**CA18 — Nota de changelog sobre ausência de backfill**
Given o PR desta correção
When revisado
Then contém uma linha explícita no changelog/descrição do PR mencionando que publicações anteriores à migration permanecem sem legenda de IA (sem backfill), para contexto histórico.
