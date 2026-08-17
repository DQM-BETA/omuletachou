# Tasks — ISSUE-182: MercadoLivreCollector quebrado — reconstrução com Highlights API

Ordem de dependência: **Sub-A e Sub-B podem rodar em paralelo** (arquivos diferentes —
`MercadoLivreCollector.cs` na Sub-A; `Product.cs`/`ProductStatus.cs`/`ProcessorJob.cs`/
`ProductsController.cs`/`ProductDtos.cs` na Sub-B — sem sobreposição de arquivo). **Sub-C
(dashboard) depende do contrato da Sub-B** (`GET /api/products?status=AwaitingAffiliateLink`,
campo `SourceUrl` em `ProductListItemDto`, `POST /api/products/affiliate-links/import`) — pode ser
codada contra o contrato descrito na especificação técnica em paralelo, mas o merge para `desenv`
deve esperar a Sub-B estar mergeada, para poder validar a integração real (não apenas mock) antes do
PR `desenv→homolog`. Release `homolog→main` espera as 3 prontas juntas.

Referência completa (contratos exatos, trechos de código, paths):
`documentacoes/ISSUE-182-mercadolivrecollector-quebrado/especificacao-tecnica.md` (seções indicadas
em cada sub-tarefa abaixo). Critérios de aceite originais (Given/When/Then):
`documentacoes/ISSUE-182-mercadolivrecollector-quebrado/criterios-aceite.md` — **CA 3.2 e CA 7.1-7.3
foram revisados pela especificação técnica §2.6/§3.7** (endpoint de multi-get e de link de afiliado
não são acessíveis — ver racional completo lá antes de implementar).

## Sub-A — `MercadoLivreCollector`: reconstrução com Highlights API — `stack:dotnet`

### Critérios de aceite
CA 1.1, 1.2 (mapeamento de categorias — já confirmado, só copiar a tabela), CA 2.1, 2.2 (Highlights
por categoria), CA 3.1, 3.3 (resolução de detalhes via `/products/{id}` + `/products/{id}/items` —
CA 3.2 não se aplica mais, ver especificação técnica §2.6), CA 4.1-4.3 (mapeamento para `Product` e
upsert, reaproveitando o que já existe), CA 5.1-5.3 (isolamento de falha por categoria e por
produto), CA 6.1 (frequência inalterada), CA 8.1, 8.2, 8.4 (sem regressão em scoring/categorização/
Amazon/Shopee).

### O que fazer
1. Adicionar `CategoryMap` estático (`Dictionary<string, string[]>`, 8 entradas, valores reais já
   confirmados) ao `MercadoLivreCollector` — especificação técnica §2.5, tabela completa em
   `design.md` §3.4.
2. Reescrever `CollectAsync`: iterar `CategoryMap` → `GET /highlights/MLB/category/{id}` por
   categoria (isolamento de falha: categoria que falha é pulada, log de warning, ciclo continua) →
   para cada `catalog_product_id` retornado, resolver via novo método privado (nome sugerido
   `ResolveAndUpsertAsync`) que chama `GET /products/{id}` + `GET /products/{id}/items` (isolamento
   de falha por produto: produto que falha é pulado, log de warning) — especificação técnica §2.1.
3. Critério de escolha do item quando `/products/{id}/items` retorna mais de um vendedor: menor
   `price` (sem `buy_box_winner` utilizável) — especificação técnica §2.2.
4. `SourceUrl` construído como `https://www.mercadolivre.com.br/p/{catalogProductId}` (não vem do
   campo `permalink`, que retorna vazio) — especificação técnica §1/§2.1. `ExternalId` =
   `catalogProductId` (não `item_id` — precisa ser estável entre ciclos).
5. Antes de codar o parsing de `/products/{id}` e `/products/{id}/items`: rodar as duas chamadas
   localmente (mesma prática já usada no projeto para os collectors existentes) para confirmar os
   nomes exatos dos campos JSON (`name`, `pictures`, `price`, etc.) e procurar um campo de preço
   original/desconto antes de assumir o fallback `OriginalPrice = SalePrice`/`DiscountPct = 0` —
   especificação técnica §2.3. Documentar no PR o payload real observado (mesmo padrão de
   `ValidSearchResponse` em `MercadoLivreCollectorTests.cs`, usado como fixture de teste).
