# Tasks — ISSUE-260: Busca textual inteligente (fonética/fuzzy) na tela de produtos do site público

> Devs leem este arquivo. Contexto técnico completo em `especificacao-tecnica.md` (docs_path) e
> `design.md` (openspec_path, mesma pasta deste tasks.md).

## T-01 (stack:dotnet) — Migration: `search_vector` + extensões + índice GIN

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.

### O que fazer
- `CREATE EXTENSION unaccent`/`pg_trgm`, função `immutable_unaccent`, coluna gerada
  `search_vector` (`tsvector`, `GENERATED ALWAYS ... STORED`, pesos A/B/C título>categoria>
  descrição), índice `IX_products_search_vector` (GIN) — SQL exato em
  `especificacao-tecnica.md` §1.
- Mapear `search_vector` como shadow property em `ProductConfiguration.cs` (não expor propriedade
  CLR pública em `Product`).
- Migration nova: `AddProductSearchVector`.

### Critérios de aceite (Given/When/Then)
- Given o projeto backend compilado, When `dotnet ef database update` roda sobre a base de
  desenvolvimento, Then a migration aplica sem erro (extensões, função, coluna gerada, índice).
- Given a migration aplicada, When se inspeciona o schema, Then `search_vector` existe como
  `tsvector` gerado (`STORED`) e o índice `IX_products_search_vector` existe como GIN.
- Given `dotnet ef migrations add` rodado novamente após esta migration, Then não gera diff
  espúrio a partir do mapeamento shadow property (model snapshot em sincronia com o schema real).

### Contexto técnico
- `especificacao-tecnica.md` §1 (docs_path).
- `design.md` §2.2, §2.4 (openspec_path) — por que `unaccent` precisa de wrapper `IMMUTABLE`, por
  que GIN.
- Arquivos: nova migration em `backend/src/AfiliadoBot.Infrastructure/Migrations/`,
  `backend/src/AfiliadoBot.Infrastructure/Data/Configurations/ProductConfiguration.cs`.
- Stack: EF Core 8.0, PostgreSQL 16 (`postgres:16.14-alpine`, já em uso).
- Repo: `repos/omuletachou`. Branch base: `desenv`.
- **Pré-requisito de T-02** (mesmo repo — LT funde sequencialmente, nunca dois merges de sub-issue
  em paralelo): T-02 depende desta migration existir para escrever e rodar os testes de busca.

---

## T-02 (stack:dotnet) — Endpoint `q` em `GET /api/public/deals` (busca 2 estágios)

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.
**Depende de T-01** (branch/PR de T-02 parte de `desenv` já com a migration de T-01 mergeada).

### O que fazer
- Novo `[FromQuery] string? q` em `PublicController.GetDeals`.
- Lógica de 2 estágios (full-text primeiro, trigram fallback só se estágio 1 vazio) — fluxo exato
  e opções de implementação LINQ/SQL raw em `especificacao-tecnica.md` §2-§3.
- `q` irrelevante (ausente/vazio/`< 2 chars` após `Trim()`) → comportamento 100% atual.
- `q` relevante → ignora `sort`, ordena por relevância (`ts_rank`/`combined_score` desc).
- `q` compõe com os demais filtros existentes via AND (mesma `IQueryable` base).
- `PagedResult<T>` ganha `IsApproximateSearch` (`bool?`, default `null`) — propagar em
  `ToDtoPagedResultAsync`.
- Constantes `SearchConstants.MinQueryLength` (2) e `ApproximateSimilarityThreshold` (0.15).
- Testes via Testcontainers (InMemory não suporta `tsvector`/`pg_trgm`) — ver
  `especificacao-tecnica.md` §4 para o padrão (precedente `ClaudeBudgetServiceIntegrationTests`) e
  as duas opções de fixture.

### Critérios de aceite (Given/When/Then)
- CA 1.2 (não-regressão): `q` ausente → resposta idêntica ao comportamento atual,
  `IsApproximateSearch == null`.
- CA E.1: `q` com 1 caractere → tratado como ausente, sem erro.
- CA 3.1: `q` com match só em `description` → produto aparece.
- CA 3.2/3.3: match em título rankeia antes de categoria, que rankeia antes de descrição; produto
  com match em múltiplos campos rankeia acima de produto com match em um só.
- CA 4.1/4.3: termo com erro de digitação sem match exato → estágio 2 aciona,
  `IsApproximateSearch == true`, resultados relevantes retornados.
- Termo com plural/singular/variação de acento (ex. "tenis"/"tênis") → resolvido pelo estágio 1
  (stemmer + `immutable_unaccent`), `IsApproximateSearch == false`.
- CA 5.1: termo sem nenhuma relação (abaixo do threshold 0.15 em tudo) → lista vazia,
  `IsApproximateSearch == false` (vazio genuíno).
- CA 6.1: `q` combinado com `category`/`minPrice`/`maxPrice` → interseção AND.
- `q` presente + `sort=price_asc` → ordenação por relevância prevalece, não por preço.
- CA 7.1: nenhuma chamada à API Anthropic/Claude disparada em nenhum cenário acima.

