# Especificação Técnica — ISSUE-167: Categorização unificada + remoção de distinção de plataforma

Consolida `proposal.md` (PRD), `criterios-aceite.md` (27 cenários) e `design.md` (decisões do
Arquiteto) em plano técnico executável. Escrita em rota `backlog` — a issue é retomada depois via
rota `normal`; este documento é o ponto de partida do LT quando isso acontecer (não precisa
reabrir `design.md` para reconstituir decisões).

Todos os paths abaixo são relativos à raiz do repo `omuletachou`.

## 1. Migration (`Subcategory` + 5 índices + seeds de `app_settings`)

Convenção do projeto: migrations EF Core (code-first), não SQL solto — ver
`backend/src/AfiliadoBot.Infrastructure/Migrations/*.cs` (últimas: `20260803173650_AddCaptionToPublicationQueue`,
`20260804120430_SeedFacebookCredentials`). Gerar via
`dotnet ef migrations add AddSubcategoryAndCategorizationBudget -p AfiliadoBot.Infrastructure -s AfiliadoBot.Api`
após alterar `Product` (Domain) e `ProductConfiguration`/`AppSettingConfiguration` (Infrastructure).

### 1.1 Entidade `Product` (Domain) — nova propriedade
`backend/src/AfiliadoBot.Domain/Entities/Product.cs`:
- `+ public string? Subcategory { get; private set; }` (nullable, ao lado de `Category`).
- Construtor principal: **não** ganha parâmetro `subcategory` obrigatório (collectors continuam
  passando só `category` na criação — a subcategoria, quando houver, é setada logo em seguida via
  o método novo abaixo, mesma linha de código do collector). Evita quebrar a assinatura em 3 call
  sites simultaneamente com o parâmetro posicional.
- Novo método (substitui o uso de `SetCategory` para o fluxo pós-coleta/fallback IA):
  ```csharp
  /// <summary>
  /// Define categoria/subcategoria a partir do fallback de IA (ProcessorJob, Issue #167).
  /// Só substitui quando a categoria atual ainda for "Geral" — mesma regra defensiva de
  /// SetCategory, mas agora cobre também Subcategory.
  /// </summary>
  public void SetCategoryFromAiFallback(string category, string? subcategory)
  {
      if (string.IsNullOrWhiteSpace(category))
          return;
      if (!string.Equals(Category, "Geral", StringComparison.OrdinalIgnoreCase))
          return;

      Category = category;
      Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory;
      UpdatedAt = DateTime.UtcNow;
  }
  ```
- O `SetCategory(string category)` existente (linha 172-182) fica obsoleto para o fluxo de
  categorização — ver seção 5 (`ProcessorJob`). Decisão do LT/Dev na retomada: remover ou manter
  como método morto/depreciado (nenhum critério de aceite exige removê-lo; se nada mais chamar,
  preferir remover para não deixar dois caminhos de "definir categoria").
- Collectors (seção 4) setam `Category`/`Subcategory` **direto no construtor**, não via este
  método (o método é só para o fallback pós-coleta, que respeita a regra "só sobrescreve Geral").
  Para isso, o construtor de `Product` precisa aceitar `subcategory` como parâmetro opcional:
  `string? subcategory = null` — adicionar ao final da lista de parâmetros opcionais (depois de
  `sourceUrl`) para não quebrar a ordem posicional usada pelos 3 collectors hoje.

### 1.2 `ProductConfiguration` (Infrastructure)
`backend/src/AfiliadoBot.Infrastructure/Data/Configurations/ProductConfiguration.cs`:
```csharp
builder.Property(x => x.Subcategory)
    .HasColumnName("subcategory")
    .HasMaxLength(100); // nullable — sem .IsRequired()

builder.HasIndex(x => x.Status) // se ainda não existir isolado — checar antes de duplicar
    ...
```
Adicionar os 5 índices definidos pelo Arquiteto (design.md §4.2), na mesma classe, após o índice
existente `IX_products_platform_external_id`:
```csharp
builder.HasIndex(x => new { x.Status, x.AiScore })
    .HasDatabaseName("IX_products_status_aiscore");

builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.AiScore })
    .HasDatabaseName("IX_products_status_category_subcategory_aiscore");

builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.SalePrice })
    .HasDatabaseName("IX_products_status_category_subcategory_saleprice");

builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.DiscountPct })
    .HasDatabaseName("IX_products_status_category_subcategory_discountpct");

builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.CreatedAt })
    .HasDatabaseName("IX_products_status_category_subcategory_createdat");
```
EF Core não expressa `DESC`/`ASC` por coluna do índice via `HasIndex` fluente simples — para as
colunas que design.md pede desc (`ai_score DESC`, `discount_pct DESC`, `created_at DESC`), usar
`.IsDescending(false, false, false, true)` (EF Core 8+, um bool por coluna do índice, na ordem
declarada) ou, se a versão do EF Core do projeto não suportar `IsDescending` no fluente (checar
`Microsoft.EntityFrameworkCore 8.0.x` instalado), cair para `migrationBuilder.Sql(...)` direto na
migration gerada com o DDL do design.md §4.2 — qualquer uma das duas é aceitável, mas escolher UMA
consistentemente para as 4 (não misturar 2 via fluente + 2 via SQL cru).