6. Delay defensivo de 300ms entre chamadas HTTP consecutivas ao domínio `api.mercadolibre.com`
   (mantido de `design.md` §5.2, sem rate limiter dedicado) — especificação técnica §2.4.
7. Remover código obsoleto: `SearchUrl`, `SendWithRetryAsync`, `ParseItems`, `RetryDelaysMs`,
   `MercadoLivreItem` (record) — especificação técnica §2.5. Manter sem alteração: autenticação
   OAuth2 (`LoadSettingsAsync`/`ValidateCredentials`/`EnsureValidTokenAsync`/
   `RequestNewTokenAsync`/`PersistTokenAsync`/`UpsertSettingAsync`), `GenerateSlug`,
   `CategoryDetector.Detect`, upsert por `(Platform, ExternalId)`.
8. Reescrever `MercadoLivreCollectorTests.cs` para o novo fluxo (mock de `HttpMessageHandler` já
   usado no arquivo, trocar os payloads fixture de `/sites/MLB/search` para
   `/highlights/MLB/category/{id}` + `/products/{id}` + `/products/{id}/items`), cobrindo:
   Highlights ok, Highlights falha isolada (categoria pulada, demais seguem), produto individual
   falha isolado (produto pulado, demais seguem), upsert de produto existente
   (`UpdateFromCollector`), produto novo (scoring + `CategoryDetector`), mesmo produto em duas
   categorias no mesmo ciclo (upsert único).

### Contexto técnico
- Especificação técnica: `documentacoes/ISSUE-182-mercadolivrecollector-quebrado/especificacao-tecnica.md`
  §1 (permalink), §2 (fluxo completo do collector).
- `design.md` (`openspec/changes/issue-182-mercadolivrecollector-quebrado/design.md`) §3 (Decisão 1,
  `CategoryMap` — reaproveitar tabela exata), §5 (Decisão 3, rate limit — reaproveitar racional),
  §10 (evidência bruta das chamadas testadas ao vivo).
- Arquivo principal: `backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/MercadoLivreCollector.cs`.
- Testes: `backend/src/AfiliadoBot.Tests/Integrations/MercadoLivreCollectorTests.cs`.
- Repo: `repos/omuletachou`. Stack: ASP.NET Core 8 / EF Core 8 / PostgreSQL 16 / xUnit + Moq +
  FluentAssertions (padrão já usado no arquivo de teste atual).

## Sub-B — Fluxo semi-manual de link de afiliado (domínio + API) — `stack:dotnet`

### Critérios de aceite
CA 7.1-7.3 revisados (especificação técnica §3.7 — link não é mais gerado por chamada HTTP desta
aplicação, é colado pelo operador; validação passa a ser sobre o encadeamento de status, não sobre o
conteúdo do link em si).

### O que fazer
1. `ProductStatus` (`backend/src/AfiliadoBot.Domain/Enums/ProductStatus.cs`): adicionar
   `AwaitingAffiliateLink` **ao final** do enum (preserva valores `int` já persistidos, sem
   migration) — especificação técnica §3.3.
2. `Product` (`backend/src/AfiliadoBot.Domain/Entities/Product.cs`): dois métodos novos,
   `MarkAsAwaitingAffiliateLink()` e `ResolveAffiliateLink(string link)` (preenche `AffiliateLink` +
   volta `Status` para `Queued`) — especificação técnica §3.2, código completo lá. Não remover/
   alterar `SetAffiliateLink` existente.
3. `ProcessorJob.EnsureAffiliateLinkAsync` (`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`):
   remover a constante `AffiliateLinkUrl` e toda a chamada HTTP a `affiliate-tools/links`; substituir
   pela chamada a `product.MarkAsAwaitingAffiliateLink()` (sem chamada externa) — especificação
   técnica §3.4, código completo lá. O restante de `ExecuteAsync` não muda. Remover usings que
   ficarem órfãos (checar se ainda são usados por outro método do arquivo antes de remover).
4. `ProductsController` (`backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs`): novo
   endpoint `POST api/products/affiliate-links/import` — especificação técnica §3.5, código
   completo lá (pareamento explícito por `ProductId`, isolamento de falha por item, nunca falha o
   lote inteiro por um item inválido).
