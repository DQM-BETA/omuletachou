# Especificação Técnica — ISSUE-260: Busca textual inteligente (fonética/fuzzy) na tela de produtos do site público

> Complementa `design.md` (openspec_path) com os contratos exatos que os devs implementam.
> Decisões de arquitetura (por que 2 estágios, pesos, threshold) estão no design — não repetidas
> aqui além do necessário para o contrato.

## 1. Migration + schema (backend)

Nova migration EF Core (`migrationBuilder.Sql(...)`, SQL raw — feature Postgres-específica sem
equivalente na API fluente):

```sql
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE OR REPLACE FUNCTION immutable_unaccent(text) RETURNS text AS $$
  SELECT unaccent('unaccent', $1)
$$ LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT;

ALTER TABLE products ADD COLUMN search_vector tsvector
  GENERATED ALWAYS AS (
    setweight(to_tsvector('portuguese', immutable_unaccent(title)),       'A') ||
    setweight(to_tsvector('portuguese', immutable_unaccent(category)),    'B') ||
    setweight(to_tsvector('portuguese', immutable_unaccent(description)), 'C')
  ) STORED;

CREATE INDEX "IX_products_search_vector" ON products USING gin (search_vector);
```

Nome sugerido da migration: `AddProductSearchVector`.

**`ProductConfiguration.cs`:** mapear `search_vector` como shadow property (`tsvector`) via
`builder.Property<NpgsqlTsVector>("SearchVector").HasColumnName("search_vector")` — necessário
para o model snapshot do EF não tentar recriar/dropar a coluna gerada em migrations futuras. Não
expor propriedade CLR pública em `Product` (não é usada em C#, só em SQL raw da query de busca —
ver §2). Registrar o índice GIN via `builder.HasIndex("SearchVector").HasMethod("gin")
.HasDatabaseName("IX_products_search_vector")` **ou** deixar só no SQL raw da migration (shadow
index em coluna `tsvector` tem suporte limitado no EF Core 8 — se `HasIndex` no shadow property
gerar SQL divergente do bloco `migrationBuilder.Sql`, priorizar o SQL raw e usar
`HasAnnotation("Relational:SuppressPendingModelChangesWarning", true)` ou registrar o índice só via
`.HasComment`/anotação para não duplicar; validar com `dotnet ef migrations add` gerando migration
vazia depois — se gerar diff, ajustar o mapeamento).

**Requer `Npgsql.EntityFrameworkCore.PostgreSQL` >= 8.x com suporte a `NpgsqlTsVector`** — já é a
versão em uso no projeto (confirmar em `AfiliadoBot.Infrastructure.csproj`; não deve exigir bump).

**Teste desta sub-issue:** aplicar a migration localmente (`dotnet ef database update`) e confirmar
que sobe sem erro sobre a base de desenvolvimento; inspecionar o schema resultante (coluna gerada +
índice GIN existem com a definição esperada). Não precisa testar a query de busca aqui (isso é da
sub-issue do endpoint).

## 2. Query dos 2 estágios (backend)

A query usa SQL raw via `FromSqlInterpolated`/`FromSqlRaw` ou `EF.Functions` — **EF Core 8 não tem
tradução LINQ nativa para `@@`/`ts_rank`/`similarity()`**, então a forma mais direta é compor a
`IQueryable<Product>` combinando `.Where(p => EF.Functions.ToTsVector(...))`. Duas opções viáveis
(o Dev escolhe a que compilar mais limpo, documentando a escolha no PR):

**Opção A — `EF.Functions` (Npgsql provider já expõe `ToTsVector`/`Matches` desde v8):**
```csharp
var tsQuery = q; // sanitizado (§2.6 do design) — usar plainto_tsquery, não to_tsquery, para não
                  // exigir sintaxe de operador do usuário e evitar erro de parse com caracteres
                  // especiais
var tier1 = query
    .Where(p => EF.Functions.ToTsVector("portuguese",
                    EF.Functions.Unaccent(p.Title) + " " + EF.Functions.Unaccent(p.Category) + " " + EF.Functions.Unaccent(p.Description))
                .Matches(EF.Functions.PlainToTsQuery("portuguese", q)))
```
**Cuidado:** essa forma recalcula o `tsvector` on-the-fly (não usa a coluna gerada/índice GIN).
**Preferir a Opção B**, que usa a coluna `search_vector` já materializada e indexada — é o
propósito do design (§2.4 do design.md: índice GIN "sempre usado, caminho quente").

