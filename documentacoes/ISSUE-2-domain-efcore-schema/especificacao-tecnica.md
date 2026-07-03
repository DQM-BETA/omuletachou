# Especificação Técnica — ISSUE-2: Domain, EF Core e Schema de Banco

## Projetos envolvidos

| Projeto | Caminho | Responsabilidade |
|---|---|---|
| AfiliadoBot.Domain | `backend/src/AfiliadoBot.Domain` | Entidades, enums, interfaces de contrato |
| AfiliadoBot.Infrastructure | `backend/src/AfiliadoBot.Infrastructure` | DbContext, Configurations, Migration, Seeds |
| AfiliadoBot.Tests | `backend/src/AfiliadoBot.Tests` | Testes unitários de domínio |

---

## 1. Entidades de Domínio (`AfiliadoBot.Domain/Entities/`)

> Regra D4: o projeto Domain **não deve referenciar nenhum NuGet de infraestrutura** (sem EF Core, sem PostgreSQL, sem Anthropic.SDK). Apenas tipos do .NET runtime.

### 1.1 `Product`

```csharp
// AfiliadoBot.Domain/Entities/Product.cs
public class Product
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public decimal SalePrice { get; private set; }       // >= 0
    public decimal OriginalPrice { get; private set; }
    public decimal DiscountPct { get; private set; }     // 0..100
    public string AffiliateLink { get; private set; }    // não nulo
    public string ImageUrl { get; private set; }
    public string Slug { get; private set; }             // UNIQUE — VARCHAR(300)
    public string Category { get; private set; }         // VARCHAR(100)
    public Platform Platform { get; private set; }
    public int? AiScore { get; private set; }            // coluna: ai_score INT
    public string? AiReason { get; private set; }        // coluna: ai_reason VARCHAR(300)
    public string? AiCaption { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navegação
    public ICollection<PublicationQueue> PublicationQueues { get; private set; }
}
```

**Invariantes de domínio:**
- `SalePrice >= 0`
- `DiscountPct` entre 0 e 100
- `AffiliateLink` não pode ser nulo ou vazio

### 1.2 `PublicationQueue`

```csharp
// AfiliadoBot.Domain/Entities/PublicationQueue.cs
public class PublicationQueue
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }          // FK → products.id (ON DELETE CASCADE)
    public SocialNetwork SocialNetwork { get; private set; }
    public PublicationStatus Status { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int RetryCount { get; private set; }          // default 0
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navegação
    public Product Product { get; private set; }
    public ICollection<PublicationLog> Logs { get; private set; }
}
```

### 1.3 `AppSetting`

```csharp
// AfiliadoBot.Domain/Entities/AppSetting.cs
public class AppSetting
{
    public int Id { get; private set; }
    public string Key { get; private set; }    // UNIQUE
    public string Value { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
```

### 1.4 `PushSubscription`

```csharp
// AfiliadoBot.Domain/Entities/PushSubscription.cs
public class PushSubscription
{
    public Guid Id { get; private set; }
    public string Endpoint { get; private set; }    // UNIQUE
    public string P256dh { get; private set; }
    public string Auth { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
```

### 1.5 `PublicationLog`

```csharp
// AfiliadoBot.Domain/Entities/PublicationLog.cs
public class PublicationLog
{
    public Guid Id { get; private set; }
    public Guid PublicationQueueId { get; private set; }
    public SocialNetwork SocialNetwork { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Navegação
    public PublicationQueue PublicationQueue { get; private set; }
}
```

---

## 2. Enums (`AfiliadoBot.Domain/Enums/`)

```csharp
// Platform.cs — origem da coleta
public enum Platform { Amazon, MercadoLivre, Shopee }

// SocialNetwork.cs — destino de publicação
public enum SocialNetwork { Telegram, Youtube, Instagram, TikTok, Facebook }

// ProductStatus.cs
public enum ProductStatus { Pending, Queued, Published, Rejected }

// PublicationStatus.cs
public enum PublicationStatus { Scheduled, Published, Failed }
```

**Regra D3:** os dois enums são distintos e sem sobreposição de valores ou membros.

---

## 3. Interfaces de Contrato (`AfiliadoBot.Domain/Interfaces/`)

```csharp
// IPlatformCollector.cs
public interface IPlatformCollector
{
    Platform Platform { get; }
    Task<IEnumerable<Product>> CollectAsync(CancellationToken ct = default);
}

// ISocialPublisher.cs
public interface ISocialPublisher
{
    SocialNetwork Network { get; }
    Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default);
}

// IMediaStorage.cs
public interface IMediaStorage
{
    Task<string> UploadAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string path, CancellationToken ct = default);
}

// IAiService.cs
public interface IAiService
{
    Task<(bool Approve, int Score, string Reason, string Caption)> EvaluateProductAsync(
        Product product, CancellationToken ct = default);
}
```