5. `ProductDtos.cs` (`backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`): novos records
   `AffiliateLinkImportItem`, `ImportAffiliateLinksRequest`, `AffiliateLinkImportSkip`,
   `ImportAffiliateLinksResult` — especificação técnica §3.5. Adicionar campo `SourceUrl` (nullable,
   aditivo, ao final) em `ProductListItemDto` + projetar `p.SourceUrl` em
   `ProductsController.GetProducts` — especificação técnica §3.5.
6. **Nenhum endpoint novo de listagem é necessário** — `GET /api/products?status=AwaitingAffiliateLink`
   já funciona via o filtro genérico existente (`Enum.TryParse<ProductStatus>`). Confirmar com um
   teste que o filtro aceita o novo valor do enum.
7. Testes: `ProcessorJobTests` (produto ML sem `AffiliateLink` e com `SourceUrl` vira
   `AwaitingAffiliateLink`, não `Error`; produto ML sem `SourceUrl` continua indo para `Error`;
   produto não-ML ou já com `AffiliateLink` não é afetado). Testes de controller para
   `ImportAffiliateLinks`: import de item válido (`AwaitingAffiliateLink` → `Queued`,
   `AffiliateLink` preenchido), item com `ProductId` inexistente (pulado, no `Skipped`), item cujo
   produto não está `AwaitingAffiliateLink` (pulado, não sobrescreve `AffiliateLink` existente), item
   com link vazio (pulado), lote misto (alguns importados, alguns pulados, resposta reflete os dois).

### Contexto técnico
- Especificação técnica: `documentacoes/ISSUE-182-mercadolivrecollector-quebrado/especificacao-tecnica.md`
  §3 inteira (contratos exatos, código completo de cada método/endpoint).
- Arquivos: `backend/src/AfiliadoBot.Domain/Enums/ProductStatus.cs`,
  `backend/src/AfiliadoBot.Domain/Entities/Product.cs`,
  `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`,
  `backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs`,
  `backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`.
- Testes: `backend/src/AfiliadoBot.Tests` (seguir a mesma estrutura de pastas/nomeação já usada para
  `ProcessorJob`/`ProductsController`, ver testes existentes desses componentes).
- Repo: `repos/omuletachou`. Stack: ASP.NET Core 8 / EF Core 8 / xUnit + Moq + FluentAssertions.
- **Sem migration** — reforçar isso no PR (`ProductStatus` é `int` convertido, valor novo ao final
  do enum não quebra dados existentes).

## Sub-C — Dashboard: tela "Links de Afiliado — Mercado Livre" — `stack:angular`

### Critérios de aceite
Não há CA formal dedicado em `criterios-aceite.md` a esta tela (ela nasce da resolução do Gate 1.5,
posterior à Fase 2 do PM) — critério de aceite funcional está na especificação técnica §3.6
(schema mínimo do fluxo de importação).

### O que fazer
1. Componente standalone `dashboard/src/app/pages/mercadolivre-links/mercadolivre-links.component.ts`
   — precedente direto a seguir: `dashboard/src/app/pages/facebook-manual/` (mesma estrutura:
   lista de itens pendentes de ação manual, botão de confirmação, feedback via `MatSnackBar`).
2. Rota `/mercadolivre-links` em `dashboard/src/app/app.routes.ts` (lazy-loaded, mesmo padrão de
   `facebook-manual`) + item de navegação em
   `dashboard/src/app/core/shell/shell.component.ts` (array `navItems`).
3. Fluxo funcional completo — especificação técnica §3.6: carregar
   `GET /api/products?status=AwaitingAffiliateLink&pageSize=200`; estado vazio quando não há
   pendências; lista de produtos com `sourceUrl` visível + botão "Copiar todas as URLs”
   (`navigator.clipboard.writeText`, uma URL por linha, mesmo padrão de `copyCaption` em
   `facebook-manual.component.ts`); `<textarea>` para colar os links de volta; botão "Importar" que
   faz split por linha, pareia por índice com a lista já carregada (client-side, não confia em
   ordenação do servidor), bloqueia o envio se a contagem de linhas coladas não bater com a
   contagem de produtos exibidos, monta `{ items: [{ productId, affiliateLink }] }` e chama
   `POST /api/products/affiliate-links/import`; snackbar de resultado (`Imported`/`Skipped`);
   recarrega a lista após importar.
