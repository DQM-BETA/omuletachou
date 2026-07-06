# Task Breakdown — ISSUE-4 Collector Amazon (PAAPI v5 + Scoring)

> Stack única (dotnet) — sem ambiguidade arquitetural residual após Gate 1 + PM Fase 2. Uma única task porque o escopo é coeso (um collector + sua migration).

## T-01: AmazonCollector — PAAPI v5, AWS SigV4, Scoring automático

**Sub-issue GitHub:** ver `estado.md` (`sub_issues`)
**Stack:** dotnet (.NET 8, EF Core, HttpClient)

### O que fazer
1. **Migration (EF Core)**
   - Adicionar `ExternalId` (`string`, not null) à entidade `Product` (`AfiliadoBot.Domain/Entities/Product.cs`).
   - Adicionar índice único composto `(Platform, ExternalId)` via Fluent API em `AppDbContext` (`AfiliadoBot.Infrastructure`).
   - Gerar migration EF Core (`dotnet ef migrations add AddExternalIdToProduct`).
   - **Atenção a dados pré-existentes**: se a tabela `Products` já tiver linhas em ambientes de homolog/main, definir estratégia de backfill (ex.: valor transitório único por linha, tipo `Guid.NewGuid().ToString()`, antes de aplicar `NOT NULL` + índice único) para não quebrar a migration. Se o ambiente ainda estiver vazio (dev local/CI), não é necessário.
   - Adicionar método de upsert na entidade, ex. `UpdateFromCollector(decimal salePrice, decimal originalPrice, decimal discountPct, string? mediaUrl)`: atualiza `SalePrice`, `OriginalPrice`, `DiscountPct`, `ImageUrl`/`MediaUrl` e `UpdatedAt`; preserva `Id`, `Status`, `AiScore`, `Slug`, `CreatedAt` (equivalente a `CollectedAt`). **Não** reexecuta scoring.
   - Adicionar `ExternalId` como parâmetro no construtor de `Product` (produto novo já nasce com o ASIN).

2. **`AmazonCollector : IPlatformCollector`** em `AfiliadoBot.Infrastructure/Integrations/Platforms/AmazonCollector.cs`
   - `Platform => Platform.Amazon`.
   - `CollectAsync(CancellationToken ct)`:
     a. Ler config de `app_settings`: `amazon.access_key`, `amazon.secret_key`, `amazon.partner_tag`, `amazon.marketplace`, `amazon.max_results` (padrão 20).
     b. **Fail-fast** (antes de qualquer chamada HTTP): se `access_key`, `secret_key` ou `partner_tag` vazios/ausentes → `InvalidOperationException` com mensagem descritiva. Se `marketplace != "www.amazon.com.br"` → `InvalidOperationException("Marketplace não suportado nesta versão")`.
     c. Montar request `SearchItems` para `webservices.amazon.com.br/paapi5/searchitems`: `Keywords = "oferta do dia"`, `SortBy = Relevance`, `ItemCount = amazon.max_results`, `Resources`: `ItemInfo.Title`, `Offers.Listings.Price`, `Offers.Listings.SavingBasis`, `Images.Primary.Large`.
     d. Assinar a requisição com **AWS Signature V4 manual** (sem SDK AWS) — implementar helper interno (ex. classe `AwsSignatureV4Signer`) para: canonical request, string to sign, assinatura HMAC-SHA256, headers `Authorization`/`X-Amz-Date`/`X-Amz-Content-Sha256`.
     e. Enviar via `HttpClient` injetado (via `IHttpClientFactory` ou client nomeado).
     f. **Retry em HTTP 429**: até 3 tentativas, esperas de 2s/4s/8s entre tentativas (`Task.Delay`, respeitando `ct`). Se as 3 tentativas falharem: logar `Warning` descritivo e abortar o ciclo retornando os produtos já persistidos até aquele ponto **sem lançar exception**.
     g. Erros HTTP não-429 (timeout, 5xx, DNS): tratar com o mesmo fluxo — logar `Warning`, abortar o ciclo sem exception (decisão do LT, ver PRD "Pontos que seguem para o refinamento técnico").
     h. **Rate limiting**: `Task.Delay(1000, ct)` entre cada requisição feita à PAAPI dentro do mesmo ciclo.
     i. Para cada item retornado: montar `ExternalId` = ASIN, `AffiliateLink = $"https://www.amazon.com.br/dp/{asin}?tag={partnerTag}"`, `SalePrice` de `Offers.Listings.Price`, `OriginalPrice` de `Offers.Listings.SavingBasis` (se ausente, igual a `SalePrice`), `DiscountPct` calculado, `ImageUrl` de `Images.Primary.Large`, `Title` de `ItemInfo.Title`.
     j. **Upsert por `(Platform, ExternalId)`**:
        - Não existe → cria `Product` novo (`Category` pode ficar vazio/"Geral" nesta issue, sem categorização automática em escopo) e chama `IAiService.ScoreProductAsync(product, ct)` → `product.UpdateAiResult(score, reason, caption ou string vazio)`.
        - Já existe → chama o método de upsert (`UpdateFromCollector`), **sem** chamar `ScoreProductAsync` novamente.
     k. Persistir via EF Core (`DbContext.SaveChangesAsync`).
     l. Zero resultados → retorna coleção vazia, sem exception, sem log de erro.
     m. Retornar a coleção de produtos processados no ciclo (novos + atualizados).

3. **Endpoint/Job Hangfire**
   - `POST /api/jobs/collector/trigger`: endpoint que enfileira/dispara `CollectorJob` (Hangfire) que chama `AmazonCollector.CollectAsync(ct)` uma única vez. Sem agendamento recorrente (fora de escopo).
   - Job também deve estar disponível/acionável via dashboard Hangfire.

4. **Testes unitários**
   - Mock de `HttpMessageHandler` (nunca chamada real à PAAPI).
   - Cobrir todos os critérios de aceite (ver `criterios-aceite.md`, itens 1 a 12): coleta com sucesso, fail-fast credenciais, fail-fast marketplace, scoring de produto novo, deduplicação, upsert de preço/mídia, retry 429 (2s/4s/8s), aborto sem exception após esgotar retries, rate limiting (`Task.Delay(1000)` entre chamadas — pode validar via abstração/mock de delay para não estourar tempo de teste), zero resultados, acionamento do job.

### Critérios de aceite
Ver `documentacoes/ISSUE-4-collector-amazon/criterios-aceite.md` (itens 1–12, Given/When/Then completos).

### Contexto técnico
- Design/PRD: `documentacoes/ISSUE-4-collector-amazon/prd.md`
- Interface: `backend/src/AfiliadoBot.Domain/Interfaces/IPlatformCollector.cs` (já existente, não alterar assinatura)
- Entidade: `backend/src/AfiliadoBot.Domain/Entities/Product.cs`
- Referência de padrão de retry/uso de `IAiService`: `backend/src/AfiliadoBot.Infrastructure/Services/ClaudeAiService.cs`
- Repo: `repos/omuletachou`, branch base: `desenv`
- Branch da sub-issue: `feature/ISSUE-<SUB>-amazon-collector` (SUB = número da sub-issue no GitHub)