**Opção B — via coluna gerada (recomendada, usa o índice):**
```csharp
// EF.Property<NpgsqlTsVector>(p, "SearchVector") acessa o shadow property mapeado em §1.
var tsQuery = EF.Functions.PlainToTsQuery("portuguese", q); // ou WebSearchToTsQuery se disponível
var tier1 = query
    .Where(p => EF.Property<NpgsqlTsVector>(p, "SearchVector").Matches(tsQuery))
    .OrderByDescending(p => EF.Functions.ToTsVector("portuguese", "").Rank(...)) // ver nota abaixo
```
Ranking: usar `EF.Functions.RankCoverDensity`/`.Rank(EF.Property<NpgsqlTsVector>(p,"SearchVector"),
tsQuery)` conforme a API exposta pela versão do provider Npgsql em uso — **confirmar a assinatura
exata disponível na versão instalada** (`dotnet list package` no projeto Infrastructure/Api) antes
de codar; se a tradução LINQ para `ts_rank` sobre shadow property não for suportada de forma limpa,
cair para SQL raw parcial (`FromSqlInterpolated` retornando `Product` + `.Where`/`.OrderBy`
compostos em cima, já que `FromSql` participa de composição LINQ normalmente no EF Core).

Estágio 2 (fallback, só quando `tier1` retorna 0 — ver fluxo completo em `design.md` §3):
```csharp
var t = SearchConstants.ApproximateSimilarityThreshold; // 0.15
var tier2 = query.Where(p =>
    EF.Functions.TrigramsSimilarity(p.Title, q) >= 0 || true) // placeholder — ver nota
    .Where(p => Math.Max(EF.Functions.TrigramsSimilarity(p.Title, q),
                Math.Max(EF.Functions.TrigramsSimilarity(p.Category, q),
                         EF.Functions.TrigramsSimilarity(p.Description, q))) >= t)
    .OrderByDescending(p =>
        0.60 * EF.Functions.TrigramsSimilarity(p.Title, q) +
        0.25 * EF.Functions.TrigramsSimilarity(p.Category, q) +
        0.15 * EF.Functions.TrigramsSimilarity(p.Description, q));
```
`EF.Functions.TrigramsSimilarity` é o método exposto pelo Npgsql provider para `similarity()` do
`pg_trgm` (confirmar nome exato na versão em uso — pode ser `TrigramsSimilarity` ou
`FuzzyStringMatchSimilarity` dependendo da versão; se não existir tradução LINQ, usar SQL raw
equivalente). `Math.Max` de 3 argumentos não existe — usar `Math.Max(a, Math.Max(b, c))` (aninhado,
como no rascunho acima) ou `new[] { a, b, c }.Max()` se o provider traduzir `Enumerable.Max` sobre
literal array (validar; se não traduzir, aninhar `Math.Max`).

**Se a tradução LINQ para qualquer uma dessas funções não for suportada de forma limpa pela versão
do Npgsql provider em uso**, a alternativa é `FromSqlInterpolated<Product>($"SELECT * FROM products
WHERE ...")` retornando `IQueryable<Product>` e compondo os demais `.Where()` de filtro (category/
minPrice/etc.) por cima — o EF Core compõe SQL raw com LINQ normalmente, desde que o SQL raw
selecione `SELECT *` da tabela mapeada. Documentar no PR qual caminho foi usado.

## 3. Contrato do endpoint

`GET /api/public/deals` — novo parâmetro:
```
[FromQuery] string? q
```

