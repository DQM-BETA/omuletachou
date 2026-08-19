# Design — ISSUE-228: Relatório de produtos com filtros na tela Reports

## 1. Visão geral

O relatório pede duas visões do mesmo universo filtrado — `products` com `Status = Published`
por padrão, refinável por Categoria/Subcategoria/Plataforma/Status/Faixa de data de coleta: (a)
**cards agregados** (contagens) e (b) **tabela/gráfico detalhado** (linhas). As duas visões
compartilham o mesmo conjunto de filtros mas têm formas de dado e ciclos de recálculo diferentes —
os cards são um resumo fixo (poucas linhas agregadas), a tabela é paginada (pode ter muitas
linhas, e o operador pagina dentro dela sem reaplicar filtro).

Essa assimetria é o eixo da decisão central deste design: **dois endpoints, não um** — um novo
endpoint de agregação dedicado para os cards, e a **extensão aditiva** do `GET /api/products` já
existente (`ProductsController`) para a tabela/gráfico detalhado. Nenhuma tabela nova, nenhuma
camada de cache/materialização — agregação calculada diretamente em SQL/EF Core a cada request,
com um índice composto novo cobrindo a combinação Plataforma + Status + data de coleta que os
índices atuais (orientados a Categoria, Issue #167) não cobrem.

`CreatedAt` em `Product` já **é** a data de coleta (setada uma única vez no construtor, quando o
collector insere o produto; `UpdateFromCollector`, chamado em re-coletas/upsert, não a toca — só
atualiza `UpdatedAt`). Não há ambiguidade de schema aqui: o filtro de "faixa de data de coleta" é
literalmente um filtro de faixa sobre `CreatedAt`, coluna `timestamptz` já `NOT NULL`. Nenhuma
migration de tipo é necessária — só o índice novo (§2.2).

## 2. Decisões técnicas

### 2.1 Contrato dos endpoints (ambiguidade 1 do PM)

**Decisão: dois endpoints, reaproveitando `GET /api/products` para o detalhe.**

- **Cards (novo):** `GET /api/reports/products/summary` em `ReportsController` — recebe os
  mesmos filtros (`category`, `subcategory`, `platform`, `status`, `collectedFrom`,
  `collectedTo`) e devolve só agregados (total + quebras por dimensão), sem paginação.
- **Detalhe (extensão aditiva):** `GET /api/products` em `ProductsController` ganha os 4 novos
  query params `category`, `subcategory`, `collectedFrom`, `collectedTo`, somados aos já
  existentes `status`/`platform`/`page`/`pageSize`. Resposta continua `PagedResult<ProductListItemDto>`
  — `ProductListItemDto` ganha `Subcategory` como campo aditivo ao final (mesmo padrão já usado
  para `SourceUrl`/`Destinations`, Issue #184/#208), necessário porque a tabela detalhada precisa
  exibir a Subcategoria quando o operador filtra por ela.

**Por que não um único endpoint combinado (cards + detalhe no mesmo payload):** rejeitado por
acoplar o recálculo dos agregados a cada troca de página da tabela. O caso de uso do Cenário 2.9
(paginar dentro do resultado sem reaplicar filtro — implícito no comportamento esperado de uma
tabela paginada) faria o backend recalcular `COUNT`/`GROUP BY` de todas as dimensões a cada
`page++`, sem necessidade (o filtro não mudou). Um payload misto (lista paginada + agregados fixos)
também é uma forma de resposta inconsistente com o padrão já usado no projeto (`PagedResult<T>`
genérico, reaproveitado por `ProductsController`/`QueueController`).

**Por que não dois endpoints novos (i.e., também um `GET /api/reports/products` dedicado para o
detalhe, ignorando `GET /api/products`):** rejeitado por duplicar paginação/DTO/ordenação que já
existem e já são testados em `ProductsController.GetProducts` — mesma entidade (`Product`), mesma
forma de filtro (`status`/`platform` já existem lá), diferindo apenas em 4 parâmetros aditivos e
no *default* de `status` quando nenhum filtro é passado (ver §2.3). Estender é a menor mudança que
atende o requisito (mesmo princípio já aplicado no design da Issue #208, §"não introduzir campo
novo quando o existente já serve").

**Trade-off aceito:** 2 requisições HTTP por aplicação de filtro (cards + página 1 do detalhe,
disparadas em paralelo via `forkJoin`, mesmo padrão já usado em `reports.component.ts` para
`totals()`+`summary()`) em vez de 1. Aceitável porque é on-demand por interação humana (não há
polling), a latência de 2 chamadas paralelas é imperceptível frente ao ganho de não recalcular
agregados a cada troca de página.

### 2.2 Performance de agregação com filtros combinados (ambiguidade 2 do PM)

**Decisão: query direta (EF Core `GroupBy`/`CountAsync`) a cada request, sem cache/materialização,
mais um índice composto novo.**

Índices atuais em `products` (Issue #167, `ProductConfiguration.cs`) são todos **orientados a
Categoria**, com `Status` líder:
```
IX_products_status_aiscore                              (Status, AiScore)
IX_products_status_category_subcategory_aiscore          (Status, Category, Subcategory, AiScore)
IX_products_status_category_subcategory_saleprice        (Status, Category, Subcategory, SalePrice)
IX_products_status_category_subcategory_discountpct      (Status, Category, Subcategory, DiscountPct)
IX_products_status_category_subcategory_createdat        (Status, Category, Subcategory, CreatedAt)
```
`IX_products_status_category_subcategory_createdat` já cobre bem o caso "filtro por
Categoria/Subcategoria + faixa de data" (Cenário 2.6 parcialmente) e a ordenação padrão da tabela
detalhada (`ORDER BY CreatedAt DESC`, mesma ordenação já usada em `GetProducts`). Mas **nenhum
índice atual tem `Platform`** — o Cenário 2.3 (filtro só por Plataforma) e o 2.6 (Plataforma +
Categoria + data) caem em varredura filtrada sobre `IX_products_status_aiscore` (só `Status`) ou
sequential scan.

Novo índice, mesmo padrão dos 4 já existentes (Status líder, por ser o predicado mais comum —
default do relatório é `Published`):
```csharp
builder.HasIndex(x => new { x.Status, x.Platform, x.CreatedAt })
    .HasDatabaseName("IX_products_status_platform_createdat")
    .IsDescending(false, false, true);
```
Cobre: filtro só por Plataforma (Cenário 2.3), filtro Plataforma + faixa de data, e a quebra "por
Plataforma" dos cards (`GROUP BY Platform` com `Status` no `WHERE`, prefixo do índice serve para o
scan). Combinado com os 4 índices existentes, o planner do Postgres tem cobertura para os dois
eixos mais comuns (Categoria-orientado e Plataforma-orientado); quando os dois filtros vêm juntos
(Plataforma + Categoria + data, Cenário 2.6) o planner escolhe bitmap scan em um dos dois índices
compostos e filtra o restante em memória — aceitável no volume atual (catálogo de produtos
coletados por 3 collectors, escala de dashboard interno, não o caminho público de maior tráfego).

**Por que não um índice composto único cobrindo as 5 dimensões
(`Status, Category, Subcategory, Platform, CreatedAt`):** rejeitado por especulativo — nenhum
requisito de SLA foi dado (proposal: "sem requisito de SLA específico"), e um índice de 5 colunas
tem custo de escrita (todo `INSERT`/`UPDATE` de `products` mantém 6 índices compostos + 2 únicos)
sem benefício comprovado sobre a combinação de 2 índices de 3 colunas já cobrindo os pares mais
prováveis. Fica como risco monitorado (§6) — se o LT/QA medir degradação real em homologação com
os filtros combinados nos 3 no mesmo predicado, adicionar então (YAGNI: não antecipar).

**Por que não cache/materialização (view materializada, cache em memória por combinação de
filtros):** rejeitado por dois motivos. (1) Conflita com o requisito de negócio já confirmado
("recalcula ao aplicar filtro" — Gate 1, PM) — uma `MATERIALIZED VIEW` exigiria `REFRESH`
(síncrono no request, anulando o ganho, ou assíncrono, reintroduzindo staleness que o Gerente
explicitamente não pediu). (2) O espaço de combinações de filtro é grande (5 dimensões
combináveis em AND, incluindo faixas de data livres) — um cache por combinação teria taxa de
acerto baixa (cada filtro novo é uma chave nova), sem ganho real para justificar a complexidade
operacional (invalidação a cada novo produto coletado/publicado). Reavaliar só se medição real em
produção mostrar tempo de resposta inaceitável (não antecipar).

### 2.3 Default de Status = Published só no relatório, não no endpoint (ambiguidade 3 do PM avaliada
junto)

`GetProducts` hoje, sem filtro de `status`, retorna produtos de **todos** os status (uso
operacional de gestão/aprovação — `ProductsComponent`). O relatório precisa que a ausência de
filtro de Status signifique `Published` (CA 1.1/2.4: "o padrão sem filtro é Published, mas o
filtro de Status permite consultar outros estados"). **Decisão: esse default é responsabilidade do
Angular (`reports.component.ts`), não do backend** — o dashboard sempre envia `status=Published`
por padrão ao montar a query string do relatório, e troca para o valor selecionado quando o
operador escolhe outro Status no filtro. Isso evita ramificar o comportamento de `GetProducts` por
"quem está chamando" (a tela `Products` continua sem filtro de status = todos, comportamento
inalterado, CA implícito de não regressão) — o contrato do endpoint não muda, só o cliente que o
consome tem um default de UX diferente. Mesmo default se aplica ao novo endpoint de summary.

### 2.4 Formato de data de coleta (ambiguidade 4 do PM)

Já respondido em §1: `CreatedAt` (`timestamptz`, `NOT NULL`) já é a data de coleta. `collectedFrom`/
`collectedTo` chegam como `date` (`yyyy-MM-dd`) na query string — convertidos no controller para
`DateTime` UTC no início do dia (`from`) e início do dia seguinte exclusivo (`to.AddDays(1)`),
mesmo padrão de janela `[from, toExclusive)` já usado em `ReportsController.Totals`/`Summary`
(`periodStart`/`periodEndExclusive`). Cenário 2.5 exige inclusão dos limites — a janela
`>= from AND < toExclusive` inclui o dia final inteiro, atendendo ao "inclusive nos limites"
tratando data (sem hora) como o dia completo.

## 3. Fluxo de dados (resumo)

```
Angular ReportsComponent
  ├─ filterForm (category, subcategory, platform, status, collectedFrom, collectedTo)
  ├─ on init / on filtro aplicado (default status='Published' se vazio):
  │    forkJoin([
  │      reportsService.productsSummary(filters)         → GET /api/reports/products/summary
  │      productsService.list({ ...filters, page: 1 })   → GET /api/products (+ novos params)
  │    ])
  └─ on troca de página da tabela (mesmo filtro):
       productsService.list({ ...filters, page: N })     → só GET /api/products

Backend
  ReportsController.ProductsSummary(filtros)
    └─ base query _db.Products.Where(filtros) (Status default Published vindo do Angular)
         ├─ CountAsync()                                  → total
         ├─ GroupBy(Platform).Select(count)                → byPlatform
         ├─ GroupBy(Category).Select(count)                 → byCategory
         ├─ GroupBy(Status).Select(count)                    → byStatus   (aditivo, barato)
         └─ GroupBy(Subcategory).Select(count)                → bySubcategory (aditivo, barato)

  ProductsController.GetProducts(status, platform, category, subcategory, collectedFrom, collectedTo, page, pageSize)
    └─ mesma query base + 4 filtros novos (Where aditivo) → ToPagedResultAsync (inalterado)
```

Os 4 `GroupBy` do summary reaproveitam a **mesma `IQueryable` filtrada** (base comum), cada um
materializado com sua própria `ToListAsync` — 5 queries curtas (1 `Count` + 4 `GroupBy`) no mesmo
request, todas seek-friendly pelos índices de §2.2 porque `Status` (e opcionalmente
`Category`/`Platform`) sempre lidera o `WHERE`.

## 4. Componentes afetados

| Componente | Mudança | Escopo |
|---|---|---|
| `AfiliadoBot.Infrastructure.Data.Configurations.ProductConfiguration` | Novo índice `IX_products_status_platform_createdat` (Status, Platform, CreatedAt desc) | Backend |
| Migration EF Core nova (`AddStatusPlatformCreatedAtIndex` ou similar) | `CREATE INDEX` do índice acima | Backend |
| `AfiliadoBot.Api.Controllers.ReportsController` | Novo endpoint `GET /api/reports/products/summary` (total + byPlatform + byCategory + byStatus + bySubcategory) | Backend |
| `AfiliadoBot.Api.Controllers.ProductsController.GetProducts` | +4 query params aditivos: `category`, `subcategory`, `collectedFrom`, `collectedTo` | Backend |
| `AfiliadoBot.Api.Products.ProductDtos` (`ProductListItemDto`) | Novo campo aditivo `Subcategory` (ao final, mesmo padrão de `SourceUrl`/`Destinations`) | Backend |
| Novo DTO `ProductsReportSummaryDto` (+ registros de breakdown) em `ReportsController`/pasta `Reports` da API | Resposta do novo endpoint | Backend |
| `dashboard/.../core/services/reports.service.ts` | Novo método `productsSummary(filters)`; interface `ProductsReportSummary` | Frontend |
| `dashboard/.../core/services/products.service.ts` | `list()` ganha os 4 novos params opcionais; `ProductListItem` ganha `subcategory?` | Frontend |
| `dashboard/.../pages/reports/reports.component.ts` | Novo `filterForm` (category/subcategory/platform/status/collectedFrom/collectedTo, default status vazio → enviado como `Published`), cards agregados + tabela/gráfico paginado, `forkJoin` como já usado hoje | Frontend |
| `dashboard/.../pages/reports/reports.component.html` | Novo bloco de filtros + cards + tabela/gráfico, abaixo/ao lado dos cards "Hoje/Semana/Mês" e gráfico "Publicações por rede" existentes (sem removê-los, restrição do proposal) | Frontend |
| Testes (`ReportsControllerTests`, `ProductsControllerTests`, `reports.component.spec.ts`) | Cobrir cenários de filtro combinado, estado vazio, erro de rede (§5) | Backend/Frontend |

## Contrato de componentes globais

| Componente | Renderiza em | NÃO renderiza em |
|---|---|---|
| Layout (Header + Sidenav) | `dashboard/src/app/layout/` (já existente, inalterado) | `ReportsComponent` |
| Cards "Hoje/Semana/Mês" + gráfico "Publicações por rede" (existentes) | `reports.component.html`, topo da tela | — |
| Novo bloco "Relatório de produtos publicados" (filtros + cards agregados + tabela/gráfico) | `reports.component.html`, abaixo do bloco existente, mesmo componente | Não é uma tela/rota nova — não recebe `<router-outlet>` próprio, não duplica o Layout |

Não há Layout/Header/Providers novos nesta issue — é uma extensão de conteúdo dentro de
`ReportsComponent`, componente já montado sob o Layout existente do dashboard.

## 5. Casos de teste a cobrir (mapeamento para os critérios de aceite)

- `ReportsControllerTests`: `GET /api/reports/products/summary` sem filtro (status=Published
  explícito do cliente) retorna total + breakdowns corretos (CA 1.1); com `status=Pending` retorna
  contagem daquele status, não restrito a Published (CA 2.4); combinação Platform+Category+data
  retorna interseção (CA 2.6); combinação sem match retorna `total=0` e listas de breakdown vazias,
  sem erro (CA 2.7/1.3).
- `ProductsControllerTests`: `GET /api/products` com os 4 novos params filtra corretamente
  (Category, Subcategory, faixa de CreatedAt inclusive nos limites — CA 2.1/2.2/2.5); combinação
  AND completa (CA 2.6); resposta inclui `Subcategory` no DTO; não filtrar por `category`/
  `subcategory`/`collectedFrom`/`collectedTo` mantém comportamento atual inalterado (não-regressão
  de `ProductsComponent`).
- `reports.component.spec.ts`: aplicar filtro dispara as duas chamadas (`forkJoin`) com o mesmo
  conjunto de params, inclusive `status=Published` quando o campo Status do form está vazio (CA
  1.1/2.4); trocar filtro sem reload atualiza cards+tabela (CA 2.9); limpar filtros volta ao
  universo completo (CA 2.8); erro de rede em qualquer uma das duas chamadas exibe mensagem de
  falha sem manter dado antigo na tela (CA 5.1); estado vazio nos cards/tabela sem erro (CA 2.7/1.3).
- Migration: teste de smoke/integração (se existente no projeto para migrations) ou validação
  manual de `dotnet ef database update` aplicando o índice novo sem erro sobre a base de
  homologação — não deve haver dado inconsistente que impeça a criação do índice (índice não é
  `UNIQUE`, então não há risco de falha por duplicidade).

## 6. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Filtros combinando Categoria/Subcategoria E Plataforma simultaneamente (Cenário 2.6 completo) não têm um único índice cobrindo as 5 colunas — planner pode fazer bitmap scan de 2 índices + filtro residual | Aceito por ora (§2.2, YAGNI); QA deve incluir esse cenário combinado no teste de carga informal; se LT/QA medir lentidão perceptível em homologação, adicionar índice composto de 5 colunas como follow-up, não bloquear esta issue por otimização especulativa |
| 5 queries (`Count` + 4 `GroupBy`) no endpoint de summary, uma a mais que o padrão usual (1-2 queries por endpoint no projeto) | Todas leves (agregação de `COUNT`, não fetch de linhas completas) e compartilham a mesma `IQueryable` base filtrada; medir apenas se profiling real mostrar custo, não preventivamente |
| Novo índice `IX_products_status_platform_createdat` adiciona overhead de escrita em todo INSERT/UPDATE de `products` (6º índice composto da tabela) | Aceitável — `products` já sofre updates frequentes dos jobs (Processor/Collectors) com 5 índices compostos hoje; overhead marginal de mais 1, sem escala de milhões de linhas reportada no projeto |
| Duplicação de lógica de "janela de data inclusiva" entre `ReportsController.Totals/Summary` (já existente) e o novo `ProductsSummary`/`GetProducts` (data de coleta) | Considerar extrair um helper compartilhado (`DateRangeExtensions.ToInclusiveUtcRange(from, to)`) na Application/Api Common — não bloqueante, LT decide se vale a pena nesta issue ou fica para follow-up |
| Default `status=Published` implementado só no Angular (não no backend) — se um consumidor futuro da API chamar `GET /api/products`/`GET /api/reports/products/summary` sem esse default, verá "todos os status" em vez de "publicados" | Documentado explicitamente em XML doc do controller; aceitável porque hoje o único consumidor é o próprio dashboard (mesmo padrão de "contrato mínimo, default de UX no cliente" já usado no restante do projeto) |

## 7. Dependências

- Nenhuma dependência externa nova.
- Depende de `Product.Category`/`Subcategory` (Issue #167) e `Product.Status = Published` (Issue
  #208) como já definidos — reaproveitados, não alterados.
- Depende de `PagedResult<T>`/`PaginationExtensions` (Issue #11) — reaproveitados sem mudança de
  contrato.

## 8. Fora de escopo (confirmado no proposal)

- Exportação/impressão do relatório (Cenário 4.1, restrição do Gerente).
- Atualização em tempo real/polling/websocket (Cenário 3.1, requisito de negócio já confirmado).
- Filtro de faixa de desconto (fora do escopo v1, confirmado no Gate 1).
- Índice composto de 5 colunas cobrindo todas as dimensões simultâneas — fica como follow-up
  condicionado a medição real (§6).