4. Nota textual no componente lembrando que `Jobs` → `Processor`
   (`POST /api/jobs/processor/trigger`, endpoint já existente, ver `jobs.component.ts`) publica
   imediatamente os produtos recém-importados, sem precisar esperar a próxima execução agendada.
5. `dashboard/src/app/core/services/products.service.ts`: adicionar `listAwaitingAffiliateLink()` e
   `importAffiliateLinks(items)`, mesmo padrão HTTP client dos métodos já existentes no serviço
   (ver uso em `facebook-manual.component.ts`).
6. Testes: `mercadolivre-links.component.spec.ts` (seguir `facebook-manual.component.spec.ts` como
   referência) — lista carregada e exibida, estado vazio, cópia de URLs, validação de contagem de
   linhas antes do envio, sucesso de importação (lista recarrega, itens importados somem), erro de
   importação (mensagem exibida, lista não é limpa incorretamente).

### Contexto técnico
- Especificação técnica: `documentacoes/ISSUE-182-mercadolivrecollector-quebrado/especificacao-tecnica.md`
  §3.6 (contrato funcional completo da tela).
- Precedente de código a seguir de perto: `dashboard/src/app/pages/facebook-manual/` (component,
  html, scss, spec — os 4 arquivos, mesma composição).
- Contrato de API consumido (depende da Sub-B):
  `GET /api/products?status=AwaitingAffiliateLink` (retorna `ProductListItemDto[]`, agora com
  `sourceUrl`), `POST /api/products/affiliate-links/import` (body/response documentados na
  especificação técnica §3.5).
- Repo: `repos/omuletachou`. Stack: Angular 17+, Angular Material, mesmo padrão de testes já usado
  no projeto (ver `facebook-manual.component.spec.ts`).
- **Layout/composição visual da tela**: revisar com UX/UI antes de implementar (a sessão principal
  spawna o UX/UI antes desta sub-issue) — este documento define o contrato funcional/de dados, não
  o desenho visual.

## Sub-D — Fix: isolamento de falha em `GetJsonAsync` (achado do `/code-review` estático, PR #189) — `stack:dotnet`

> Adicionada após o merge do PR #189 (`desenv→homolog`). Achado 1 do comentário
> https://github.com/DQM-BETA/omuletachou/pull/189#issuecomment-5319794063 — sub-issue #190.

### Critérios de aceite
Ver #190 (CA 1-3, Given/When/Then completos no corpo da sub-issue).

### O que fazer
1. `JsonDocument.Parse(body)` em `GetJsonAsync` (linha ~409) roda fora do `try/catch` que envolve o
   restante do método — mover para dentro do `try` (ou `try/catch` próprio), capturar `JsonException`
   e relançar como `MercadoLivreApiException` (mesmo padrão já usado no método para falha de rede e
   HTTP não-2xx).
2. TDD obrigatório: teste que reproduz o bug primeiro (corpo malformado com HTTP 200, mock de
   `HttpMessageHandler`), confirmar falha, corrigir, confirmar sucesso.
3. Não alterar o contrato público do método.

### Contexto técnico
- Arquivo: `backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/MercadoLivreCollector.cs`
  (`GetJsonAsync`, linhas ~388-410).
- Testes: `backend/src/AfiliadoBot.Tests/Integrations/MercadoLivreCollectorTests.cs`.
- Branch: `feature/ISSUE-190-json-parse-try-catch`, base `desenv`.

## Achado 2 (PR #189, comentário `/code-review`) — NÃO endereçado nesta rodada, escalado

`DiscountPct = 0` fixo para todos os produtos coletados do Mercado Livre (Highlights API não expõe
preço original/desconto) colide com o critério "Desconto real mínimo de 15%" do prompt de
`ClaudeAiService.ScoreProductAsync` — na prática reprova sistematicamente todo produto ML. Análise
completa e decisão de encaminhamento em `documentacoes/ISSUE-182-mercadolivrecollector-quebrado/estado.md`
(bloco `blockers`) e no comentário da Issue #182. Qualquer correção técnica avaliada (ex.: omitir o
campo desconto do prompt/scoring quando a plataforma não tem esse dado) produz o mesmo efeito
prático de isentar o canal ML do critério de desconto mínimo — decisão de negócio, não técnica.
Encaminhado ao Gerente via PM Fase 2, **sem sub-issue criada** até a decisão.