Comportamento (pseudocódigo completo em `design.md` §3 — seguir exatamente):
1. `q` irrelevante (`null`/vazio/whitespace ou `.Trim().Length < 2`) → comportamento 100% atual,
   `IsApproximateSearch = null` no `PagedResult<T>`.
2. `q` relevante → **ignora `sort`** (ordena sempre por relevância); roda estágio 1
   (`search_vector @@ tsquery`, `ORDER BY ts_rank DESC`); se `total1 > 0` → retorna,
   `IsApproximateSearch = false`.
3. Se `total1 == 0` → roda estágio 2 (trigram, `GREATEST(similarity...) >= 0.15`,
   `ORDER BY combined_score DESC`); retorna; `IsApproximateSearch = (total2 > 0)`.
4. `q` composto com os demais filtros já existentes (`category`/`subcategory`/`minPrice`/
   `maxPrice`) via AND — mesma `IQueryable` base, `.Where(q)` é só mais um filtro (ver design.md
   §2.6). **Não** aplicar `minDiscount` a menos que já esteja ativo (comportamento inalterado desse
   filtro).

**Constantes novas** (classe estática `SearchConstants` em `AfiliadoBot.Api` ou próxima ao
controller):
```csharp
public static class SearchConstants
{
    public const int MinQueryLength = 2;
    public const double ApproximateSimilarityThreshold = 0.15;
}
```

**`PagedResult<T>`** (`backend/src/AfiliadoBot.Api/Common/PagedResult.cs`) ganha campo aditivo:
```csharp
public bool? IsApproximateSearch { get; init; } // null = sem `q`; false = estágio 1; true = estágio 2
```
Default `null` — não quebra nenhum consumidor existente (`ProductsController`/`QueueController`/
demais chamadas de `PublicController`). `PublicController.ToDtoPagedResultAsync` (helper privado
existente) precisa propagar esse campo — hoje ele reconstrói o `PagedResult<PublicDealDto>`
manualmente (linha ~128 do arquivo atual); adicionar o parâmetro.

**Vazio genuíno:** `Items.Count == 0 && q relevante` com `IsApproximateSearch == false` (não
`true`) — é o estado que o frontend usa para distinguir de "resultados aproximados vazios" (que
não existe como estado separado: se `total2 == 0`, `IsApproximateSearch` já vem `false`, ver §3.3
acima e design.md §2.5).

## 4. Testes backend — Testcontainers obrigatório

`PublicControllerTests` hoje roda contra `CustomWebApplicationFactory`, que força
`UseInMemoryDatabase` (`backend/src/AfiliadoBot.Tests/CustomWebApplicationFactory.cs`) — **o
provider InMemory do EF Core não suporta `tsvector`/`pg_trgm`/funções SQL do Postgres**, então os
testes desta issue não podem rodar nesse fixture.

**Padrão a seguir — precedente `ClaudeBudgetServiceIntegrationTests`
(`backend/src/AfiliadoBot.Tests/Services/ClaudeBudgetServiceIntegrationTests.cs`):** sobe um
Postgres real via `Testcontainers.PostgreSql` (`postgres:16.14-alpine`, já usado no
`docker-compose.yml` do projeto — sem imagem nova), roda `db.Database.MigrateAsync()` (aplica todas
as migrations reais, inclui a nova desta issue), e testa contra esse banco.

Duas formas de aplicar esse padrão ao `PublicController` (o Dev escolhe, documentando no PR):
- **(a) Nova classe de teste dedicada** (ex. `PublicSearchTests`) que sobe o Postgres via
  Testcontainers e monta seu próprio `WebApplicationFactory` (ou uma variante de
  `CustomWebApplicationFactory` parametrizável para trocar `UseInMemoryDatabase` por
  `UseNpgsql(connectionString)`), fazendo requisições HTTP reais via `HttpClient` — mantém a
  cobertura ponta-a-ponta (rota + rate limit + serialização), like os testes atuais de
  `PublicControllerTests`.