### 1.3 Seeds novos em `app_settings`
`backend/src/AfiliadoBot.Infrastructure/Data/Configurations/AppSettingConfiguration.cs`: o padrão
atual usa `HasData` com `Id` incremental manual (últimos: 39, 40). **Antes de implementar, conferir
o maior `Id` vigente no momento** (pode ter subido entre esta especificação e a retomada da issue,
por outras issues no meio) — os `Id`s abaixo (41-45) são a proposta assumindo que a numeração atual
(até 40) não mudou:
```csharp
new { Id = 41, Key = "claude.monthly_budget_limit_brl", Value = "30", UpdatedAt = now },
new { Id = 42, Key = "claude.monthly_usage", Value = "{\"month\":\"\",\"spend_brl\":0}", UpdatedAt = now },
new { Id = 43, Key = "claude.price_input_usd_per_mtok", Value = "<confirmar com Gerente/DevOps>", UpdatedAt = now },
new { Id = 44, Key = "claude.price_output_usd_per_mtok", Value = "<confirmar com Gerente/DevOps>", UpdatedAt = now },
new { Id = 45, Key = "claude.usd_brl_rate", Value = "<confirmar com Gerente/DevOps>", UpdatedAt = now },
```
Preço/câmbio: modelo em uso é `claude-haiku-4-5-20251001` (ver `repos/omuletachou/CLAUDE.md`); o
Dev/LT que retomar deve confirmar os valores vigentes da tabela de preços Anthropic para esse
modelo antes do deploy (risco já registrado por design.md §8 — "soft guard", não bloqueante).

### 1.4 Critérios de aceite cobertos
CA 1.1, 1.2 (migration aditiva, sem enum/constraint), CA 4.1 (default R$30).

## 2. Mover `CategoryDetector` de `Application` para `Domain`

Resolve a dependência circular achada pelo Arquiteto: `Infrastructure` (onde moram os 3 collectors)
não referencia `Application` — só `Domain` (confirmado em
`backend/src/AfiliadoBot.Infrastructure/AfiliadoBot.Infrastructure.csproj`, único `ProjectReference`
é `AfiliadoBot.Domain`). `CategoryDetector` hoje é `AfiliadoBot.Application.CategoryDetector`
(`backend/src/AfiliadoBot.Application/CategoryDetector.cs`) — precisa virar
`AfiliadoBot.Domain.Services.CategoryDetector` (ou namespace equivalente dentro de Domain; sugestão
`AfiliadoBot.Domain.Services`, já que é lógica de negócio pura sem I/O — criar a pasta `Services/`
em Domain se não existir).

**Todos os arquivos que referenciam `CategoryDetector` hoje** (Grep confirmado, nenhum outro no
repo):
| Arquivo | Referência atual | Ação |
|---|---|---|
| `backend/src/AfiliadoBot.Application/CategoryDetector.cs` | é a própria classe | mover arquivo inteiro para `backend/src/AfiliadoBot.Domain/Services/CategoryDetector.cs`; trocar `namespace AfiliadoBot.Application;` → `namespace AfiliadoBot.Domain.Services;` |
| `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs:150` | `AfiliadoBot.Application.CategoryDetector.Detect(product.Title)` (chamada totalmente qualificada, sem `using`) | **Remover a chamada inteira daqui** — a camada de dicionário sai do `ProcessorJob` e vai para os collectors (Gate 1, regra 4). Ver seção 5 — `EnsureCategory` deixa de existir; o que resta no `ProcessorJob` é só o fallback IA |
| `backend/src/AfiliadoBot.Tests/CategoryDetectorTests.cs` | `using` implícito (mesmo assembly de testes, sem `using AfiliadoBot.Application` explícito — checar) + chamadas `CategoryDetector.Detect(...)` | adicionar `using AfiliadoBot.Domain.Services;` no topo do arquivo; testes continuam válidos (mesma API), só ajustar `using` |
| `backend/src/AfiliadoBot.Domain/Entities/Product.cs:168` | comentário XML mencionando "CategoryDetector" (não é referência de código, é doc) | atualizar o texto do comentário para refletir a nova localização/fluxo (comentário informativo, não bloqueante) |

