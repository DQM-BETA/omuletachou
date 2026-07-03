# Tasks — ISSUE-2: Domain, EF Core e Schema de Banco

> Devs: ler este arquivo antes de começar. Paths de docs e spec em cada task.
> Spec completa: `documentacoes/ISSUE-2-domain-efcore-schema/especificacao-tecnica.md`
> PRD: `documentacoes/ISSUE-2-domain-efcore-schema/prd.md`

---

## T-01 — Domain: Entidades, Enums e Interfaces

**Stack:** dotnet
**Projeto:** `backend/src/AfiliadoBot.Domain`
**Branch base:** `desenv`
**Branch de trabalho:** `feature/ISSUE-SUB-<numero>-domain-entities`

### O que fazer

1. Criar entidades em `AfiliadoBot.Domain/Entities/`:
   - `Product.cs` — com propriedades: Id, Title, Description, SalePrice, OriginalPrice, DiscountPct, AffiliateLink, ImageUrl, Slug, Category, Platform, AiScore(int?), AiReason(string?), AiCaption(string?), Status, CreatedAt, UpdatedAt
   - `PublicationQueue.cs` — com propriedades: Id, ProductId, SocialNetwork, Status, ScheduledAt, PublishedAt?, RetryCount(default 0), ErrorMessage?, CreatedAt
   - `AppSetting.cs` — com propriedades: Id(int), Key, Value, UpdatedAt
   - `PushSubscription.cs` — com propriedades: Id, Endpoint, P256dh, Auth, CreatedAt
   - `PublicationLog.cs` — com propriedades: Id, PublicationQueueId, SocialNetwork, AttemptedAt, Success(bool), ErrorMessage?
   - Propriedades de navegação entre entidades (ver spec)

2. Criar enums em `AfiliadoBot.Domain/Enums/`:
   - `Platform.cs`: `Amazon`, `MercadoLivre`, `Shopee`
   - `SocialNetwork.cs`: `Telegram`, `Youtube`, `Instagram`, `TikTok`, `Facebook`
   - `ProductStatus.cs`: `Pending`, `Queued`, `Published`, `Rejected`
   - `PublicationStatus.cs`: `Scheduled`, `Published`, `Failed`

3. Criar interfaces em `AfiliadoBot.Domain/Interfaces/`:
   - `IPlatformCollector.cs`
   - `ISocialPublisher.cs`
   - `IMediaStorage.cs`
   - `IAiService.cs`
   - (ver assinaturas completas na spec, seção 3)

4. Adicionar testes em `backend/src/AfiliadoBot.Tests/Domain/`:
   - `ProductTests.cs` — validações de SalePrice, DiscountPct, AffiliateLink
   - `ProductStatusTransitionTests.cs` — Pending→Rejected (score < 6), Pending→Queued (score >= 6)
   - `PublicationStatusTransitionTests.cs` — Scheduled→Published/Failed; RetryCount >= 3 bloqueia retentativa
   - `ClaudeAiServiceTests.cs` — mock AnthropicClient; score abaixo do threshold → Approve=false; deserialização JSON correta

**Regra crítica:** o projeto `AfiliadoBot.Domain` NÃO deve referenciar nenhum NuGet de infraestrutura (sem EF Core, sem Anthropic.SDK). Verificar com `dotnet build backend/src/AfiliadoBot.Domain` isolado.

### Critérios de aceite (Given/When/Then)

**CA-5:** Given `AfiliadoBot.Tests` com testes de domínio / When `dotnet test` / Then 0 falhas, cobertura dos cenários de Product, ProductStatus, PublicationStatus e ClaudeAiService

**CA-6:** Given o projeto `AfiliadoBot.Domain` / When `dotnet build AfiliadoBot.Domain` / Then compila sem erros; arquivos `IPlatformCollector.cs`, `ISocialPublisher.cs`, `IMediaStorage.cs`, `IAiService.cs` existem em `Interfaces/`; sem referências a NuGet de infra

**CA-9:** Given o código compilado / When inspecionar `AfiliadoBot.Domain/Enums/` / Then `Platform` = {Amazon, MercadoLivre, Shopee}; `SocialNetwork` = {Telegram, Youtube, Instagram, TikTok, Facebook}; sem sobreposição

