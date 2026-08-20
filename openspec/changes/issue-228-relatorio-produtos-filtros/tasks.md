# Tasks — ISSUE-228: Relatório de produtos com filtros na tela Reports

> Devs leem este arquivo. Contexto técnico completo em `especificacao-tecnica.md` (docs_path) e
> `design.md` (openspec_path, mesma pasta deste tasks.md).

## T-01 (stack:dotnet) — Índice composto + migration

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.

**O que fazer:**
- Adicionar o índice `IX_products_status_platform_createdat` (`Status`, `Platform`, `CreatedAt` desc)
  em `ProductConfiguration.cs` (`backend/src/AfiliadoBot.Infrastructure/Data/Configurations/ProductConfiguration.cs`).
- Gerar migration EF Core nova (`AddStatusPlatformCreatedAtIndex`).
- Aplicar a migration localmente e confirmar que sobe sem erro sobre a base atual.

**Critérios de aceite (Given/When/Then):**
- Given o projeto backend compilado, When `dotnet ef database update` roda sobre a base de
  desenvolvimento, Then o índice `IX_products_status_platform_createdat` é criado sem erro (índice
  não é `UNIQUE` — sem risco de falha por duplicidade).
- Given a migration aplicada, When se consulta o schema do Postgres, Then o índice existe com as 3
  colunas na ordem `Status, Platform, CreatedAt` (CreatedAt descendente).

**Contexto técnico:**
- `especificacao-tecnica.md` §1 (docs_path).
- `design.md` §2.2 (openspec_path) — por que este índice e não outro.
- Stack: ASP.NET Core 8.0 / EF Core 8.0 / PostgreSQL 16.
- Repo: `repos/omuletachou`. Branch base: `desenv`.

---

## T-02 (stack:dotnet) — Endpoint `GET /api/reports/products/summary`

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.

**O que fazer:**
- Novo action `ProductsSummary` em `ReportsController.cs` (`backend/src/AfiliadoBot.Api/Controllers/ReportsController.cs`).
- Novo(s) DTO(s) de resposta (`ProductsReportSummaryDto` + records de breakdown).
- Filtros: `category`, `subcategory`, `platform`, `status`, `collectedFrom`, `collectedTo` (todos
  opcionais, ver regras em `especificacao-tecnica.md` §2).
- Testes em `ReportsControllerTests` cobrindo os cenários do design.md §5.

**Critérios de aceite (Given/When/Then):**
- Given produtos `Published` existentes, When `GET /api/reports/products/summary?status=Published`
  é chamado, Then a resposta traz `total` correto e os 4 breakdowns (`byPlatform`, `byCategory`,
  `byStatus`, `bySubcategory`) com as contagens corretas (CA 1.1).
- Given `status=Pending` explícito, When o endpoint é chamado, Then a contagem reflete produtos
  `Pending`, não restrita a `Published` (CA 2.4).
- Given filtros combinados `platform=MercadoLivre&category=Eletrônicos&collectedFrom=...&collectedTo=...`,
  When o endpoint é chamado, Then o resultado é a interseção (AND) dos filtros (CA 2.6).
- Given uma combinação de filtros sem nenhum produto correspondente, When o endpoint é chamado,
  Then a resposta é `200 OK` com `total: 0` e as 4 listas de breakdown vazias, sem erro (CA 1.3/2.7).
- Given `collectedFrom`/`collectedTo` definidos, When o endpoint é chamado, Then produtos com
  `CreatedAt` exatamente na data limite (início e fim) são incluídos (CA 2.5, janela inclusiva).
- Given o endpoint sem token de autenticação, When chamado, Then retorna `401` (`[Authorize]`).

**Contexto técnico:**
- `especificacao-tecnica.md` §2 e §5 (docs_path).
- `design.md` §2.1, §2.3, §2.4, §3, §5 (openspec_path).
- Repo: `repos/omuletachou`. Branch base: `desenv`.

---

## T-03 (stack:dotnet) — Extensão de `GET /api/products` (filtros + `Subcategory` no DTO)

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.

**O que fazer:**
- `ProductsController.GetProducts` (`backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs`)
  ganha 4 novos `[FromQuery]` opcionais: `category`, `subcategory`, `collectedFrom`, `collectedTo`.
- `ProductListItemDto` (`backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`) ganha campo aditivo
  `Subcategory` (`string?`) ao final do record.
- Testes em `ProductsControllerTests` cobrindo os cenários do design.md §5, incluindo
  **não-regressão** de `GetProducts` sem os novos params.

**Critérios de aceite (Given/When/Then):**
- Given produtos de categorias/subcategorias diferentes, When `GET /api/products?category=X` (ou
  `?subcategory=Y`) é chamado, Then só produtos daquela categoria/subcategoria retornam (CA 2.1/2.2).
