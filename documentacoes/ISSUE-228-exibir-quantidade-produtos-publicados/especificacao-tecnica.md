# Especificação Técnica — ISSUE-228: Relatório de produtos com filtros na tela Reports

> Decisões de arquitetura completas em `openspec/changes/issue-228-relatorio-produtos-filtros/design.md`.
> Este documento consolida os contratos que os devs implementam.

## 1. Índice novo (backend, `ProductConfiguration.cs`)

```csharp
builder.HasIndex(x => new { x.Status, x.Platform, x.CreatedAt })
    .HasDatabaseName("IX_products_status_platform_createdat")
    .IsDescending(false, false, true);
```
Migration EF Core nova (nome sugerido: `AddStatusPlatformCreatedAtIndex`), gerada com
`dotnet ef migrations add AddStatusPlatformCreatedAtIndex --project backend/src/AfiliadoBot.Infrastructure --startup-project backend/src/AfiliadoBot.Api`.
Só `CREATE INDEX` — sem alteração de tipo/coluna.

## 2. Endpoint novo — `GET /api/reports/products/summary` (`ReportsController`)

Protegido por `[Authorize]` (mesmo padrão do resto do controller).

**Query params (todos opcionais):** `category`, `subcategory`, `platform`, `status`, `collectedFrom` (`yyyy-MM-dd`), `collectedTo` (`yyyy-MM-dd`).

**Regras de filtro (mesmo padrão de `ProductsController.GetProducts`/`PublicController.GetDeals`):**
- `category`/`subcategory`: match exato de string (`p.Category == category`, `p.Subcategory == subcategory`), ignorado se vazio/whitespace.
- `platform`/`status`: `Enum.TryParse(ignoreCase: true)`; se o valor não bate com o enum, filtra para `Where(_ => false)` (não é erro 400 — mesma postura defensiva do resto do projeto).
- `collectedFrom`/`collectedTo`: filtro de faixa sobre `Product.CreatedAt` (é a data de coleta, ver design.md §1). Convertidos para janela `[from, toExclusive)`: `from = collectedFrom.Date` (UTC), `toExclusive = collectedTo.Date.AddDays(1)` (UTC) — inclui o dia final inteiro (CA 2.5).
- **Sem default de `status=Published` no backend** — se `status` vier vazio, o filtro de status não é aplicado (retorna todos os status). O default `Published` é responsabilidade exclusiva do Angular (§4).

**Response 200 (`ProductsReportSummaryDto`):**
```json
{
  "total": 0,
  "byPlatform": [{ "platform": "MercadoLivre", "count": 0 }],
  "byCategory": [{ "category": "Eletrônicos", "count": 0 }],
  "byStatus": [{ "status": "Published", "count": 0 }],
  "bySubcategory": [{ "subcategory": "Celulares", "count": 0 }]
}
```
Sem paginação. Sem resultado (nenhum produto casa o filtro) → `total: 0` e as 4 listas de breakdown vazias `[]`, **200 OK** (nunca erro) — CA 1.3/2.7.

**Implementação:** uma única `IQueryable<Product>` base com os filtros aplicados (`Where` aditivo), depois `CountAsync()` + 4 `GroupBy(...).Select(...).ToListAsync()` independentes sobre a mesma base (design.md §3). DTOs novos em `AfiliadoBot.Api/Products/ProductDtos.cs` (mesmo arquivo dos DTOs de produto, para não fragmentar) ou em novo arquivo `AfiliadoBot.Api/Reports/ReportsDtos.cs` — critério do dev, manter só um lugar.

## 3. Extensão — `GET /api/products` (`ProductsController.GetProducts`)

**4 novos query params opcionais, aditivos aos já existentes (`status`, `platform`, `page`, `pageSize`):** `category`, `subcategory`, `collectedFrom`, `collectedTo` — mesmas regras de filtro do item 2 acima (reaproveitar a mesma lógica de conversão de data, ver §5 "duplicação aceita").