Novos consumidores da classe movida (collectors, seção 4) ficam em `Infrastructure` e adicionam
`using AfiliadoBot.Domain.Services;` — já é uma referência válida (Infrastructure → Domain já
existe).

## 3. Dicionário expandido — estrutura de dados

`CategoryDetector` (Domain) expande de 5 categorias/sem subcategoria para 9 categorias com
subcategorias. Estrutura de dados proposta (o Dev preenche as ~35 subcategorias e suas keywords —
não são listadas aqui, é curadoria de dado, fora do escopo desta especificação técnica):

```csharp
namespace AfiliadoBot.Domain.Services;

public static class CategoryDetector
{
    private const string FallbackCategory = "Geral";

    // Dicionário de 2 níveis: Categoria -> Subcategoria -> lista de keywords.
    // A ordem de iteração dos Dictionary/List é a ordem de declaração (C# preserva
    // ordem de inserção em Dictionary<TKey,TValue> na prática, mas não é contrato
    // documentado da linguagem — se a ordem de match importar para desempate,
    // considerar List<(string Categoria, string Subcategoria, string[] Keywords)>
    // em vez de Dictionary aninhado, mais explícito sobre prioridade).
    private static readonly Dictionary<string, Dictionary<string, List<string>>> Taxonomia = new()
    {
        ["Eletrodomésticos"] = new()
        {
            ["<subcategoria 1>"] = new() { "<keyword>", "..." },
            // 3-5 subcategorias
        },
        ["Climatização"] = new() { /* ... */ },
        ["Ferramentas"] = new() { /* ... */ },
        ["Eletrônicos"] = new() { /* ... */ }, // já existe parcialmente hoje, expandir p/ subcategorias
        ["Casa e Cozinha"] = new() { /* ... */ }, // idem
        ["Beleza"] = new() { /* ... */ }, // idem
        ["Moda"] = new() { /* ... */ }, // idem
        ["Brinquedos"] = new() { /* ... */ }, // idem
        // "Geral" não entra no dicionário — é o fallback quando nenhuma keyword casa.
    };

    public static (string Category, string? Subcategory) Detect(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (FallbackCategory, null);

        foreach (var (categoria, subcategorias) in Taxonomia)
        {
            foreach (var (subcategoria, keywords) in subcategorias)
            {
                foreach (var keyword in keywords)
                {
                    if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return (categoria, subcategoria);
                }
            }
        }

        return (FallbackCategory, null);
    }
}
```

**Mudança de assinatura**: `Detect` passa de `string Detect(string title)` para
`(string Category, string? Subcategory) Detect(string title)` (design.md §2, linha da tabela
"Domain | CategoryDetector"). Isso quebra a assinatura de `CategoryDetectorTests.cs` — os 5 testes
existentes (`backend/src/AfiliadoBot.Tests/CategoryDetectorTests.cs`) precisam de ajuste de
asserção (`var categoria = CategoryDetector.Detect(...)` → `var (categoria, _) = CategoryDetector.Detect(...)`
ou equivalente), além dos novos testes por categoria/subcategoria exigidos por CA 2.3.

**Categorias exatas (nomes-chave do Dictionary, literais, sem variação)**: Eletrodomésticos,
Climatização, Ferramentas, Eletrônicos, Casa e Cozinha, Beleza, Moda, Brinquedos, Geral (fallback).
Usar exatamente esses literais (com acentuação) — são o contrato consumido por
`GET /api/public/categories` (seção 8) e pelo dropdown do frontend (seção 9); divergência de
acentuação/case entre o dicionário e qualquer lugar que compare string quebra o filtro.

### CA cobertos
CA 2.1, 2.2, 2.3.

## 4. Integração na coleta — os 3 collectors

`backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/{Amazon,MercadoLivre,Shopee}Collector.cs`
— cada um tem hoje `private const string DefaultCategory = "Geral";` e usa `category: DefaultCategory`
na construção do `Product` (linhas: Amazon 297-308, MercadoLivre ~359-370, Shopee ~308-320 — ver
offsets exatos por arquivo, todos seguem o mesmo padrão estrutural).

Mudança idêntica nos 3 arquivos:
1. Adicionar `using AfiliadoBot.Domain.Services;` no topo.
2. Remover (ou manter só como fallback documentado) `private const string DefaultCategory = "Geral";`
   — não é mais necessário chamar explicitamente, `CategoryDetector.Detect` já retorna `"Geral"`
   como fallback.
