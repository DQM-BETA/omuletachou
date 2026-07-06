# Task Breakdown — Issue #5: Collectors MercadoLivre e Shopee

## Decisão de escopo (LT)

### CollectorJob orquestrador
**Não incluído nesta Issue.** Seguindo a recomendação do PM: a Issue #4 já estabeleceu o padrão de endpoint manual de trigger por collector (`POST /api/jobs/collector/{platform}/trigger`), sem job orquestrador Hangfire. Um job que itere Amazon + ML + Shopee em sequência com captura de exceção isolada é natural para uma issue futura de "Scheduler"/orquestração de jobs — a independência entre collectors (critério 20) é validada nesta Issue via teste unitário/integração direta (chamar os dois `CollectAsync` em sequência no teste, sem precisar do job), não via um orquestrador novo em produção.

### Divisão em sub-issues e migration
**Uma única migration, feita como parte do T-01.** Migrations concorrentes de dois PRs paralelos alterando o mesmo schema (`Products`) criam risco real de conflito de migration history no EF Core (duas migrations, ordem de apply ambígua, `ModelSnapshot` divergente). É mais simples e seguro:
- **T-01** cria a migration (`AffiliateLink` nullable + `MediaUrl`/`MediaType`) e implementa `MercadoLivreCollector` completo.
- **T-02** depende de T-01 estar mergeado em `desenv` (migration já aplicada) e implementa `ShopeeCollector`, apenas consumindo os campos já existentes — sem tocar em `Product.cs` além de usar os setters/parâmetros já expostos pela mudança do T-01.

Isso resulta em execução **sequencial** (T-01 → merge → T-02), não paralela, mas evita reconciliar duas migrations concorrentes — troca deliberada de paralelismo por segurança de schema, que se paga em uma tabela nova (Products) com poucos registros de produção ainda.

### Link de afiliado da Shopee
Confirmando a interpretação do PRD: a Shopee retorna `offerLink` pronto na resposta do produto (sem custo de chamada extra), então o `ShopeeCollector` preenche `AffiliateLink` diretamente na criação. Isso é **diferente** do ML (que fica `null` até aprovação do scoring pelo `ProcessorJob`), mas não fere a regra de negócio "only-scored-products-get-links" — a regra do Gate 1 foi motivada pelo custo/limite de chamadas extras à API do ML para gerar o link; a Shopee não tem esse custo porque o link já vem pronto no mesmo payload da busca. Documentado explicitamente para evitar retrabalho no Code Review.

---

## T-01: MercadoLivreCollector — OAuth2, Cache de Token, Migration, Scoring

### Contexto técnico
- Repo: `DQM-BETA/omuletachou`, branch base `desenv`
- Novo arquivo: `backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/MercadoLivreCollector.cs`
- Referência de padrão: `backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/AmazonCollector.cs` (estrutura de classe, retry, upsert, scoring)
- Entidade a alterar: `backend/src/AfiliadoBot.Domain/Entities/Product.cs`
  - Construtor: remover o fail-fast de `affiliateLink` (linhas 53-54); tornar `AffiliateLink` propriedade `string?`
  - Adicionar `MediaUrl` (`string?`) e `MediaType` (`string?`) como propriedades
  - `UpdateFromCollector`: adicionar parâmetros opcionais `mediaUrl`/`mediaType` mantendo compatibilidade com a chamada atual do `AmazonCollector` (que não usa os novos campos)
- Migration nova via `dotnet ef migrations add` no projeto de Infrastructure (schema PostgreSQL)
- `IPlatformCollector.CollectAsync(CancellationToken ct)` — mesmo contrato do `AmazonCollector`
- Endpoint manual: seguir o padrão de trigger da Issue #4 (`POST /api/jobs/collector/mercadolivre/trigger`)