### Contexto técnico
- `especificacao-tecnica.md` §2, §3, §4 (docs_path).
- `design.md` §2.1-§2.6, §3, §5 (openspec_path).
- Arquivos: `backend/src/AfiliadoBot.Api/Controllers/PublicController.cs`,
  `backend/src/AfiliadoBot.Api/Common/PagedResult.cs`, nova classe de teste (ex.
  `backend/src/AfiliadoBot.Tests/Public/PublicSearchTests.cs` ou extensão de
  `PublicControllerTests.cs` — decisão do Dev conforme fixture escolhida em §4).
- Stack: ASP.NET Core 8.0, EF Core 8.0 + Npgsql provider, `Testcontainers.PostgreSql` (já usado no
  projeto).
- Repo: `repos/omuletachou`. Branch base: `desenv`.

---

## T-03 (stack:nodejs) — Campo de busca na `FilterBar` + estados de resultado

**Sub-issue:** criada no GitHub como sub-tarefa desta issue.
**Paralelizável com T-01/T-02** (o contrato do endpoint — parâmetro `q`, campo
`isApproximateSearch` — já está definido nesta especificação; a integração real só é validada em
Code Review/QA depois que o backend estiver mergeado, mas a implementação do frontend não precisa
esperar).

### O que fazer
- `website/lib/api.ts`: `DealFilters.q?: string`, propagado por `fetchDeals`.
- `website/lib/types.ts`: `PagedResult<T>.isApproximateSearch?: boolean | null`.
- `website/components/FilterBar.tsx`: novo input de busca (draft + debounce 350ms +
  `router.replace`, reaproveitando o mecanismo já usado para preço — ver
  `especificacao-tecnica.md` §5.3 linha a linha); `q` em `RESTRICTIVE_KEYS`; pílula em `Pills()`.
- `website/app/page.tsx`: `searchParams.q`, `buildFilters`/`buildPaginationQuery` propagam `q`;
  3 estados de resultado (normal / aproximado / vazio genuíno de busca) — ver
  `especificacao-tecnica.md` §5.4.
- Novo `website/app/loading.tsx` (Suspense fallback da rota).
- Testes: `FilterBar.test.tsx`, `page.test.tsx`, novo `website/e2e/search.spec.ts` (Playwright).

### Critérios de aceite (Given/When/Then)
- CA 1.1: campo de busca visível na `filter-bar`, sem substituir filtros existentes.
- CA 1.2: campo vazio → listagem normal, sem filtro de busca.
- CA 2.1: digitar filtra automaticamente, sem botão/Enter; não dispara requisição a cada tecla
  (debounce).
- CA 2.2: loading state visível se a resposta ultrapassar tempo perceptível (via
  `app/loading.tsx`).
- CA 4.2: `isApproximateSearch === true` → banner "resultados aproximados para 'X'" (ou
  equivalente), visualmente distinto do resultado normal.
- CA 5.1: `items.length === 0` com `q` presente → mensagem de vazio genuíno distinta da mensagem
  de "nenhuma oferta com esses filtros" já existente.
- CA 6.1: busca combina com filtros já ativos (comportamento herdado do backend — validar que o
  frontend não filtra client-side por cima).
- E.2: erro de rede/timeout na busca segue o padrão já existente (`app/error.tsx`) — sem página de
  erro genérica sem mensagem.
- Não-regressão: sem `q` na URL, nenhuma mudança visual/funcional perceptível.

### Contexto técnico
- `especificacao-tecnica.md` §5 (docs_path).
- `design.md` §3 (fluxo), §4 (componentes afetados), §5 (openspec_path).
- Arquivos: `website/components/FilterBar.tsx`, `website/components/FilterBar.test.tsx`,
  `website/app/page.tsx`, `website/app/page.test.tsx`, novo `website/app/loading.tsx`,
  `website/lib/api.ts`, `website/lib/types.ts`, novo `website/e2e/search.spec.ts`.
- Stack: Next.js 14+ (App Router), React 18, TypeScript, Jest + Testing Library, Playwright
  (`npm run test:visual`, comando documentado em `repos/omuletachou/CLAUDE.md`).
- Repo: `repos/omuletachou`. Branch base: `desenv`.
- Sem UX/UI nesta issue (PM não sinalizou necessidade de tela nova/fluxo complexo — campo reaproveita
  o padrão visual já existente da `filter-bar`; texto exato dos banners a critério do Dev, seguindo
  o tom já usado nos estados vazios existentes de `page.tsx`).

## Repo / branches
- Repo: `repos/omuletachou`. Branch base: `desenv`.
- `feature/ISSUE-<NNN>-descricao` onde NNN = número da sub-issue (T-01, T-02, T-03).
- Ordem sugerida: T-01 → T-02 (dependência real, mesmo repo, LT funde sequencialmente) → T-03 pode
  rodar em paralelo desde o início (contrato já fechado nesta especificação).