3. Antes de `var product = new Product(...)`, chamar:
   ```csharp
   var (category, subcategory) = CategoryDetector.Detect(item.Title);
   ```
4. No construtor de `Product`, trocar `category: DefaultCategory,` por `category: category,` e
   adicionar `subcategory: subcategory,` (novo parâmetro opcional, seção 1.1).

Repetir para `MercadoLivreCollector.cs` e `ShopeeCollector.cs` — mesmo padrão estrutural
(`private const string DefaultCategory = "Geral";` + `category: DefaultCategory,` no `new Product(...)`),
confirmado por Grep nos 3 arquivos.

**Atenção ao `UpdateFromCollector`** (upsert de produto já existente, chamado quando
`existing is not null` antes de qualquer criação de `Product`): esse método (Product.cs linha 141)
não toca `Category`/`Subcategory` hoje e **não deve passar a tocar** — Gate 1 regra 3 (sem
recategorização retroativa); produtos que já existem mantêm a categoria que tinham, mesmo se o
dicionário mudou desde então.

### CA cobertos
CA 2.1 (comportamento idêntico nos 3 collectors), parte de CA 2.2.

## 5. Fallback IA no `ProcessorJob`

`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`. Estado atual do método `ExecuteAsync`
(linhas 70-105), ordem das chamadas dentro do loop:
```csharp
DownloadMediaAsync(product, ct);
EnsureSlug(product);        // linha 79
EnsureCategory(product);    // linha 81 — dicionário, síncrono, sem I/O externo
EnsureAffiliateLinkAsync(...)
CreatePublicationQueueEntriesAsync(...)
```

Mudança:
1. **Remover `EnsureCategory` (método privado, linhas 148-152) inteiro** — a camada de dicionário
   não roda mais aqui, foi para os collectors (seção 4). A chamada
   `AfiliadoBot.Application.CategoryDetector.Detect(product.Title)` (linha 150) deixa de existir
   neste arquivo — é a referência que a migração do CategoryDetector (seção 2) precisa eliminar
   daqui, não portar.
2. **Adicionar `EnsureCategoryFallbackAsync` (novo, assíncrono — chama IA)**:
   ```csharp
   private async Task EnsureCategoryFallbackAsync(Product product, CancellationToken ct)
   {
       if (!string.Equals(product.Category, "Geral", StringComparison.OrdinalIgnoreCase))
           return; // CA 3.3 — dicionário já classificou, não chama IA

       var classification = await _aiService.ClassifyCategoryAsync(product, ct);
       if (classification is not null)
           product.SetCategoryFromAiFallback(classification.Category, classification.Subcategory);
       // classification null (orçamento estourado OU erro/timeout da chamada) — produto
       // permanece "Geral", sem exceção, sem bloquear o resto do loop (CA 4.3, mesma postura
       // de erro já usada para GenerateCaptionAsync/ScoreProductAsync — design.md §3.6).
   }
   ```
   O filtro `Status == Queued` (CA 3.2) já é garantido pela query do topo de `ExecuteAsync`
   (`.Where(p => p.Status == ProductStatus.Queued)`, linha 60) — todo produto que chega neste ponto
   do loop já está `Queued`; não precisa checagem adicional de status dentro do método (confirma
   design.md §3.6, último parágrafo).
3. **Reordenar a sequência no loop** — `design.md`/CA 3.1 exigem que o fallback rode **antes** da
   geração de slug/legenda. Slug é gerado por `EnsureSlug` (síncrono, sem I/O); legenda é gerada
   dentro de `CreatePublicationQueueEntriesAsync` → `_aiService.GenerateCaptionAsync` (linha 274).
   Nova ordem:
   ```csharp
   DownloadMediaAsync(product, ct);
   await EnsureCategoryFallbackAsync(product, ct);   // NOVO — antes do slug
   EnsureSlug(product);                              // estava antes, agora depois
   var linkOk = await EnsureAffiliateLinkAsync(product, ct);
   ...
   CreatePublicationQueueEntriesAsync(...)            // gera legenda, continua depois da categoria
   ```
   Ou seja: inverter as linhas 79/81 atuais (`EnsureSlug` / `EnsureCategory`) — a nova
   `EnsureCategoryFallbackAsync` entra na posição que hoje é de `EnsureCategory`, mas o `EnsureSlug`
   desce para depois dela (hoje é `Slug` antes de `Category`; a partir daqui é `Category` antes de
   `Slug`).

### CA cobertos
CA 3.1, 3.2, 3.3, 3.4 (não mexe em `ScoreProductAsync`, que já é chamado só nos collectors —
`AmazonCollector.cs:312` e equivalentes — nunca no `ProcessorJob`; confirmado, escopo intocado).

