# Design — ISSUE-130: fix: Legenda de IA nunca é persistida

## Visão geral
`ProcessorJob.CreatePublicationQueueEntriesAsync` já chama `_aiService.GenerateCaptionAsync(product, network, ct)`
por rede social, mas descarta o retorno (linha 256 de `ProcessorJob.cs`) — o resultado nunca é atribuído a nada.
Os 4 publishers automáticos e o Facebook Manual (dashboard) leem `product.AiCaption` (campo único por produto,
nunca escrito por nenhum caminho de código ativo), então todo post sai sem legenda. A correção:
1. Novo campo `PublicationQueue.Caption` (persistido por item, ou seja, por rede) — fonte de verdade.
2. `ProcessorJob` passa a atribuir o retorno de `GenerateCaptionAsync` ao construir cada `PublicationQueue`.
3. Os 4 publishers passam a ler `item.Caption` (do próprio `PublicationQueue` sendo publicado) em vez de
   `product.AiCaption`.
4. `ProductDetailDto`/`ProductDetail` (Facebook Manual) passam a expor/consumir a caption real do item de
   `PublicationQueue` da rede Facebook, em vez de `product.description`/`product.Description`.
5. `Product.AiCaption` é mantido no schema (sem remoção — reduz risco/escopo da migration), mas deixa de ser
   lido por qualquer publisher ou pelo Facebook Manual (CA5).

## Componentes envolvidos
- Backend (.NET 8): `PublicationQueue.cs` (entidade), `PublicationQueueConfiguration.cs` (EF config), nova
  migration, `ProcessorJob.cs`, `TelegramPublisher.cs`, `YoutubePublisher.cs`, `InstagramPublisher.cs`,
  `TikTokPublisher.cs`, `ProductDtos.cs` (`ProductDetailDto`), `ProductsController.cs`,
  `ProcessorJobTests.cs`.
- Frontend (Angular, dashboard): `products.service.ts` (`ProductDetail`), `facebook-manual.component.ts`,
  `facebook-manual.component.html`.
- Sem telas novas, sem componentes de UI novos — apenas troca da fonte de dados de um texto já existente na
  tela (campo `caption`/`aiCaption` em vez de `description`). **Não aciona UX/UI.**

## Stack
.NET 8 (EF Core 8, PostgreSQL 16) + Angular 17+ (dashboard). Sem integrações externas novas.

## Fluxo de dados (pós-fix)
1. `ProcessorJob.ExecuteAsync` → para cada produto elegível → `CreatePublicationQueueEntriesAsync`.
2. Para cada rede habilitada com credenciais e elegibilidade OK: `caption = await _aiService
   .GenerateCaptionAsync(product, network, ct)` → `new PublicationQueue(product.Id, network, scheduledAt,
   caption)` (novo parâmetro no construtor) → `_dbContext.PublicationQueues.Add(entry)`.
3. `PublisherJob` (inalterado neste fix) dispara cada publisher passando o `PublicationQueue item`
   correspondente.
4. Cada publisher automático lê `item.Caption` (não mais busca em `product.AiCaption`).
5. Facebook Manual: `ProductsController.GetProduct` passa a buscar (além do `Product`) o `PublicationQueue`
   mais recente do produto para a rede Facebook e inclui sua `Caption` no `ProductDetailDto` como
   `AiCaption` (novo campo, `[JsonPropertyName("ai_caption")]`). Frontend consome `product.aiCaption` no
   botão "copiar legenda" e no texto exibido, com fallback para string vazia quando ausente (CA14).

## Sub-issues
- **Sub-A (backend, bloqueante):** migration + `PublicationQueue.Caption` + `ProcessorJob` + 4 publishers +
  `ProductDetailDto`/`ProductsController` (expõe `ai_caption`) + `ProcessorJobTests.cs` corrigido.
- **Sub-B (frontend, depende do contrato do DTO da Sub-A):** `ProductDetail`/`ProductsService` (Angular)
  consome `ai_caption` e o `facebook-manual.component` passa a usar esse campo no lugar de `description`,
  com fallback (CA14).

## Decisão UX/UI
Não aciona o UX/UI: nenhuma tela nova, nenhum componente novo, nenhuma mudança de layout — apenas a fonte de
dados de um texto (`caption`) que já é exibido e copiado na tela existente do Facebook Manual.