**`ProductListItemDto`:** novo campo `Subcategory` (`string?`) **ao final** do record (mesmo padrão aditivo já usado para `SourceUrl`/`Destinations`, Issue #184/#208) — não reordenar os campos existentes.

**Não-regressão:** sem os 4 novos params, comportamento de `GetProducts` idêntico ao atual (usado por `ProductsComponent`, que não filtra por essas dimensões) — os filtros são aditivos via `Where`, nunca mudam o resultado quando ausentes.

## 4. Frontend — Angular (`dashboard/`)

### `reports.service.ts`
Novo método:
```typescript
productsSummary(filters: ProductsReportFilters): Observable<ProductsReportSummary>
// GET /api/reports/products/summary?<query>
```
Nova interface `ProductsReportSummary` espelhando o DTO do item 2. Nova interface `ProductsReportFilters` (`category?`, `subcategory?`, `platform?`, `status?`, `collectedFrom?`, `collectedTo?`) — usar `cleanParams` (já existe em `paged-result.model.ts`, usado por `products.service.ts`) para omitir campos vazios da query string.

### `products.service.ts`
`ProductsListParams` ganha `category?`, `subcategory?`, `collectedFrom?`, `collectedTo?` (opcionais, aditivos). `ProductListItem` ganha `subcategory?: string | null`.

### `reports.component.ts` / `.html`
- Novo `filterForm` (Reactive Forms, mesmo padrão Angular Material já usado no dashboard) com os 6 campos: `category`, `subcategory`, `platform`, `status`, `collectedFrom`, `collectedTo`.
- **Default de Status = Published (CA 1.1/2.4):** ao montar os params para as chamadas de API, se `filterForm.value.status` estiver vazio, enviar `status: 'Published'`; se o operador escolher outro valor no filtro de Status, enviar o valor escolhido. Isso vale para as duas chamadas (summary + list).
- Ao carregar a tela e a cada mudança de filtro (CA 2.9): `forkJoin([reportsService.productsSummary(filters), productsService.list({ ...filters, page: 1 })])` — mesmo padrão `forkJoin` já usado em `loadReports()` hoje.
- Troca de página da tabela (mesmo filtro ativo): só `productsService.list({ ...filters, page: N })`, sem recalcular os cards (design.md §2.1, trade-off aceito).
- "Limpar filtros" (CA 2.8): reset do `filterForm`, refaz a chamada com filtros vazios (volta ao universo completo, `status` ainda default `Published`).
- Estado vazio (CA 1.3/2.7): cards mostram zero, tabela/gráfico mostra mensagem de "nenhum produto encontrado" — sem erro.
- Erro de rede (CA 5.1): mensagem de erro visível (reaproveitar `errorMessage`/padrão já usado em `loadReports()`), sem manter dado da consulta anterior na tela, com opção de tentar novamente (reaplicar o filtro/recarregar).
- Cards/gráfico existentes ("Hoje/Semana/Mês", "Publicações por rede") **não são tocados** — o novo bloco é adicionado abaixo, no mesmo componente (CA 1.2, design.md "Contrato de componentes globais").
- Composição visual exata (layout dos filtros, tipo de gráfico) é decisão do agente **UX/UI**, que atua antes da implementação — este documento define o contrato de dados, não o layout.

## 5. Duplicação de lógica de data (aceita, não bloqueante)

`ReportsController.ProductsSummary` (novo) e `ProductsController.GetProducts` (estendido) precisam da mesma conversão `collectedFrom`/`collectedTo` → janela `[from, toExclusive)` (§2, §3). Reaproveitar um helper único (ex.: `AfiliadoBot.Api.Common.DateRangeExtensions.ToInclusiveUtcRange(from, to)`) é preferível, mas **não bloqueia**: se as duas sub-issues (T-02/T-03) forem implementadas em paralelo, cada dev pode implementar a conversão inline de forma consistente com a regra acima; o LT reconcilia duplicação no merge/code review, sem re-trabalho de contrato (design.md §6, risco aceito).

## 6. Padrões obrigatórios (reforço)

- `[Authorize]` em qualquer endpoint novo/estendido deste escopo (não é público).
- Filtro inválido/enum desconhecido nunca retorna 400 — resulta em "sem match" (postura já estabelecida no projeto).
- Nenhuma mudança de contrato quebra consumidores existentes — todos os campos/params novos são aditivos.
- Cobertura de testes: seguir os casos mapeados em `design.md` §5 (`ReportsControllerTests`, `ProductsControllerTests`, `reports.component.spec.ts`).
