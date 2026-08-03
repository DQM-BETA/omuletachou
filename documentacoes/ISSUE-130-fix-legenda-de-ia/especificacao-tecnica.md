# Especificação Técnica — ISSUE-130: fix: Legenda de IA nunca é persistida

## 1. Migration — `PublicationQueue.Caption`

Nova migration EF Core (nome sugerido: `AddCaptionToPublicationQueue`, seguindo o padrão de nomes já usado
no projeto, ex.: `20260707125445_InitialSchema`). SQL efetivo (gerado pelo EF a partir da alteração do
model, não escrito manualmente):

```sql
ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL DEFAULT '';
```

### 1.1 Entidade `PublicationQueue.cs`
Adicionar propriedade e ajustar o construtor público (`ProcessorJob` é o único caller em produção; testes
usam o mesmo construtor):

```csharp
public string Caption { get; private set; } = string.Empty;

// Construtor para EF Core
private PublicationQueue() { }

public PublicationQueue(Guid productId, SocialNetwork socialNetwork, DateTime scheduledAt, string caption)
{
    Id = Guid.NewGuid();
    ProductId = productId;
    SocialNetwork = socialNetwork;
    ScheduledAt = scheduledAt;
    Caption = caption ?? string.Empty;
    Status = PublicationStatus.Scheduled;
    RetryCount = 0;
    CreatedAt = DateTime.UtcNow;
}
```
Assinatura muda de 3 para 4 parâmetros (breaking change interno, único caller é `ProcessorJob`). Não criar
overload — atualizar o único call site.

### 1.2 `PublicationQueueConfiguration.cs`
Adicionar mapeamento (após `ScheduledAt`, por exemplo):
```csharp
builder.Property(x => x.Caption)
    .HasColumnName("caption")
    .HasColumnType("text")
    .HasDefaultValue(string.Empty)
    .IsRequired();
```

### 1.3 Sem backfill (CA2)
Itens de `PublicationQueue` já existentes ficam com `Caption=''` após a migration — nenhuma tarefa/script de
backfill é criado. Nota de changelog obrigatória no PR (CA18): uma linha mencionando que publicações
anteriores à migration permanecem sem legenda de IA (sem backfill), para contexto histórico.

## 2. `ProcessorJob.cs` — persistência da legenda gerada

Em `CreatePublicationQueueEntriesAsync` (arquivo
`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`), o retorno de `GenerateCaptionAsync` é hoje
descartado (linha 256: `await _aiService.GenerateCaptionAsync(product, network, ct);`). Corrigir para:

```csharp
var caption = await _aiService.GenerateCaptionAsync(product, network, ct);

var entry = new PublicationQueue(product.Id, network, scheduledAt, caption);

if (network == SocialNetwork.Facebook)
    entry.MarkAsManualPending();

_dbContext.PublicationQueues.Add(entry);
```
Nenhuma outra mudança de fluxo: comportamento de erro/retry de `GenerateCaptionAsync` (CA6) já é tratado
pelo `IAiService`/chamador externo ao `ProcessorJob` (fora deste método) — preservar inalterado.

## 3. Publishers automáticos — leitura de `item.Caption`

Os 4 publishers hoje montam a legenda a partir de `product.AiCaption ?? string.Empty` (ou, no caso de
Instagram/TikTok, `SocialDisclosureHelper.AppendIfMissing(product.AiCaption ?? string.Empty)`). Trocar a
fonte para `item.Caption` (o parâmetro `PublicationQueue item` recebido em `PublishAsync`), mantendo o
`SocialDisclosureHelper` onde já é usado:

- **`TelegramPublisher.cs`** (linha 51): `var caption = product.AiCaption ?? string.Empty;` →
  `var caption = item.Caption;` (já não-nulo por design; manter `?? string.Empty` como defesa se preferir).
- **`YoutubePublisher.cs`** (`BuildMetadataJson`, linha 255): `var description = product.AiCaption ?? string.Empty;`
  → `BuildMetadataJson` precisa receber `item.Caption` como parâmetro adicional (ou o `item` inteiro) em vez
  de derivar de `product`; ajustar a assinatura do método e o call site em `PublishAsync`.
- **`InstagramPublisher.cs`** (linha 118): `var caption = SocialDisclosureHelper.AppendIfMissing(product.AiCaption ?? string.Empty);`
  → `var caption = SocialDisclosureHelper.AppendIfMissing(item.Caption);`.