- **(b) Testar a query/lógica de busca isolada** (helper/service extraído do controller,
  ex. `ProductSearchService.SearchAsync(...)`) direto contra o `DbContext` Testcontainers, sem
  `WebApplicationFactory` — mais rápido, segue o padrão exato de
  `ClaudeBudgetServiceIntegrationTests` (que testa o service, não via HTTP). **Recomendado** se o
  Dev decidir extrair a lógica de 2 estágios para uma classe própria (o que também ajuda a manter
  `PublicController.GetDeals` legível — hoje já é o endpoint mais complexo do controller). Extração
  não é obrigatória, mas se o método `GetDeals` passar de ~40-50 linhas com a lógica de busca
  inline, extrair é a decisão certa.

**Cenários obrigatórios:** lista completa em `design.md` §5 (não duplicada aqui). Cobrir todos —
inclui a asserção explícita de que nenhuma chamada à IA/Claude é disparada (CA 7.1: mock/spy do
client de IA, se houver algum já injetável nos testes, ou simplesmente ausência de qualquer
referência a `Anthropic.SDK`/`IClaudeClient` no caminho de código da busca — revisão de código
como evidência, já que a busca não deveria sequer ter uma dependência desse tipo injetada).

**Setup de teste:** reaproveitar `SeedPublishedProductAsync` (helper existente em
`PublicControllerTests`) como referência para criar produtos com título/categoria/descrição
controlados nos testes de busca — copiar/adaptar helper equivalente na nova classe de teste, já que
`WebApplicationFactory`/fixture serão diferentes (Testcontainers vs. InMemory).

## 5. Frontend

### 5.1 `website/lib/api.ts`
`DealFilters` ganha `q?: string`; `fetchDeals` propaga `q` na querystring quando presente (mesmo
padrão dos demais campos, `params.set('q', filters.q)` só se truthy).

### 5.2 `website/lib/types.ts`
`PagedResult<T>` ganha `isApproximateSearch?: boolean | null;`.

### 5.3 `website/components/FilterBar.tsx`
Novo grupo/input de busca textual, reaproveitando **exatamente** o mecanismo já usado para preço
(`minDraft`/`maxDraft` + `PRICE_COMMIT_DEBOUNCE_MS` + `commitPriceParams` via `router.replace`,
linhas ~148-234 do arquivo atual):
- Estado local `searchDraft` (inicializado de `searchParams.get('q') ?? ''`), ref
  `searchDraftRef` (evita closure obsoleta no debounce, mesmo padrão de `minDraftRef`).
- Constante `SEARCH_COMMIT_DEBOUNCE_MS = 350` (design.md §3 — mais alto que os 250ms do preço, que
  é um gesto contínuo de arrasto; digitação tem pausas naturais maiores entre palavras).
- `commitSearch()`: `router.replace` com `q` setado (ou removido se vazio) — **mesma função**
  `params.delete('page')` já usada em `commitPriceParams`/`updateParams` (reset de paginação).
- `onChange` do `<input type="search">` (ou `type="text"`) atualiza `searchDraft` +
  `searchDraftRef.current` + agenda o debounce (`setTimeout`, `clearTimeout` do anterior — mesmo
  padrão de `scheduleDebouncedPriceCommit`). Limpar o timer no `useEffect` de cleanup (mesmo padrão
  da linha ~196-203).
- Ressincronizar `searchDraft` a partir da URL sempre que `q` mudar por outra via (Limpar filtros,
  navegação/back-forward) — mesmo `useEffect` padrão de `minPrice`/`maxPrice` (linhas ~183-193).
- `q` entra em `RESTRICTIVE_KEYS` (linha 28) — conta para o badge/"Limpar filtros" e ganha pílula em
  `Pills()` (mostrando o termo digitado, com botão de remover que chama `commitSearch('')`
  imediatamente, sem esperar debounce).
- Acessibilidade: `aria-label="Buscar produtos"` no input, mesmo padrão dos demais controles do
  componente (`aria-label` explícito, não dependente de `<label>` associado via `htmlFor` — CSS não
  roda no jsdom dos testes, ver comentário existente linha ~680-684 sobre esse cuidado já adotado
  no componente).