## 6. Contador de orçamento (`IClaudeBudgetService`)

Novo serviço em Infrastructure (design.md §3.6 já define o contrato — reproduzido aqui com o
detalhe de implementação do `UPDATE` atômico):

`backend/src/AfiliadoBot.Infrastructure/Services/IClaudeBudgetService.cs` +
`ClaudeBudgetService.cs`:
```csharp
public interface IClaudeBudgetService
{
    Task<bool> IsCategorizationBudgetAvailableAsync(CancellationToken ct = default);
    Task RecordUsageAsync(int inputTokens, int outputTokens, CancellationToken ct = default);
}
```
- `IsCategorizationBudgetAvailableAsync`: `SELECT` simples em `app_settings` — lê
  `claude.monthly_usage` (parse do JSON), compara `month` com `yyyy-MM` (UTC) atual; se `month`
  diferente, gasto tratado como zero. Lê `claude.monthly_budget_limit_brl` (numérico). Disponível
  se `spend_brl_efetivo < limite`.
- `RecordUsageAsync`: calcula `custoBRL` (fórmula design.md §3.3, usando
  `claude.price_input_usd_per_mtok` / `claude.price_output_usd_per_mtok` / `claude.usd_brl_rate` de
  `app_settings`) e executa o `UPDATE` atômico via `_dbContext.Database.ExecuteSqlInterpolatedAsync`
  com o `CASE` de design.md §3.5 (reproduzido):
  ```sql
  UPDATE app_settings
  SET value = CASE
          WHEN (value::jsonb->>'month') = {mesAtual}
              THEN jsonb_set(value::jsonb, '{spend_brl}',
                   to_jsonb(((value::jsonb->>'spend_brl')::numeric + {deltaBrl})))::text
          ELSE jsonb_build_object('month', {mesAtual}, 'spend_brl', {deltaBrl})::text
      END,
      updated_at = now()
  WHERE key = 'claude.monthly_usage';
  ```
  Executado **fora do change tracker do EF** (`ExecuteSqlInterpolatedAsync`, não
  `SaveChangesAsync` sobre uma entidade `AppSetting` já tracked) — evita lost-update sob concorrência
  (design.md §3.5). Só chamado após sucesso da chamada Claude (CA 4.2 — "executada com sucesso").

Registro em `Program.cs`: `services.AddScoped<IClaudeBudgetService, ClaudeBudgetService>()`
(mesmo padrão de DI dos demais serviços de Infrastructure — checar bloco de `AddScoped` existente
para `IAiService`/`ClaudeAiService`).

### CA cobertos
CA 4.2, 4.3, 4.4 (só o fallback de categorização usa o contador — `ScoreProductAsync`/
`GenerateCaptionAsync` seguem sem qualquer dependência de `IClaudeBudgetService`), CA 4.5 (reset
lazy embutido no `UPDATE`/leitura, sem job novo).

## 7. `IAnthropicClientWrapper` / `ClaudeAiService` — contrato de tokens

`backend/src/AfiliadoBot.Infrastructure/Services/IAnthropicClientWrapper.cs` +
`AnthropicClientWrapper.cs`:
- `CompleteAsync` muda de `Task<string>` para `Task<ClaudeCompletionResult>`, onde
  `record ClaudeCompletionResult(string Text, int InputTokens, int OutputTokens)` (novo, em
  Infrastructure — usa `response.Usage.InputTokens`/`OutputTokens` do `Anthropic.SDK`, já instalado,
  sem dependência nova).
- `ClaudeAiService.ScoreProductAsync`/`GenerateCaptionAsync`: troca mecânica de `response` (string)
  por `response.Text` — **não ganham** nenhuma lógica de orçamento (CA 3.4).
- `ClaudeAiService.ClassifyCategoryAsync` (novo, implementa
  `IAiService.ClassifyCategoryAsync(Product, ct)` de `backend/src/AfiliadoBot.Domain/Interfaces/IAiService.cs`):
  1. `if (!await _budgetService.IsCategorizationBudgetAvailableAsync(ct)) return null;` (CA 4.3)
  2. Monta prompt (título/descrição do produto + lista fechada de categorias/subcategorias v1 —
     mesmo texto da seção 3, para a IA responder dentro da taxonomia conhecida; formato de
     resposta esperado JSON `{category, subcategory}` — parsing e tratamento de resposta fora do
     formato ficam a critério do Dev, seguindo o padrão já usado em `ScoreProductAsync` para
     parsear JSON da resposta do Claude).
  3. Chama `_client.CompleteAsync(...)`; `try/catch` — exceção retorna `null` sem debitar orçamento
     (design.md §3.6).
  4. Sucesso: `await _budgetService.RecordUsageAsync(result.InputTokens, result.OutputTokens, ct);`
     e retorna `new CategoryClassification(category, subcategory)` parseado da resposta.
