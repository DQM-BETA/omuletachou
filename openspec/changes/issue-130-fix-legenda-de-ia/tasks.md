# Tasks — ISSUE-130: fix: Legenda de IA nunca é persistida

## Sub-A (#139) — backend, bloqueante — `stack:dotnet`

### Critérios de aceite
CA1, CA2 (migration/legado), CA3-CA6 (ProcessorJob), CA7-CA11 (4 publishers), CA12 (ProductDetailDto),
CA15-CA17 (ProcessorJobTests.cs), CA18 (changelog) — ver
`documentacoes/ISSUE-130-fix-legenda-de-ia/criterios-aceite.md`.

### Contexto técnico
- Especificação completa (contratos exatos, trechos de código, ordem sugerida): seções 1-4, 6, 8 de
  `documentacoes/ISSUE-130-fix-legenda-de-ia/especificacao-tecnica.md`.
- Arquivos a alterar:
  - `backend/src/AfiliadoBot.Domain/Entities/PublicationQueue.cs` (nova propriedade `Caption`, construtor
    com 4º parâmetro).
  - `backend/src/AfiliadoBot.Infrastructure/Data/Configurations/PublicationQueueConfiguration.cs` (mapear
    coluna `caption`).
  - Nova migration EF Core (`dotnet ef migrations add AddCaptionToPublicationQueue`).
  - `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs` (`CreatePublicationQueueEntriesAsync`:
    persistir o retorno de `GenerateCaptionAsync`).
  - `backend/src/AfiliadoBot.Infrastructure/Integrations/Social/TelegramPublisher.cs`,
    `YoutubePublisher.cs`, `InstagramPublisher.cs`, `TikTokPublisher.cs` (ler `item.Caption` em vez de
    `product.AiCaption`).
  - `backend/src/AfiliadoBot.Api/Products/ProductDtos.cs` (`ProductDetailDto` ganha `ai_caption`).
  - `backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs` (`GetProduct`: buscar
    `PublicationQueue` da rede Facebook mais recente do produto).
  - `backend/src/AfiliadoBot.Tests/Jobs/ProcessorJobTests.cs` (mock de `GenerateCaptionAsync` retorna valor
    por rede; asserts de `Caption` persistida; teste de múltiplas redes sem sobrescrita).
- Sem backfill (CA2) — não criar script/task de migração de dados históricos.
- PR obrigatoriamente com linha de changelog sobre ausência de backfill (CA18, texto sugerido na seção 8 da
  especificação técnica).
- **Este contrato (`ai_caption` no `ProductDetailDto`) é o que a Sub-B consome — não alterar o nome do campo
  sem avisar/atualizar a Sub-B.**

## Sub-B (#140) — frontend, depende da Sub-A — `stack:angular`

### Critérios de aceite
CA13, CA14 — ver `documentacoes/ISSUE-130-fix-legenda-de-ia/criterios-aceite.md`.

### Contexto técnico
- Especificação completa: seção 5 de `documentacoes/ISSUE-130-fix-legenda-de-ia/especificacao-tecnica.md`.
- Arquivos a alterar:
  - `dashboard/src/app/core/services/products.service.ts` (`ProductDetail` ganha `ai_caption?: string | null`).
  - `dashboard/src/app/pages/facebook-manual/facebook-manual.component.html` (troca de
    `post.product?.description` por `post.product?.ai_caption`, com fallback para ausência/vazio — CA14).
  - `dashboard/src/app/pages/facebook-manual/facebook-manual.component.spec.ts` (ajustar mocks de
    `ProductDetail` usados nos testes existentes).
  - `facebook-manual.component.ts`: nenhuma mudança de lógica esperada (a troca é só no template).
- **Não iniciar antes de a Sub-A estar mergeada em `desenv`** (o contrato do DTO precisa existir de fato —
  evita retrabalho se o nome do campo mudar durante a implementação da Sub-A).

## Decisão UX/UI
Não aciona o UX/UI — nenhuma tela nova, nenhum componente novo, nenhuma mudança de layout. É apenas a troca
da fonte de dados de um texto já existente na tela (campo de legenda) — ver seção "Decisão UX/UI" em
`openspec/changes/issue-130-fix-legenda-de-ia/design.md`.