---

## 4. EF Core — Infrastructure (`AfiliadoBot.Infrastructure/`)

### 4.1 `AfiliadoBotDbContext`

```csharp
// AfiliadoBot.Infrastructure/Data/AfiliadoBotDbContext.cs
public class AfiliadoBotDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<PublicationQueue> PublicationQueues { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
    public DbSet<PushSubscription> PushSubscriptions { get; set; }
    public DbSet<PublicationLog> PublicationLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AfiliadoBotDbContext).Assembly);
    }
}
```

Registrar no DI (em `AfiliadoBot.Api` ou `AfiliadoBot.Infrastructure/DependencyInjection.cs`):
```csharp
services.AddDbContext<AfiliadoBotDbContext>(opt =>
    opt.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
```

### 4.2 Configurations (Fluent API)

Cada entidade tem sua própria classe de configuração em `AfiliadoBot.Infrastructure/Data/Configurations/`.

**ProductConfiguration:**
```
tabela: products
colunas:
  - id: uuid PRIMARY KEY
  - title: varchar(500) NOT NULL
  - description: text NOT NULL
  - sale_price: numeric(18,2) NOT NULL
  - original_price: numeric(18,2) NOT NULL
  - discount_pct: numeric(5,2) NOT NULL
  - affiliate_link: text NOT NULL
  - image_url: text
  - slug: varchar(300) UNIQUE NOT NULL
  - category: varchar(100) NOT NULL
  - platform: int NOT NULL  (enum convertido para int)
  - ai_score: int NULL
  - ai_reason: varchar(300) NULL
  - ai_caption: text NULL
  - status: int NOT NULL
  - created_at: timestamptz NOT NULL
  - updated_at: timestamptz NOT NULL
```

**PublicationQueueConfiguration:**
```
tabela: publication_queue
colunas:
  - id: uuid PRIMARY KEY
  - product_id: uuid NOT NULL → FK → products.id ON DELETE CASCADE
  - social_network: int NOT NULL
  - status: int NOT NULL
  - scheduled_at: timestamptz NOT NULL
  - published_at: timestamptz NULL
  - retry_count: int NOT NULL DEFAULT 0
  - error_message: text NULL
  - created_at: timestamptz NOT NULL
índices:
  - IX_publication_queue_status_scheduled_at (status, scheduled_at) — composto
FK:
  - product_id → products.id ON DELETE CASCADE
```

**AppSettingConfiguration:**
```
tabela: app_settings
colunas:
  - id: int GENERATED ALWAYS AS IDENTITY PRIMARY KEY
  - key: varchar(200) UNIQUE NOT NULL
  - value: text NOT NULL
  - updated_at: timestamptz NOT NULL
índices:
  - IX_app_settings_key UNIQUE
```

**PushSubscriptionConfiguration:**
```
tabela: push_subscriptions
colunas:
  - id: uuid PRIMARY KEY
  - endpoint: text UNIQUE NOT NULL
  - p256dh: text NOT NULL
  - auth: text NOT NULL
  - created_at: timestamptz NOT NULL
índices:
  - IX_push_subscriptions_endpoint UNIQUE
```

**PublicationLogConfiguration:**
```
tabela: publication_logs
colunas:
  - id: uuid PRIMARY KEY
  - publication_queue_id: uuid NOT NULL FK → publication_queue.id
  - social_network: int NOT NULL
  - attempted_at: timestamptz NOT NULL
  - success: bool NOT NULL
  - error_message: text NULL
```

### 4.3 Migration Inicial

Nome: `20240101000000_InitialSchema`

Gerada com:
```bash
dotnet ef migrations add InitialSchema \
  --project AfiliadoBot.Infrastructure \
  --startup-project AfiliadoBot.Api
```

Aplicada com:
```bash
dotnet ef database update \
  --project AfiliadoBot.Infrastructure \
  --startup-project AfiliadoBot.Api
```

Para recriar schema limpo (dev):
```bash
dotnet ef database drop --force --project AfiliadoBot.Infrastructure --startup-project AfiliadoBot.Api
dotnet ef database update --project AfiliadoBot.Infrastructure --startup-project AfiliadoBot.Api
```

### 4.4 Seeds de `app_settings` (via `HasData`)

Mínimo de 25 registros. Campos de credencial com `Value = ""`. Campos não-sensíveis com valores reais (D2):