- **`TikTokPublisher.cs`** (linha 124): `var caption = SocialDisclosureHelper.AppendIfMissing(product.AiCaption ?? string.Empty);`
  → `var caption = SocialDisclosureHelper.AppendIfMissing(item.Caption);`.

**CA11 (item legado, `Caption=''`):** todos os 4 publishers já tratam string vazia sem exceção (concatenação/
serialização de string vazia é comportamento padrão) — nenhuma lógica de fallback adicional é necessária além
de garantir que `item.Caption` nunca seja `null` (garantido pelo default do EF/construtor).

**CA5:** nenhum publisher deve mais referenciar `product.AiCaption` após o ajuste — verificar com busca
textual (`grep -rn "AiCaption" backend/src/AfiliadoBot.Infrastructure/Integrations/Social/`) que não sobra
nenhuma ocorrência nos 4 arquivos.

## 4. Facebook Manual — backend (`ProductDetailDto` + `ProductsController`)

### 4.1 `ProductDtos.cs`
Adicionar campo `AiCaption` (nome de propriedade C# `AiCaption`, serializado como `ai_caption` — mesmo
padrão snake_case já usado para `ai_score`/`ai_reason` neste DTO):

```csharp
public record ProductDetailDto(
    Guid Id,
    string Title,
    string Description,
    decimal SalePrice,
    decimal OriginalPrice,
    decimal DiscountPct,
    string? AffiliateLink,
    string? ImageUrl,
    string? MediaUrl,
    string? MediaLocalPath,
    string Slug,
    string Category,
    string Platform,
    string Status,
    [property: JsonPropertyName("ai_score")] int? AiScore,
    [property: JsonPropertyName("ai_reason")] string? AiReason,
    [property: JsonPropertyName("ai_caption")] string? AiCaption,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```
`AiCaption` é a `Caption` do item de `PublicationQueue` mais recente do produto para a rede Facebook — `null`
quando não existe nenhum item de `PublicationQueue` para essa rede (produto ainda não enfileirado para
Facebook, ou legado). String vazia (`""`) é um valor válido (item existe mas `Caption=''`, legado pré-fix ou
pré-migration) — distinguir de `null` no frontend (CA14: "vazia ou fallback explícito", ambos os casos
tratados sem quebrar a UI).

### 4.2 `ProductsController.GetProduct` (CA12)
Buscar o item de `PublicationQueue` da rede Facebook para o produto, ordenado por `CreatedAt` decrescente
(mais recente primeiro — cobre o caso raro de mais de um item por produto/rede):

```csharp
var facebookCaption = await _db.PublicationQueues
    .AsNoTracking()
    .Where(q => q.ProductId == id && q.SocialNetwork == SocialNetwork.Facebook)
    .OrderByDescending(q => q.CreatedAt)
    .Select(q => (string?)q.Caption)
    .FirstOrDefaultAsync(ct);

var dto = new ProductDetailDto(
    product.Id,
    product.Title,
    product.Description,
    product.SalePrice,
    product.OriginalPrice,
    product.DiscountPct,
    product.AffiliateLink,
    product.ImageUrl,
    product.MediaUrl,
    product.MediaLocalPath,
    product.Slug,
    product.Category,
    product.Platform.ToString(),
    product.Status.ToString(),
    product.AiScore,
    product.AiReason,
    facebookCaption,
    product.CreatedAt,
    product.UpdatedAt);
```
Necessário `using AfiliadoBot.Domain.Enums;` já presente no arquivo (`SocialNetwork` já é usado em outros
controllers do projeto — confirmar import).

## 5. Facebook Manual — frontend (Angular)

### 5.1 `products.service.ts`
Adicionar campo à interface `ProductDetail`:
```typescript
export interface ProductDetail extends ProductListItem {
  description: string;
  affiliateLink: string | null;
  imageUrl: string | null;
  mediaUrl: string | null;
  mediaLocalPath: string | null;
  updatedAt: string;
  ai_caption?: string | null;
}
```
(Segue o padrão já usado em `ProductListItem` para `ai_score`/`ai_reason`: snake_case no nome do campo
TypeScript, espelhando o JSON vindo do backend — sem camelCase, sem `@JsonProperty` no lado Angular porque
não há decorators de serialização customizados neste projeto, o HttpClient desserializa por nome literal.)