### Contexto técnico
- Spec completa: `documentacoes/ISSUE-2-domain-efcore-schema/especificacao-tecnica.md` seções 1, 2, 3, 5
- Stack: .NET 8, xUnit, sem NuGet de infra no Domain
- CLAUDE.md do repo: `repos/omuletachou/CLAUDE.md`

---

## T-02 — Infrastructure: DbContext, Migrations e Seeds

**Stack:** dotnet
**Projeto:** `backend/src/AfiliadoBot.Infrastructure`
**Branch base:** `desenv`
**Branch de trabalho:** `feature/ISSUE-SUB-<numero>-infrastructure-efcore`
**Dependência:** T-01 deve estar merged em `desenv` antes de iniciar (entidades devem existir)

### O que fazer

1. Criar `AfiliadoBotDbContext` em `AfiliadoBot.Infrastructure/Data/`:
   - DbSets para: Products, PublicationQueues, AppSettings, PushSubscriptions, PublicationLogs
   - `OnModelCreating` com `ApplyConfigurationsFromAssembly`

2. Criar configurations Fluent API em `AfiliadoBot.Infrastructure/Data/Configurations/`:
   - `ProductConfiguration.cs` — tabela `products`, nomes de colunas em snake_case, slug UNIQUE, ai_score INT, ai_reason VARCHAR(300), category VARCHAR(100)
   - `PublicationQueueConfiguration.cs` — tabela `publication_queue`, FK product_id ON DELETE CASCADE, índice composto (status, scheduled_at)
   - `AppSettingConfiguration.cs` — tabela `app_settings`, key UNIQUE
   - `PushSubscriptionConfiguration.cs` — tabela `push_subscriptions`, endpoint UNIQUE
   - `PublicationLogConfiguration.cs` — tabela `publication_logs`

3. Implementar `IDesignTimeDbContextFactory<AfiliadoBotDbContext>` para suporte a `dotnet ef` em design-time

4. Gerar migration: `dotnet ef migrations add InitialSchema --project AfiliadoBot.Infrastructure --startup-project AfiliadoBot.Api`

5. Adicionar seeds de `AppSetting` via `HasData` na configuration (30 registros — lista completa na spec seção 4.4):
   - Credenciais com `Value = ""`
   - Campos não-sensíveis com valores reais (schedule crons, max_per_day, min_score, networks.*.enabled)

6. Registrar `AfiliadoBotDbContext` no DI da API (string de conexão via variável de ambiente `ConnectionStrings__DefaultConnection`)

### Critérios de aceite (Given/When/Then)

**CA-1:** Given container `db` em execução e migration aplicada / When consultar `information_schema.tables` / Then tabelas `products`, `publication_queue`, `app_settings`, `push_subscriptions` existem

**CA-2:** Given tabela `products` criada / When verificar colunas / Then existem `ai_score INT`, `ai_reason VARCHAR(300)`, `slug VARCHAR(300) UNIQUE`, `category VARCHAR(100)`

**CA-3:** Given tabela `push_subscriptions` / When inserir dois registros com mesmo `endpoint` / Then banco rejeita com UNIQUE constraint violation

**CA-4:** Given seeds aplicados / When `SELECT COUNT(*) FROM app_settings` / Then resultado >= 25; campos não-sensíveis têm valores reais; credenciais têm valor `""`

**CA-7:** Given AppDbContext configurado com string de conexão / When `docker compose up` e `GET localhost:5000/health` / Then API inicia sem exceção e retorna 200

**CA-8:** Given migration aplicada / When consultar `pg_indexes` e `information_schema.table_constraints` / Then FK `product_id → products.id` com ON DELETE CASCADE e índice composto `(status, scheduled_at)` existem

### Contexto técnico
- Spec completa: `documentacoes/ISSUE-2-domain-efcore-schema/especificacao-tecnica.md` seções 4, 6
- Stack: .NET 8, EF Core 8, Npgsql, PostgreSQL 16
- CLAUDE.md do repo: `repos/omuletachou/CLAUDE.md`
- Padrões obrigatórios: `HasColumnName` (snake_case), `ToTable`, enums como int, timestamps como timestamptz
- Nomes de coluna no banco: ver spec seção 4.2 (cada configuration tem o mapeamento completo)