### O que fazer
1. Migration EF Core: `AffiliateLink` nullable, `MediaUrl` (nullable), `MediaType` (nullable) na tabela `Products`
2. Ajustar `Product.cs`: construtor aceita `affiliateLink` null/vazio sem exception; novos campos `MediaUrl`/`MediaType`; `UpdateFromCollector` aceita e atualiza os novos campos
3. `MercadoLivreCollector : IPlatformCollector`:
   - `LoadSettingsAsync`: ler `mercadolivre.client_id`, `mercadolivre.client_secret`, `mercadolivre.access_token`, `mercadolivre.token_expires_at` de `app_settings`
   - `EnsureValidTokenAsync`: se `access_token` ausente OU `token_expires_at` no passado ou a menos de 5 min do agora → `POST https://api.mercadolibre.com/oauth/token` (grant_type=client_credentials, client_id, client_secret) e persistir `mercadolivre.access_token` + `mercadolivre.token_expires_at` (now + expires_in) em `app_settings` antes de prosseguir
   - Busca: `GET https://api.mercadolibre.com/sites/MLB/search?sort=best_seller&limit=20` com header `Authorization: Bearer {access_token}`
   - Mapear cada item: `id` → `ExternalId`; `title` → `Title`; `price` → `SalePrice`; `original_price` (fallback `price` se ausente) → `OriginalPrice`; `thumbnail` → `MediaUrl` com `MediaType = "image"`
   - Upsert por `(Platform.MercadoLivre, ExternalId)`; produto novo: `AffiliateLink = null`, aciona `IAiService.ScoreProductAsync` e `UpdateAiResult`; produto existente: `UpdateFromCollector` sem re-scoring
   - Rate limiting: `Task.Delay(150)` entre chamadas HTTP subsequentes (10 req/s)
   - Retry 3x backoff (2s/4s/8s) em HTTP 429 (mesmo padrão de `AmazonCollector.SendWithRetryAsync`); após esgotar, `_logger.LogWarning` e aborta sem exception
   - Falha de rede/status não-sucesso (não-429): mesmo tratamento — log warning, aborta sem exception
   - Fail-fast: `InvalidOperationException` com mensagem identificando exatamente a chave ausente entre `mercadolivre.client_id`, `mercadolivre.client_secret` (verificado antes de qualquer chamada HTTP)
4. Endpoint manual `POST /api/jobs/collector/mercadolivre/trigger` seguindo o padrão já criado para Amazon na Issue #4
5. Testes com `HttpMessageHandler` mockado (sem chamadas HTTP reais), cobrindo os cenários 1-10 dos critérios de aceite + 21-23 (mudança de contrato em `Product`) + regressão zero nos testes existentes do `AmazonCollector` (critério 22)

### Critérios de aceite
Ver `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/criterios-aceite.md`, cenários 1-10 e 21-24.

---

## T-02: ShopeeCollector — HMAC-SHA256, GraphQL, Fallback de Mídia

**Depende de T-01 mergeado em `desenv`** (migration `MediaUrl`/`MediaType`/`AffiliateLink nullable` já aplicada).

### Contexto técnico
- Novo arquivo: `backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/ShopeeCollector.cs`
- Mesma referência de padrão: `AmazonCollector.cs`
- Não altera `Product.cs` (campos já existem via T-01)

### O que fazer
1. `ShopeeCollector : IPlatformCollector`:
   - `LoadSettingsAsync`: ler `shopee.app_id`, `shopee.secret`, `shopee.affiliate_id` de `app_settings`
   - Fail-fast: `InvalidOperationException` identificando a chave exata ausente, antes de qualquer chamada HTTP
   - Assinatura HMAC-SHA256: calcular sobre o payload da requisição usando `shopee.secret`; header `Authorization: SHA256 {signature}`
   - Query GraphQL: `getProducts(sortType: 2, limit: 20)` contra `https://open-api.affiliate.shopee.com.br/graphql`
   - Mapear cada item: `productId` → `ExternalId`; `productName` → `Title`; `priceMin` → `SalePrice`; `originalPrice` (fallback `priceMin`) → `OriginalPrice`; `offerLink` → `AffiliateLink` (preenchido diretamente, diferente do ML — ver decisão acima)
   - Mídia: se vídeo disponível → `MediaType = "video"`, `MediaUrl` = URL do vídeo; senão, se `productImage` disponível → `MediaType = "image"`, `MediaUrl = productImage`; senão → `MediaUrl = null`, `MediaType = null` (produto salvo mesmo assim, não descartado)
   - Upsert por `(Platform.Shopee, ExternalId)`; produto novo aciona `IAiService.ScoreProductAsync` + `UpdateAiResult`; produto existente usa `UpdateFromCollector` sem re-scoring
   - Rate limiting: `Task.Delay(1000)` entre chamadas HTTP subsequentes
   - Retry 3x backoff (2s/4s/8s) em HTTP 429; falha de rede/status não-sucesso (não-429): log warning, aborta sem exception
2. Endpoint manual `POST /api/jobs/collector/shopee/trigger`
3. Testes com `HttpMessageHandler` mockado cobrindo os cenários 11-20 dos critérios de aceite (incluindo independência entre collectors — critério 20, testável chamando `MercadoLivreCollector.CollectAsync` com credenciais ausentes seguido de `ShopeeCollector.CollectAsync` válido no mesmo teste) + 24

### Critérios de aceite
Ver `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/criterios-aceite.md`, cenários 11-20 e 24.