- `IAiService` (Domain) ganha `Task<CategoryClassification?> ClassifyCategoryAsync(Product product, CancellationToken ct = default);`
  e o novo `record CategoryClassification(string Category, string? Subcategory);` (Domain — mesmo
  assembly de `Product`, sem dependência de Infrastructure).

### CA cobertos
CA 4.2 (contabilização só de chamadas bem-sucedidas), suporte a CA 3.1/3.4.

## 8. `PublicDealDto` e `PublicController`

`backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs`:
- Remover `public string Platform { get; init; }` (linha 25) e a atribuição
  `Platform = product.Platform.ToString(),` em `FromProduct` (linha 53).
- Adicionar `public string? Subcategory { get; init; }` e `Subcategory = product.Subcategory,` em
  `FromProduct`.
- DTO interno/dashboard (`backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`, usado por
  `ProductsController.cs`) **não é tocado** — continua expondo `Platform` normalmente (CA 5.2, 5.3).

`backend/src/AfiliadoBot.Api/Controllers/PublicController.cs`:
- **`GetDeals` (linha 31-40)**: expandir assinatura com os novos `[FromQuery]`:
  ```csharp
  [HttpGet]
  public async Task<ActionResult<PagedResult<PublicDealDto>>> GetDeals(
      [FromQuery] int? page, [FromQuery] int? pageSize,
      [FromQuery] string? category, [FromQuery] string? subcategory,
      [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice,
      [FromQuery] decimal? minDiscount, [FromQuery] string? sort,
      CancellationToken ct)
  {
      var query = _db.Products.Where(p => p.Status == ProductStatus.Published);

      if (!string.IsNullOrWhiteSpace(category))
          query = query.Where(p => p.Category == category);
      if (!string.IsNullOrWhiteSpace(subcategory))
          query = query.Where(p => p.Subcategory == subcategory);
      if (minPrice.HasValue)
          query = query.Where(p => p.SalePrice >= minPrice.Value);
      if (maxPrice.HasValue)
          query = query.Where(p => p.SalePrice <= maxPrice.Value);
      if (minDiscount.HasValue)
          query = query.Where(p => p.DiscountPct >= minDiscount.Value);

      var ordered = sort switch
      {
          "price_asc" => query.OrderBy(p => p.SalePrice),
          "discount_desc" => query.OrderByDescending(p => p.DiscountPct),
          "recent" => query.OrderByDescending(p => p.CreatedAt),
          _ => query.OrderByDescending(p => p.AiScore), // default — CA 6.5
      };

      return Ok(await ToDtoPagedResultAsync(ordered, page, pageSize, ct));
  }
  ```
  Nomes de `sort` (`price_asc`/`discount_desc`/`recent`) são sugestão — design.md §9 deixa explícito
  que a nomenclatura fina é refinamento do LT na retomada; qualquer valor não reconhecido cai no
  `default` (ordenação por `AiScore`, nunca erro — consistente com CA 6.6 aplicado também a `sort`
  inválido, embora CA 6.6 fale de `category` inexistente).
- **Remover `GetByCategory` (`[HttpGet("category/{categoria}")]`, linhas 42-51)** — decisão do
  Arquiteto (design.md §5.2). **Ordem de deploy obrigatória** (não é opcional, evita quebrar
  produção): (1) subir `GetDeals` com os filtros novos, (2) migrar `website/lib/api.ts` (seção 9),
  (3) só então remover `GetByCategory` — idealmente as 3 mudanças no mesmo PR/deploy, não em PRs
  separados no tempo.
- **Novo endpoint `GET /api/public/categories`**:
  ```csharp
  [HttpGet("~/api/public/categories")] // fora do [Route("api/public/deals")] da classe — usar
                                        // rota absoluta com "~/", ou mover para um controller
                                        // PublicCategoriesController dedicado (preferível para
                                        // não misturar rota base — decisão do Dev/LT)
  public async Task<ActionResult<List<CategoryTreeDto>>> GetCategories(CancellationToken ct)
  {
      var tree = await _db.Products
          .Where(p => p.Status == ProductStatus.Published)
          .GroupBy(p => new { p.Category, p.Subcategory })
          .Select(g => new { g.Key.Category, g.Key.Subcategory, Count = g.Count() })
          .ToListAsync(ct);

      var result = tree
          .GroupBy(x => x.Category)
          .Select(g => new CategoryTreeDto
          {
              Category = g.Key,
              Subcategories = g.Where(x => x.Subcategory != null)
                  .Select(x => new SubcategoryCountDto { Subcategory = x.Subcategory!, Count = x.Count })
                  .ToList(),
              Count = g.Sum(x => x.Count),
          })
          .ToList();

      return Ok(result);
  }
  ```
  `CategoryTreeDto`/`SubcategoryCountDto` — novos DTOs em `AfiliadoBot.Api/Public/`, mesma pasta de
  `PublicDealDto`. Formato exato (nomes de campo JSON) fica a critério do Dev, desde que satisfaça
  CA 6.7 (árvore `Category > [Subcategory]`, só com produtos ativos, com contagem).

