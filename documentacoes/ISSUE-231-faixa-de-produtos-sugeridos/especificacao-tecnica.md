# Especificação Técnica — ISSUE-231: Rastreio de cliques + faixa de produtos sugeridos (site público)

Refinamento do `openspec/changes/issue-231-faixa-de-produtos-sugeridos/design.md` (Arquiteto), com
nomes de arquivo/classe confirmados contra o código real do repo (`backend/src/`, `website/`).
Decisões do Arquiteto não são repetidas aqui — só os pontos de implementação concreta.

## 1. Investigação `discount_pct` — já concluída, sem ação nesta issue
Ver `design.md` seção 9. Resultado: Amazon e Shopee calculam `discountPct` real a partir de suas
APIs; só Mercado Livre é hardcoded em `0` (limitação já tratada na Issue #182/#192). **Coluna
mantida, nenhuma sub-issue desta issue mexe em `discount_pct`.**

## 2. Backend — schema

### 2.1 `Product` (Domain) — `backend/src/AfiliadoBot.Domain/Entities/Product.cs`
- `+ public int ClickCount { get; private set; }` (default 0 — inicializado no construtor).
- `+ public void RegisterClick()` — método de domínio: `ClickCount++; UpdatedAt = DateTime.UtcNow;`
  (segue o padrão dos demais métodos da entidade — nunca setter público, sempre método de
  intenção). **Não** atualizar `UpdatedAt` via `SaveChanges` direto em SQL cru — usar o padrão
  EF Core já usado no resto do projeto (carregar a entidade, chamar o método, `SaveChangesAsync`).
  A atomicidade do incremento (Decisão 1 do design.md) é garantida pelo Postgres mesmo assim,
  porque o campo é lido e gravado dentro da mesma transação implícita do `SaveChangesAsync` — não
  precisa de SQL bruto (`ExecuteSqlRaw`) para este volume.

### 2.2 `ProductClick` (nova entidade) — `backend/src/AfiliadoBot.Domain/Entities/ProductClick.cs`
```csharp
public class ProductClick
{
    public long Id { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTime ClickedAt { get; private set; }

    private ProductClick() { } // EF Core

    public ProductClick(Guid productId)
    {
        ProductId = productId;
        ClickedAt = DateTime.UtcNow;
    }
}
```
Sem navegação para `Product` (não é necessária para os casos de uso desta issue — evita FK object
navigation desnecessária, segue o princípio de menor superfície já usado em outras entidades do
projeto, ex. `JobRun`).

### 2.3 `ProductConfiguration.cs` — `backend/src/AfiliadoBot.Infrastructure/Data/Configurations/ProductConfiguration.cs`
Adicionar dentro de `Configure(...)`:
```csharp
builder.Property(x => x.ClickCount)
    .HasColumnName("click_count")
    .HasDefaultValue(0)
    .IsRequired();

builder.HasIndex(x => new { x.Status, x.Category, x.ClickCount, x.CreatedAt })
    .HasDatabaseName("IX_products_status_category_clickcount")
    .IsDescending(false, false, true, true);

builder.HasIndex(x => new { x.Status, x.ClickCount, x.CreatedAt })
    .HasDatabaseName("IX_products_status_clickcount")
    .IsDescending(false, true, true);
```
Segue exatamente o padrão dos índices compostos já existentes no mesmo arquivo (`status` líder,
coluna de ordenação por último, `IsDescending` explícito nas colunas DESC).

### 2.4 `ProductClickConfiguration.cs` (nova) — `backend/src/AfiliadoBot.Infrastructure/Data/Configurations/ProductClickConfiguration.cs`
```csharp
public class ProductClickConfiguration : IEntityTypeConfiguration<ProductClick>
{
    public void Configure(EntityTypeBuilder<ProductClick> builder)
    {
        builder.ToTable("product_clicks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.ClickedAt).HasColumnName("clicked_at")
            .HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => x.ProductId).HasDatabaseName("IX_product_clicks_product_id");

        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```
Descoberta automaticamente por `ApplyConfigurationsFromAssembly` (já usado em
`AfiliadoBotDbContext.OnModelCreating`) — não precisa registrar manualmente.

### 2.5 `AfiliadoBotDbContext.cs`
`+ public DbSet<ProductClick> ProductClicks { get; set; } = null!;` (mesmo padrão dos DbSets
existentes).

### 2.6 Migration
Nome sugerido: `AddProductClicksAndClickCount` (mesma convenção de nome dos arquivos existentes em
`backend/src/AfiliadoBot.Infrastructure/Migrations/`, ex. `AddProductSearchVector`,
`AddStatusPlatformCreatedAtIndex`). Gerar via `dotnet ef migrations add` (não escrever a mão) para
o EF já produzir o `.Designer.cs` e atualizar o `AfiliadoBotDbContextModelSnapshot.cs` corretamente.
Conteúdo esperado (validar no `Up()` gerado):
- `ALTER TABLE products ADD COLUMN click_count integer NOT NULL DEFAULT 0;`
- `CREATE TABLE product_clicks (id bigserial PRIMARY KEY, product_id uuid NOT NULL REFERENCES products(id) ON DELETE CASCADE, clicked_at timestamptz NOT NULL);`
- os 3 índices novos (2 em `products`, 1 em `product_clicks`).

## 3. Backend — API

### 3.1 Novo controller `PublicProductsController.cs` — `backend/src/AfiliadoBot.Api/Controllers/PublicProductsController.cs`
Route `api/public/products`, mesmos atributos de classe de `PublicController` (`[ApiController]`,
`[AllowAnonymous]`), sem `[EnableRateLimiting]` de classe — aplicar por método (policies diferentes
por endpoint, ver 3.2/3.3).

```csharp
[ApiController]
[Route("api/public/products")]
[AllowAnonymous]
public class PublicProductsController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;
    public PublicProductsController(AfiliadoBotDbContext db) { _db = db; }
    // ...
}
```

### 3.2 `POST /api/public/products/{id:guid}/click`
```csharp
[HttpPost("{id:guid}/click")]
[EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)]
public async Task<IActionResult> RegisterClick(Guid id, CancellationToken ct)
{
    var product = await _db.Products.FindAsync(new object[] { id }, ct);
    if (product is null)
        return Accepted(); // CA 2.4-adjacent: nunca falha de forma visível ao cliente do sendBeacon

    product.RegisterClick();
    _db.ProductClicks.Add(new ProductClick(id));
    await _db.SaveChangesAsync(ct);

    return Accepted();
}
```
- `202 Accepted` sempre (mesmo produto inexistente) — o `sendBeacon` do frontend não lê a resposta;
  não há motivo para vazar `404` que o client nunca vai consumir, e evita erro consumido por
  ferramentas de diagnóstico do browser sem necessidade.
- Reaproveita `RateLimiterConfigurator.PublicWritePolicy` (10 req/min/IP), já usado por
  `PushController` para `POST /api/public/push/subscribe` — mesmo padrão de endpoint público de
  escrita, não precisa de policy nova.
- Sem `[FromBody]` — nada no corpo (Decisão 4 do Arquiteto, compatível com `sendBeacon(url)`).

### 3.3 `GET /api/public/products/suggested`
```csharp
[HttpGet("suggested")]
[EnableRateLimiting(RateLimiterConfigurator.PublicReadPolicy)]
public async Task<ActionResult<List<PublicDealDto>>> GetSuggested(
    [FromQuery] string? categories, [FromQuery] bool hasResults, CancellationToken ct)
{
    const int limit = 10;
    const int minimumToShow = 4;

    var categoryList = string.IsNullOrWhiteSpace(categories)
        ? Array.Empty<string>()
        : categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var query = _db.Products.Where(p => p.Status == ProductStatus.Published);

    query = (categoryList.Length == 0 || !hasResults)
        ? query
        : query.Where(p => categoryList.Contains(p.Category));

    var products = await query
        .OrderByDescending(p => p.ClickCount)
        .ThenByDescending(p => p.CreatedAt)
        .Take(limit)
        .ToListAsync(ct);

    if (products.Count < minimumToShow)
        return Ok(new List<PublicDealDto>());

    return Ok(products.Select(p => PublicDealDto.FromProduct(p, Request)).ToList());
}
```
- Reaproveita `PublicDealDto.FromProduct` sem alteração (Decisão 3 do Arquiteto — mesmo shape de
  card, sem novo contrato).
- `PublicReadPolicy` (60 req/min/IP) — é leitura, mesmo padrão de `GetDeals`/`GetCategories`.
- Lista vazia (não erro) quando abaixo do mínimo — o frontend decide não renderizar (CA 1.5), sem
  precisar distinguir "vazio por regra" de "vazio por erro" (ambos resultam em omitir o carrossel).

## 4. Frontend (`website/`, Next.js)

### 4.1 `lib/tracking.ts` (novo, Client) — registro de clique
Segue o padrão de `lib/push.ts` (Client Component lib, `NEXT_PUBLIC_API_URL`, nunca
`API_INTERNAL_URL` que é server-only — ver comentário no topo de `lib/api.ts`).
```ts
'use client';

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export function trackProductClick(productId: string): void {
  const url = `${API_URL}/api/public/products/${productId}/click`;
  if (typeof navigator !== 'undefined' && 'sendBeacon' in navigator) {
    navigator.sendBeacon(url);
    return;
  }
  // Fallback (browsers sem sendBeacon) — fire-and-forget, não bloqueia a navegação.
  fetch(url, { method: 'POST', keepalive: true }).catch(() => {
    /* silenciado por design — CA 2.4, falha não deve ser percebida pelo usuário */
  });
}
```
`productId` — **precisa do `id` (uuid) do produto**, não do `slug`. O `Deal`/`PublicDealDto` atual
(`lib/types.ts`) **não expõe `id`** (só `slug`, usado como identificador público de rota). É
necessário adicionar `id: string` ao `PublicDealDto` (backend) e à interface `Deal` (frontend) —
registrar essa mudança na sub-issue de backend (T-03, mesmo DTO reaproveitado por `GetDeals` e
`GetSuggested`) e consumir em T-04. Confirmar que expor `id` (uuid interno, não sequencial) não veta
nenhuma regra do DTO público (`PublicDealDto` já documenta explicitamente os campos autorizados —
`id` não estava na lista original porque a rota de clique não existia; não há risco de exposição
sensível, é um uuid opaco já usado como PK pública em outras APIs REST comuns).

### 4.2 `DealCard.tsx` — boundary client/server
`DealCard` é hoje **Server Component** (sem `'use client'`, sem handlers). Não convertê-lo inteiro
em Client Component (perderia o comentário/decisão já documentada no próprio arquivo sobre evitar
esse boundary desnecessário). Extrair só o CTA:

Novo `components/DealCardLink.tsx` (Client):
```tsx
'use client';

import { trackProductClick } from '@/lib/tracking';

interface DealCardLinkProps {
  productId: string;
  href: string;
  className: string;
}

export default function DealCardLink({ productId, href, className }: DealCardLinkProps) {
  return (
    <a
      className={className}
      href={href}
      target="_blank"
      rel="nofollow"
      onClick={() => trackProductClick(productId)}
    >
      Ver oferta →
    </a>
  );
}
```
`DealCard.tsx` passa a renderizar `<DealCardLink productId={deal.id} href={deal.affiliateLink} className="deal-card__cta" />`
no lugar do `<a>` inline atual, mantendo o `<span>` de fallback ("Indisponível") como está — sem
mudança de comportamento quando `affiliateLink` é nulo.

### 4.3 `lib/suggested.ts` (novo, Client) — fetch da faixa
Mesmo padrão client-side de `lib/push.ts`/`lib/tracking.ts` — não em `lib/api.ts` (server-only).
```ts
'use client';

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export async function fetchSuggestedProducts(
  category: string | undefined,
  hasResults: boolean
): Promise<Deal[]> {
  const params = new URLSearchParams({ hasResults: String(hasResults) });
  if (category) params.set('categories', category);
  const response = await fetch(`${API_URL}/api/public/products/suggested?${params.toString()}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json();
}
```

### 4.4 `components/SuggestedProductsCarousel.tsx` (novo, Client)
Client Component — `useEffect` chama `fetchSuggestedProducts` em `try/catch` isolado (CA 1.8: erro
só afeta este componente, `useState` local para `deals`/`error`; em erro ou lista vazia, `return null`
— não renderiza nada, grid principal segue intacto). Recebe como props a categoria ativa (string
única — ver design.md §12.5, `app/page.tsx` só suporta 1 categoria hoje) e `hasResults` (calculado
pela página a partir de `deals.length > 0`, já existe em `app/page.tsx`).

Carrossel horizontal com setas: usar `overflow-x: auto` + scroll programático via `ref.scrollBy()`
nos handlers das setas (sem lib nova — catálogo pequeno, ~10 itens, não justifica dependência
externa de carrossel). Setas desabilitadas/ocultas nos extremos (CA 1.3) via checagem de
`scrollLeft`/`scrollWidth` no elemento, atualizada em `onScroll`.

Posição na página: **abaixo do grid principal**, acima da paginação — decisão de UX/UI a confirmar
com o agente UX/UI (design.md §11 deixou como pendência de UX, não arquitetural); usar essa posição
como default se o UX/UI não especificar diferente.

### 4.5 `app/page.tsx`
Adicionar, após a `section.deals-grid` (ou no bloco `deals.length === 0`, ver CA 1.2 — fallback deve
aparecer mesmo quando a listagem principal está vazia):
```tsx
<SuggestedProductsCarousel category={filters.category} hasResults={deals.length > 0} />
```
Renderizado sempre (dentro ou fora do `if (deals.length === 0)`), pois a faixa deve aparecer tanto
no caso normal (1.1) quanto no fallback (1.2) — só não renderiza nada quando `SuggestedProductsCarousel`
decide internamente (lista vazia/erro, CA 1.5/1.8).

## 5. Critérios de aceite → componente (rastreabilidade para QA)
| CA | Componente responsável |
|---|---|
| 2.1, 2.2, 2.3 | `PublicProductsController.RegisterClick` + `lib/tracking.ts` + `DealCardLink.tsx` |
| 2.4 | `lib/tracking.ts` (`sendBeacon`/`keepalive`, catch silencioso) |
| 1.1, 1.2, 1.6, 1.7 | `PublicProductsController.GetSuggested` (ordenação/fallback/limite) |
| 1.3 | `SuggestedProductsCarousel.tsx` (setas + scroll) |
| 1.4 | `DealCardLink.tsx` reaproveitado dentro do carrossel |
| 1.5 | `PublicProductsController.GetSuggested` (corte mínimo) + `SuggestedProductsCarousel.tsx` (não renderiza se lista vazia) |
| 1.8 | `SuggestedProductsCarousel.tsx` (try/catch isolado) |

## 6. Testes esperados (mínimo, cada sub-issue expande)
- Backend: xUnit — `ProductTests` (novo teste de `RegisterClick`), `PublicProductsControllerTests`
  (click em produto existente/inexistente, suggested com/sem categoria, fallback, corte mínimo,
  desempate por `CreatedAt`), migration aplicável em banco de teste (Postgres real ou InMemory
  onde já é o padrão do projeto — `SearchVector` é a única exceção condicional a Npgsql real).
- Frontend: Jest + RNTL-equivalente (Testing Library React já usado no projeto) —
  `DealCardLink.test.tsx` (chama `trackProductClick` no click, sem alterar `href`),
  `SuggestedProductsCarousel.test.tsx` (renderiza produtos, esconde em erro/vazio, setas
  habilitam/desabilitam nos extremos), `lib/tracking.test.ts`, `lib/suggested.test.ts`. Cobertura
  mínima 80% (padrão do repo, ver `website/CLAUDE.md`/`package.json`).
- e2e (Playwright, `website/e2e/`): fluxo completo — filtrar categoria com resultado → ver faixa →
  clicar em item do carrossel não quebra navegação; filtrar categoria sem resultado → ver fallback.