- Grupo de busca aparece tanto no layout desktop (`filter-bar__row`) quanto no drawer mobile
  (`filter-bar__drawer-body`) — mesmo padrão de `priceGroup`/`groupCategory` (renderizado como
  valor JSX simples, não componente aninhado — ver comentário existente linhas ~449-458 sobre por
  que isso importa para não perder foco/pointer capture durante digitação).

### 5.4 `website/app/page.tsx`
- `HomePageProps.searchParams` ganha `q?: string`.
- `buildFilters` propaga `q: searchParams.q`.
- `buildPaginationQuery` propaga `q` (mesmo padrão dos demais filtros, linha ~33-42).
- Bloco de resultado ganha 3 estados (hoje só tem 2: com/sem `hasActiveFilters`, linhas ~69-82):
  1. **Normal** (comportamento atual, inalterado).
  2. **Resultados aproximados** (`result.isApproximateSearch === true`): banner/mensagem acima do
     grid, ex. `Resultados aproximados para "${searchParams.q}"` (texto exato a cargo do UX/UI se
     aplicável, ou razoável default do Dev se a issue não passar por UX/UI).
  3. **Vazio genuíno de busca** (`deals.length === 0 && searchParams.q` presente): mensagem
     distinta da atual "Nenhuma oferta encontrada com esses filtros" — ex.
     `Nenhum produto encontrado para "${searchParams.q}"`. **Não** reutilizar o texto/branch atual
     de `hasActiveFilters` sem diferenciar — CA 5.1 exige distinção visual clara entre os dois
     vazios.
- `hasActiveFilters` (linha 47-49) passa a incluir `q` na checagem, para não cair no branch de
  "Nenhuma oferta encontrada" genérico (sem filtro) quando há busca ativa sem resultado.

### 5.5 `website/app/loading.tsx` (novo arquivo)
Suspense fallback de rota (convenção de arquivo do App Router — sem plumbing manual). Cobre CA 2.2
(loading se resposta > tempo perceptível) para busca **e**, de forma incidental, para os demais
filtros já existentes (não há nenhum loading state hoje — gap que este arquivo fecha para toda a
tela `app/page.tsx`). Conteúdo mínimo: skeleton/spinner simples reaproveitando classes CSS
existentes se houver (`globals.css`/`styles/`) ou um esqueleto simples de grid (visual não é
crítico — a issue não passou pelo UX/UI; se o Gerente pedir refinamento visual depois, é aditivo).

### 5.6 Testes frontend
- `FilterBar.test.tsx`: digitar não navega a cada tecla (debounce); após debounce, `router.replace`
  com `q` setado; campo vazio remove `q`; pílula aparece/some corretamente; conta para "Limpar
  filtros".
- `page.test.tsx`: 3 estados de resultado (§5.4); sem `q`, nenhuma mudança de comportamento
  (não-regressão).
- Playwright e2e (`website/e2e/`, novo spec, ex. `search.spec.ts`, seguindo o padrão de
  `filter-bar-price.spec.ts`): fluxo com termo aproximado (banner), termo sem relação (vazio
  genuíno), limpar campo (volta ao padrão). Rodar via `npm run test:visual` (comando já
  documentado em `repos/omuletachou/CLAUDE.md`).

## 6. Não-regressão obrigatória

- `GET /api/public/deals` sem `q` → comportamento 100% idêntico ao atual (paginação, `sort`,
  filtros existentes, `IsApproximateSearch: null`).
- `FilterBar`/`page.tsx` sem `q` na URL → nenhuma mudança visual/funcional perceptível.
- Demais consumidores de `PagedResult<T>` (`ProductsController`, `QueueController`) não são
  afetados pelo campo aditivo `IsApproximateSearch` (default `null`, nunca setado por eles).

## 7. Fora de escopo (ver `design.md` §8 para a lista completa e justificativas)
Chamada à IA por requisição; busca fonética via `fuzzystrmatch` (`soundex`/`metaphone`); campo
`Subcategory` no escopo de busca; índice `gin_trgm_ops` para o estágio 2; autocomplete/typeahead.