### CA cobertos
CA 5.1, 5.2, 5.3, 6.1-6.7.

## 9. `website` — migração de `fetchByCategory` + `FilterBar` + remoção de chips

### 9.1 `website/lib/api.ts`
- `fetchByCategory` (linhas 49-63): trocar a URL de
  `` `${API_BASE_URL}/api/public/deals/category/${encodeURIComponent(categoria)}?${params}` ``
  para reusar `fetchDeals` com o parâmetro `category` (a função `fetchDeals`, linhas 21-36, já
  aceita `category?: string` e monta `/api/public/deals?category=...`). Duas opções equivalentes:
  (a) `fetchByCategory` vira um alias fino que chama `fetchDeals(page, pageSize, categoria)`; ou
  (b) remover `fetchByCategory` e trocar a chamada em
  `website/app/categoria/[categoria]/page.tsx` para `fetchDeals` direto. Preferir (b) — menos
  código duplicado — mas checar todos os call sites de `fetchByCategory` antes (Grep confirmou 1:
  `app/categoria/[categoria]/page.tsx`, mais os testes `app/categoria/[categoria]/page.test.tsx` e
  `lib/api.test.ts`, que precisam de ajuste correspondente).
- `fetchDeals` ganha os novos parâmetros de filtro (`subcategory`, `minPrice`, `maxPrice`,
  `minDiscount`, `sort`), espelhando a assinatura da seção 8 — assinatura sugerida:
  ```ts
  export async function fetchDeals(
    page = 1,
    pageSize = 12,
    filters?: {
      category?: string;
      subcategory?: string;
      minPrice?: number;
      maxPrice?: number;
      minDiscount?: number;
      sort?: string;
    }
  ): Promise<PagedResult<Deal>>
  ```
  (muda de parâmetro posicional `category?: string` para um objeto `filters` — call sites
  existentes de `fetchDeals(page, pageSize, category)` precisam migrar para
  `fetchDeals(page, pageSize, { category })`; checar `app/page.tsx` e os `.test.tsx`
  correspondentes).
- Novo `fetchCategories(): Promise<CategoryTree[]>` chamando `GET /api/public/categories`.

### 9.2 `website/lib/types.ts`
- `Deal.platform` (linha 12) — **remover** (CA 5.1, o backend não envia mais).
- `Deal.category`/nova `Deal.subcategory?: string | null` — adicionar.
- Novo tipo `CategoryTree { category: string; subcategories: { subcategory: string; count: number }[]; count: number }`
  (nomes de campo alinhados ao DTO real que o Dev definir na seção 8 — ajustar camelCase conforme
  serialização JSON padrão do ASP.NET, que já é camelCase por config default).

### 9.3 `website/components/Header.tsx`
Achado do Arquiteto (design.md §6, tabela): **é aqui, não em badge de card**, que a distinção de
plataforma aparece hoje (`PLATFORMS`, `activePlatform` — confirmado por Grep em `Header.tsx`).
Remover os chips de plataforma inteiros do componente — `Header` passa a ser só marca/logo,
consistente com o "achado técnico da Fase 1" citado em CA 7.4 (nenhum badge de plataforma em
`DealCard`/`DealDetail`, já confirmado). Ajustar `Header.test.tsx` de acordo (Grep mostrou que o
teste hoje cobre os chips — remover as asserções correspondentes).

### 9.4 Novo componente `FilterBar`
Novo, em `website/components/FilterBar/` (ou `website/components/FilterBar.tsx`, seguindo o padrão
de organização de componente existente no projeto — checar se `Header`/`DealCard` usam pasta
própria ou arquivo solto antes de decidir). Renderiza **só em `app/page.tsx` (Home)** — não em
`app/categoria/[categoria]/page.tsx` nem `app/oferta/[slug]/page.tsx` (design.md §6, tabela de
componentes globais — escopo explícito de CA 7.1-7.5).
- Dropdown de Categoria (populado via `fetchCategories`) + dropdown dependente de Subcategoria
  (filtra pela categoria escolhida; desabilitado/vazio se a categoria não tiver subcategorias —
  caso "Geral", CA 7.1).