### 5.2 `facebook-manual.component.html`
Trocar a fonte da legenda exibida e copiada de `post.product?.description` para
`post.product?.ai_caption`, com fallback explícito para string vazia/mensagem quando ausente (CA14):

```html
<mat-card-content>
  <p class="caption" data-testid="caption-text">
    {{ post.product?.ai_caption || 'Legenda não disponível' }}
  </p>
</mat-card-content>

<mat-card-actions>
  <button
    mat-stroked-button
    color="primary"
    (click)="copyCaption(post.product?.ai_caption || '')"
    data-testid="copy-caption-button"
  >
```
Texto de fallback (`'Legenda não disponível'`) é uma sugestão — decisão de copy final fica a critério do
dev/PM se quiser ajustar a string exata; o requisito funcional (CA14) é apenas "não exibir silenciosamente a
descrição original disfarçada de legenda de IA" e "não quebrar a UI".

### 5.3 `facebook-manual.component.ts`
Nenhuma mudança de lógica necessária (o componente já usa `post.product?.description` diretamente no
template, sem transformação em TS) — apenas o template muda a propriedade lida.

## 6. `ProcessorJobTests.cs` — cobertura corrigida (CA15-CA17)

`CreateAiServiceMock()` (linha 84-90) já mocka `GenerateCaptionAsync` para retornar uma string fixa
(`"Legenda gerada"`) para qualquer rede — para CA16 (múltiplas redes com captions distintas), o teste
precisa de um mock que retorne valores diferentes por `SocialNetwork` recebido, ex.:

```csharp
private static Mock<IAiService> CreateAiServiceMock()
{
    var mock = new Mock<IAiService>();
    mock.Setup(a => a.GenerateCaptionAsync(It.IsAny<Product>(), It.IsAny<SocialNetwork>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Product _, SocialNetwork network, CancellationToken _) => $"Legenda {network}");
    return mock;
}
```
Isso torna a legenda determinística por rede sem quebrar os testes existentes que já usam
`CreateAiServiceMock()` (eles não checavam o valor da legenda antes, apenas a existência do item).

Testes a adicionar/ajustar (todos os métodos já existentes que fazem `db.PublicationQueues.Where(...)
.ToListAsync()` — linhas 224, 240, 257, 281, 335, 363, 397, 417, 444, 470, 491, 512):
- **CA15:** em pelo menos um teste que já verifica a criação do item Telegram/Instagram/TikTok, adicionar
  assert: `entries.Single(e => e.SocialNetwork == SocialNetwork.Telegram).Caption.Should().Be("Legenda Telegram");`
  (ou o valor correspondente ao mock).
- **CA16:** teste novo (ou extensão de um existente com múltiplas redes habilitadas) que habilita 2+ redes
  para o mesmo produto e verifica que cada `PublicationQueue` tem `Caption` distinta correspondente à sua
  própria rede — evidenciando que não há sobrescrita (o bug original usava um campo único em
  `Product.AiCaption`, que seria sobrescrito a cada chamada; com `PublicationQueue.Caption` por item, isso
  não pode mais ocorrer).
- **CA17 (regressão):** os testes existentes que já usam `Times.Never` (linhas 399, 472) permanecem
  inalterados — apenas complementar (não substituir) com a assertiva de que nenhum item de
  `PublicationQueue` para aquela rede foi criado (`entries.Should().BeEmpty()` ou equivalente, se ainda não
  presente).

## 7. Ordem de implementação recomendada (Sub-A → Sub-B)
1. Migration + `PublicationQueue.Caption` + `PublicationQueueConfiguration`.
2. `ProcessorJob.cs` (persistência).
3. 4 publishers (leitura de `item.Caption`).
4. `ProcessorJobTests.cs` (cobertura corrigida).
5. `ProductDetailDto` + `ProductsController` (expõe `ai_caption`) — **fecha o contrato que a Sub-B consome**.
6. (Sub-B, após #5 mergeado/disponível) `ProductDetail`/`ProductsService` + `facebook-manual.component`
   (html + eventual ajuste de teste do componente, `facebook-manual.component.spec.ts`).

## 8. Changelog obrigatório (CA18)
O PR da Sub-A deve conter, na descrição, uma linha equivalente a:
> **Nota:** publicações enfileiradas antes desta migration permanecem com `Caption=''` (sem legenda de IA),
> sem processamento retroativo (backfill) — apenas os itens enfileirados a partir deste fix têm legenda
> persistida corretamente.