- Given `collectedFrom`/`collectedTo`, When o endpoint é chamado, Then só produtos com `CreatedAt`
  dentro da faixa (inclusive nos limites) retornam (CA 2.5).
- Given todos os filtros combinados (`status`, `platform`, `category`, `subcategory`,
  `collectedFrom`, `collectedTo`), When o endpoint é chamado, Then o resultado é a interseção AND
  (CA 2.6).
- Given a resposta de qualquer chamada a `GetProducts`, When o payload é inspecionado, Then cada
  item traz o campo `Subcategory` (pode ser `null`).
- Given uma chamada a `GET /api/products` **sem** os 4 novos params, When comparada ao
  comportamento atual (pré-issue), Then o resultado é idêntico (não-regressão de `ProductsComponent`,
  que continua sem filtro de status = todos os status).

**Contexto técnico:**
- `especificacao-tecnica.md` §3 e §5 (docs_path).
- `design.md` §2.1, §2.3, §2.4, §5 (openspec_path).
- Repo: `repos/omuletachou`. Branch base: `desenv`.

---

## T-04 (stack:angular) — Filtros + cards agregados + tabela/gráfico na tela Reports

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.
**UX/UI:** o agente UX/UI atua antes desta sub-issue, definindo o layout dos filtros/cards/tabela-gráfico
(ver `design.md` "Contrato de componentes globais" — extensão do `ReportsComponent` existente, sem
Layout/rota novos). Este dev implementa a partir da entrega do UX/UI + contrato de dados abaixo.

**O que fazer:**
- `reports.service.ts`: novo método `productsSummary(filters)` + interface `ProductsReportSummary`
  + interface `ProductsReportFilters`.
- `products.service.ts`: `ProductsListParams` ganha `category?`, `subcategory?`, `collectedFrom?`,
  `collectedTo?`; `ProductListItem` ganha `subcategory?: string | null`.
- `reports.component.ts`/`.html`: novo `filterForm` (6 campos), default `status=Published` quando
  vazio, `forkJoin` para cards+página 1 ao aplicar/mudar filtro, só `list()` ao trocar de página,
  "limpar filtros", estado vazio, tratamento de erro — ver `especificacao-tecnica.md` §4 para o
  contrato completo.
- Cards/gráfico existentes ("Hoje/Semana/Mês", "Publicações por rede") permanecem inalterados,
  novo bloco adicionado abaixo no mesmo componente.
- Testes em `reports.component.spec.ts` cobrindo os cenários do design.md §5.

**Critérios de aceite (Given/When/Then):**
- Given a tela `Reports` carregando sem filtro, When os dados chegam, Then os cards de resumo (no
  mínimo total + por Plataforma + por Categoria) e a tabela/gráfico detalhado exibem os produtos
  `Published` (CA 1.1) — e os cards/gráfico existentes continuam exibidos normalmente (CA 1.2).
- Given nenhum produto `Published`, When a tela carrega, Then os cards mostram zero e a tabela/
  gráfico mostra estado vazio, sem erro (CA 1.3).
- Given o operador aplica um filtro (qualquer um dos 5: Categoria, Subcategoria, Plataforma,
  Status, Faixa de data), When o filtro é aplicado, Then cards e tabela/gráfico recalculam
  on-demand refletindo o filtro (CA 2.1–2.5).
- Given múltiplos filtros aplicados simultaneamente, When o relatório recalcula, Then reflete a
  interseção AND, não união (CA 2.6).
- Given uma combinação sem resultados, When o relatório recalcula, Then cards mostram zero e
  tabela/gráfico mostra estado vazio, sem dado remanescente da consulta anterior (CA 2.7).
- Given filtros aplicados, When o operador limpa os filtros, Then o relatório volta ao universo
  completo `Published` (CA 2.8).
- Given um filtro já aplicado, When o operador troca o valor sem recarregar a página, Then o
  relatório recalcula automaticamente (CA 2.9).
- Given a tela Reports, When o operador procura opção de exportar/imprimir o novo relatório, Then
  essa opção não existe (CA 4.1).
- Given um filtro aplicado, When a chamada ao backend falha (erro de rede/timeout/5xx), Then a
  tela indica erro claro, sem manter dado antigo como se fosse atual, permitindo nova tentativa
  (CA 5.1).

**Contexto técnico:**
- `especificacao-tecnica.md` §4 (docs_path).
- `design.md` §2.1, §3, §4 ("Componentes afetados" e "Contrato de componentes globais"), §5
  (openspec_path).
- Stack: Angular 17+, Angular Material, ng2-charts (já usado em `reports.component.ts`), Reactive
  Forms.
- Repo: `repos/omuletachou`. Branch base: `desenv`.