| Key | Value (seed) |
|---|---|
| `amazon.access_key` | `""` |
| `amazon.secret_key` | `""` |
| `amazon.partner_tag` | `""` |
| `amazon.marketplace` | `""` |
| `mercadolivre.access_token` | `""` |
| `mercadolivre.refresh_token` | `""` |
| `mercadolivre.client_id` | `""` |
| `mercadolivre.client_secret` | `""` |
| `shopee.partner_id` | `""` |
| `shopee.partner_key` | `""` |
| `shopee.shop_id` | `""` |
| `telegram.bot_token` | `""` |
| `telegram.channel_id` | `""` |
| `youtube.api_key` | `""` |
| `youtube.channel_id` | `""` |
| `instagram.access_token` | `""` |
| `instagram.page_id` | `""` |
| `tiktok.access_token` | `""` |
| `tiktok.open_id` | `""` |
| `claude.api_key` | `""` |
| `claude.model` | `""` |
| `claude.min_score` | `6` |
| `schedule.collector_cron` | `0 6 * * *` |
| `schedule.publisher_cron` | `0 9,12,15,18,20 * * *` |
| `publish.max_per_day` | `10` |
| `networks.telegram.enabled` | `true` |
| `networks.youtube.enabled` | `true` |
| `networks.instagram.enabled` | `true` |
| `networks.tiktok.enabled` | `true` |
| `networks.facebook.enabled` | `true` |

Total: 30 registros (>= 25 exigidos pelo CA-4).

---

## 5. Testes Unitários (`AfiliadoBot.Tests/Domain/`)

Framework: xUnit (já no scaffold do projeto Tests). Sem dependências externas de infra nos testes de domínio.

### 5.1 ProductTests

- `SalePrice_Negativo_LancaExcecao` — `SalePrice = -1` deve lançar `ArgumentException`
- `DiscountPct_AcimaDecem_LancaExcecao` — `DiscountPct = 101` deve lançar `ArgumentException`
- `AffiliateLink_Nulo_LancaExcecao` — `AffiliateLink = null` deve lançar `ArgumentNullException`
- `ProductValido_CriaSemErro` — valores válidos constroem entidade sem exceção

### 5.2 ProductStatusTransitionTests

- `Status_Pending_Para_Rejected_QuandoScoreBaixo` — simular aprovação com score < 6 → `Status = Rejected`
- `Status_Pending_Para_Queued_QuandoAprovado` — simular aprovação com score >= 6 → `Status = Queued`

### 5.3 PublicationStatusTransitionTests

- `Status_Scheduled_Para_Published_QuandoSucesso` — publicação bem-sucedida muda status
- `Status_Scheduled_Para_Failed_QuandoFalha` — falha na publicação muda status
- `RetryCount_MaiorOuIgualATres_NaoRetenta` — `RetryCount >= 3` → não deve enfileirar nova tentativa

### 5.4 ClaudeAiServiceTests (mock)

- Mock do `AnthropicClient` (interface ou wrapper injectável)
- `EvaluateProductAsync_RetornaAproveFalse_QuandoScoreAbaixoDoThreshold`
- `EvaluateProductAsync_DesserializaRespostaJson_Corretamente`

---

## 6. Padrões obrigatórios

| Padrão | Detalhe |
|---|---|
| Nomes de colunas | Sempre configurar explicitamente via `HasColumnName` (camelCase C# → snake_case SQL) |
| Nomes de tabelas | Sempre via `ToTable` (ex.: `ToTable("publication_queue")`) |
| Enums no banco | Armazenados como `int` (padrão EF; não usar string para evitar drift) |
| Timestamps | Sempre `timestamptz` (UTC) via `.HasColumnType("timestamptz")` |
| Namespace Domain | Sem referência a NuGet de infra — compilar `AfiliadoBot.Domain` isolado para validar |
| IDesignTimeDbContextFactory | Implementar em Infrastructure para suporte a `dotnet ef migrations` sem startup |
| Program.cs público | `public partial class Program {}` em Api para testes de integração (já na Issue #1) |

---

## 7. Critérios de aceite mapeados às tasks

| CA | Task | Validação |
|---|---|---|
| CA-1 Tabelas criadas | T-02 | `dotnet ef database update` + query `information_schema.tables` |
| CA-2 Colunas especiais em products | T-02 | Verificar `ai_score`, `ai_reason`, `slug`, `category` |
| CA-3 UNIQUE em push_subscriptions.endpoint | T-02 | Tentar inserir duplicata → constraint violation |
| CA-4 Seeds >= 25 registros | T-02 | `SELECT COUNT(*) FROM app_settings` |
| CA-5 Testes de domínio passam | T-01 | `dotnet test --filter "Domain"` |
| CA-6 Interfaces no Domain | T-01 | `dotnet build AfiliadoBot.Domain` |
| CA-7 AppDbContext na API | T-02 | `GET localhost:5000/health` retorna 200 |
| CA-8 FK e índice em publication_queue | T-02 | Consultar `pg_indexes` e `information_schema.table_constraints` |
| CA-9 Enums distintos | T-01 | Inspecionar `AfiliadoBot.Domain/Enums/` |