- Slider de faixa de preço (`minPrice`/`maxPrice`).
- Botões de desconto mínimo: 10%+/30%+/50%+ (`minDiscount`).
- Seletor de ordenação: relevância (padrão)/menor preço/maior desconto/mais recente (`sort`).
- Estado dos filtros via `useSearchParams`/`router.push` (Next.js App Router — padrão idiomático
  para filtros combináveis refletidos na URL, permite compartilhar link filtrado; decisão de
  implementação do Dev, não fixada aqui) — `app/page.tsx` lê `searchParams` e repassa para
  `fetchDeals` (design.md §2, linha `app/page.tsx`).
- Estado vazio (CA 7.5): mensagem "nenhuma oferta encontrada" quando `PagedResult.items` vier vazio
  com filtros aplicados — componente já deve existir ou ser trivial de adicionar em `app/page.tsx`.

### CA cobertos
CA 7.1, 7.2, 7.3, 7.4, 7.5.

## Sugestão de task breakdown (registrada, sem criar sub-issues agora)

Para quando o LT retomar via rota `normal` — 4 sub-issues sugeridas, cada uma compila/testa
isoladamente:

1. **`backend-schema-collectors`** (dotnet): seções 1 (migration completa) + 2 (mover
   `CategoryDetector`) + 3 (dicionário expandido) + 4 (integração nos 3 collectors). Entrega:
   produtos novos nascem com `Category`/`Subcategory` corretos, sem custo de IA, testes de
   `CategoryDetector` cobrindo as 9 categorias (CA 2.1-2.3). Base para as outras 3 (todas dependem
   do schema/`CategoryDetector` já movido).
2. **`backend-ia-orcamento`** (dotnet, depende de #1 para o schema): seções 5 (`ProcessorJob`
   fallback + reordenação) + 6 (`IClaudeBudgetService`) + 7 (`IAnthropicClientWrapper`/
   `ClaudeAiService`). Entrega: fallback IA condicionado, orçamento mensal funcionando (CA 3.1-3.4,
   4.1-4.5).
3. **`backend-api-filtros`** (dotnet, depende de #1 para o schema/índices): seção 8
   (`PublicDealDto`, `GetDeals` com filtros, remoção de `GetByCategory`, `GET /api/public/categories`).
   Entrega: CA 5.1-5.3, 6.1-6.7. **Não fazer deploy desta sub-issue isolada em produção antes da
   #4 estar pronta** (design.md §5.2 — quebra `/categoria/[categoria]` se a rota antiga sumir antes
   do frontend migrar); tecnicamente pode ser codada/testada em paralelo, mas o merge
   `desenv→homolog`/deploy final depende da #4 estar no mesmo lote.
4. **`frontend-filtros`** (nodejs/Next.js, depende de #3 para o contrato de API — pode começar
   com mocks e integrar depois): seção 9 completa (`api.ts`, `types.ts`, `Header.tsx` sem chips,
   novo `FilterBar`, `app/page.tsx`). Entrega: CA 7.1-7.5. **UX/UI deveria rodar antes desta
   sub-issue** (ver avaliação abaixo) — mockups do `FilterBar` evitam retrabalho de layout.

Ordem de merge sugerida (não estritamente sequencial — #1 primeiro, #2/#3 podem rodar em paralelo
depois, #4 por último ou em paralelo com mock): **#1 → (#2 ‖ #3) → #4**, com o release
`homolog→main` esperando as 4 prontas juntas (design.md §5.2 exige #3+#4 no mesmo deploy).

## Avaliação de necessidade de UX/UI

**Sim, recomendado.** O `FilterBar` (seção 9.4) é UI nova real — dropdowns dependentes, slider de
faixa de preço, grupo de botões de desconto mínimo, seletor de ordenação — não é ajuste de CSS em
componente existente. Envolve decisões de layout/interação (onde a barra fica na Home, comportamento
responsivo do slider em mobile, estado visual de filtro ativo) que se beneficiam de mockup antes do
Dev implementar, evitando retrabalho. Registrado como próximo passo recomendado em
`{docs_path}/estado.md` — não spawnado nesta invocação (rota `backlog` termina aqui).

## Fora de escopo desta especificação (mantido do design.md §9)

- Conteúdo exato do dicionário (as ~35 subcategorias/keywords) — curadoria de dado, Dev preenche na
  sub-issue `backend-schema-collectors`.
- Layout visual final do `FilterBar` — UX/UI, quando spawnado.
- Nomenclatura fina dos valores de `sort` — sugestão dada na seção 8, mas não é contrato fechado.
