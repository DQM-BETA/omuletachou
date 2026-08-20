# Design — ISSUE-260: Busca textual inteligente (fonética/fuzzy) na tela de produtos do site público

## 1. Visão geral

A busca precisa cobrir dois modos de match com prioridades diferentes: (a) **match "de verdade"**
(exato ou por prefixo de palavra, em título/categoria/descrição, com título pesando mais no
ranking) e (b) **match aproximado** (typo, plural/singular, variação de escrita/acento), só usado
quando (a) não encontra nada. Isso não é uma única técnica — é **duas técnicas do Postgres em
dois estágios sequenciais** (nunca uma chamada de IA, restrição vinculante):

- **Estágio 1 — Full-text search** (`tsvector`/`tsquery`, dicionário `portuguese`) sobre um
  campo gerado que combina título/categoria/descrição com pesos `A`/`B`/`C` (`setweight`). Cobre
  o CU2 (match exato/substring com título priorizado, CA 3.2/3.3) e boa parte do CU4 (plural/
  singular e variação de acento, já tratados nativamente pelo stemmer + `unaccent` do dicionário
  `portuguese`) — **de graça**, sem precisar de fuzzy matching para esses casos.
- **Estágio 2 — Trigram fallback** (`pg_trgm`, função `similarity()`) só é executado quando o
  Estágio 1 devolve **zero** resultados. Cobre o CU3 (erro de digitação: letra trocada/faltando/
  sobrando) que a busca lexical do Estágio 1 não resolve (um `tsquery` não acha "blutooth" dentro
  de um documento que só tem o lexema "bluetooth").

Essa é a decisão central do design (detalhada em §2.1): **não** é "`pg_trgm` OU full-text", é
"full-text primeiro, `pg_trgm` como rede de segurança", porque as duas técnicas resolvem classes de
erro diferentes e o requisito de negócio (regra 5: nunca lista vazia só por falta de match exato)
mapeia 1:1 para "estágio 1 vazio → tenta estágio 2 antes de declarar vazio genuíno".

Sem tabela nova, sem cache/materialização, sem chamada externa. Mudança contida em:
`Product`/`ProductConfiguration` (coluna gerada + índice, backend), `PublicController.GetDeals`
(extensão aditiva do endpoint já existente, mesmo padrão da Issue #167/#230), e `FilterBar`/
`lib/api.ts`/`app/page.tsx` no `website/` (campo novo + debounce + banner de "resultado
aproximado"/"nenhum resultado").

## 2. Decisões técnicas

### 2.1 Full-text (tier 1) + trigram fallback (tier 2) — por que não uma técnica só

**Por que não `pg_trgm` sozinho (comparar `q` contra os 3 campos por similaridade em toda busca):**
rejeitado porque `similarity()` compara **strings inteiras** por trigramas — é ótimo pra "uma
palavra com erro de digitação", mas ruim pra frases/múltiplas palavras (a métrica de trigramas do
texto inteiro dilui a similaridade quando a `description` é longa e só uma palavra bate) e não
tem noção de "prefixo de palavra" (essencial pro caso de uso 1: busca em tempo real enquanto o
usuário ainda está digitando a palavra). Usar só trigram como técnica única também jogaria fora o
ranking natural por peso de campo que o `tsvector`/`ts_rank` já dá de graça (§2.2).

**Por que não full-text sozinho (`tsvector`/`tsquery`) sem fallback:** rejeitado porque
`to_tsquery`/`plainto_tsquery` fazem match **lexical** (mesmo lexema/prefixo do lexema) — não
toleram troca/falta de letra dentro da palavra. "Fone Bluetoth" (erro de digitação) nunca vai
casar com o lexema `bluetooth` num `tsquery`. Isso violaria a regra de negócio 5/CA 4.1
diretamente (busca com erro de digitação teria que voltar vazia).

**Por que estágio 2 só roda quando estágio 1 devolve zero (não em paralelo/sempre):** rodar as
duas buscas sempre e mesclar rankings (ex.: score combinado ponderado) foi cogitado e rejeitado —
geraria uma heurística de mescla (como normalizar `ts_rank` [tipicamente 0-1, sem teto fixo] contra
`similarity()` [0-1] numa única ordenação sem embaralhar resultados exatos com aproximados) sem
necessidade real: a regra de negócio já pede uma **hierarquia** clara (exato > aproximado, nunca
misturados — CA 4.2 exige que o usuário saiba distinguir os dois), não uma mescla. Dois estágios
sequenciais com fallback condicional entregam essa hierarquia sem heurística de mescla, mais barato
(estágio 2 só roda na minoria dos casos — normalmente o termo tem *algum* match exato ou por
prefixo) e mais simples de explicar/testar (dois caminhos determinísticos, não um score composto
obscuro).

### 2.2 Peso por campo e ranking (título > categoria > descrição — CA 3.2/3.3)

**Estágio 1:** coluna gerada `search_vector` (`tsvector`, `GENERATED ALWAYS ... STORED`) combinando
os 3 campos com `setweight`:
```sql
setweight(to_tsvector('portuguese', immutable_unaccent(title)),       'A') ||
setweight(to_tsvector('portuguese', immutable_unaccent(category)),    'B') ||
setweight(to_tsvector('portuguese', immutable_unaccent(description)), 'C')
```
`ts_rank(search_vector, tsquery)` usa os pesos padrão do Postgres para os rótulos `{A=1.0, B=0.4,
C=0.2, D=0.1}` — título pesa 2,5x mais que categoria e 5x mais que descrição automaticamente, sem
lógica extra no backend. Um produto com match em mais de um campo soma contribuição de cada
ocorrência (CA 3.3, "podendo haver combinação/score") — comportamento nativo do `ts_rank`, não
precisa de código adicional.

**Estágio 2 (fallback):** sem `tsvector`, o score é calculado direto por campo e combinado com os
mesmos pesos relativos (normalizados para somar 1, título ainda dominante):
```
combined_score = 0.60 * similarity(title, q)
               + 0.25 * similarity(category, q)
               + 0.15 * similarity(description, q)
```
Incluído na consulta somente quando `GREATEST(similarity(title,q), similarity(category,q),
similarity(description,q)) >= @threshold` (§2.3) — evita que a soma ponderada de 3 matches
mediocre "escale" um produto irrelevante pra cima só por bater um pouco em tudo.

**Escopo de campos — decisão explícita de excluir `Subcategory`:** a regra de negócio 2 (Gate 1,
confirmada pelo PM) lista literalmente "título, categoria e descrição" como os 3 campos buscados —
`Subcategory` (nullable, Issue #167) fica de fora por decisão deliberada de respeitar o escopo
confirmado, não por esquecimento. Se o Gerente pedir depois, é aditivo trivial (mais um `setweight`
peso `B`, mesma técnica) — não bloqueante aqui.

### 2.3 Threshold de similaridade e "vazio genuíno" (CA 4.1/4.3/5.1)

`APPROXIMATE_SIMILARITY_THRESHOLD = 0.15` (escala 0-1 do `pg_trgm`, bem abaixo do default de 0.3
usado pelo operador `%` do Postgres) — deliberadamente permissivo, priorizando a meta qualitativa
do negócio ("cobrir o máximo de casos possível", regra 6) sobre precisão. Efeito colateral aceito:
alguns resultados do estágio 2 podem ser fracamente relacionados — mitigado por (a) só entrar em
jogo quando o estágio 1 não achou nada (b) a UI sinalizar claramente "resultados aproximados" (CA
4.2), então o usuário nunca confunde um match fraco com um exato. Constante isolada e nomeada (não
espalhada em múltiplos lugares) para o LT/QA calibrar em homologação com termos reais do catálogo
sem precisar mexer em lógica de query.

"Vazio genuíno" (CA 5.1) = estágio 1 devolve 0 **e** estágio 2 devolve 0 (nenhuma linha com
`GREATEST(similarity...) >= 0.15`). Não existe um terceiro estágio "ainda mais permissivo" —
abaixo desse threshold o próprio `pg_trgm` considera as strings não relacionadas; forçar mais
resultados a essa altura devolveria ruído sem valor (CA E.1 já topa com "não é exigida cobertura de
100% dos casos fonéticos extremos").

### 2.4 Extensões e índices Postgres

Duas extensões novas (contrib do Postgres, já incluídas na imagem `postgres:16.14-alpine` usada em
`docker-compose.yml` — sem mudança de infra/imagem, só `CREATE EXTENSION` na migration):
```sql
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
```

**Gotcha a documentar para o Dev:** `unaccent(text)` é `STABLE`, não `IMMUTABLE` — o Postgres
recusa usá-la direto numa coluna gerada (`GENERATED ALWAYS AS (...) STORED` exige expressão
`IMMUTABLE`). Precisa de um wrapper próprio:
```sql
CREATE OR REPLACE FUNCTION immutable_unaccent(text) RETURNS text AS $$
  SELECT unaccent('unaccent', $1)
$$ LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT;
```
Essa função (não `unaccent()` direto) é usada dentro do `setweight(to_tsvector(...))` da coluna
gerada (§2.2) — é o que dá tolerância a variação de acento (CU4, ex. "tenis" casando com "tênis")
de graça, via técnica de banco, sem IA.

**Índice do estágio 1 (sempre usado, caminho quente):**
```sql
CREATE INDEX "IX_products_search_vector" ON products USING gin (search_vector);
```
GIN é a escolha padrão do Postgres para `tsvector` (não há alternativa relevante aqui — GiST em
`tsvector` é mais lento para consulta e só compensa em cenários de update extremamente frequente,
não é o caso de `products`, que já tem 6 índices compostos e updates moderados dos jobs).

**Índice do estágio 2 (fallback) — decisão de NÃO criar `GIN`/`GiST` `gin_trgm_ops` agora:**
avaliado e **adiado deliberadamente (YAGNI)**, mesmo estilo de decisão já usado no design da Issue
#228 (§2.2 daquele design, "não antecipar índice sem medição"). Motivo: a query do estágio 2 usa
`similarity(coluna, @q) >= @threshold` (não o operador `%`) — só o operador `%` (que lê o GUC de
sessão `pg_trgm.similarity_threshold`) é acelerado por um índice GIN/GiST trigram; a forma
`similarity() >= constante` força sequential scan mesmo com o índice presente. Migrar para `%`
exigiria `SET LOCAL pg_trgm.similarity_threshold` dentro de uma transação explícita por request
(pegadinha real de connection pooling: `SET` sem `LOCAL`/sem transação vaza a configuração para a
próxima requisição que reusar a conexão do pool do Npgsql) — complexidade real sem ganho
mensurável hoje: o estágio 2 só roda quando o estágio 1 devolve zero (minoria dos casos), sobre um
catálogo de **~100-200 produtos** já filtrados por `Status = Published` (índice existente). Um
sequential scan computando `similarity()` 3x por linha nesse volume é da ordem de baixos
milissegundos — dentro do alvo de <300-500ms com folga. **Risco monitorado (§6):** se o catálogo
crescer para dezenas de milhares de produtos E o estágio 2 se mostrar uma fração relevante do
tráfego de busca com latência medida acima do alvo, aí sim migrar a query do estágio 2 para o
operador `%` + índice `gin_trgm_ops` + `SET LOCAL` transacional — não antes.

### 2.5 Contrato do endpoint — extensão de `GET /api/public/deals`, não endpoint novo

**Decisão: novo parâmetro `q` opcional em `PublicController.GetDeals`**, reaproveitando
paginação/DTO/composição com os filtros já existentes (`category`, `subcategory`, `minPrice`,
`maxPrice`, `sort`). Mesmo padrão aditivo já usado nesse endpoint desde a Issue #167 (e mantido nas
Issues #230/#261/#262) — um `GET /api/public/deals/search` dedicado duplicaria paginação/DTO/
composição de filtro já testados, pelo mesmo motivo já registrado no design da Issue #228 §2.1
("por que não dois endpoints" — a heurística vale aqui de novo: mesma entidade, mesma forma de
filtro/paginação, um parâmetro a mais).

**Campo novo de resposta — `IsApproximateSearch` (bool?) em `PagedResult<T>`:** o frontend precisa
saber se os resultados vieram do estágio 1 (match "de verdade") ou do estágio 2 (fallback
aproximado) para decidir a sinalização (CA 4.2 vs. comportamento normal). Em vez de criar um DTO de
resposta paralelo só para a busca (quebraria o reaproveitamento de `PagedResult<T>` já usado por
`ProductsController`/`QueueController`/`PublicController`), o campo é **aditivo e nullable** no
`PagedResult<T>` genérico: `null` quando não há `q` (irrelevante, comportamento de todos os outros
consumidores inalterado — mesmo padrão de campo aditivo já usado repetidamente no projeto, ex.
`Subcategory`/`Platform`/`Destinations`), `false` quando `q` presente e o estágio 1 achou algo,
`true` quando `q` presente e só o estágio 2 achou algo. `Items.Count == 0 && q presente` (com
`IsApproximateSearch == false`) é o "vazio genuíno" (CA 5.1) — o frontend distingue os 3 estados só
com esses dois campos, sem heurística própria.

**Decisão de composição com `sort`:** quando `q` está presente, o backend **ignora o parâmetro
`sort`** e ordena sempre por relevância (estágio 1: `ts_rank` desc; estágio 2: `combined_score`
desc) — é a única forma de cumprir CA 3.2/3.3 (título > categoria > descrição) de forma
determinística; deixar `sort=price_asc` reordenar por cima do ranking de relevância quebraria
diretamente esse critério de aceite. Sem `q`, o comportamento de `sort` continua 100% inalterado
(não regressão). Fica como nota para o LT/UX: o dropdown "Ordenar por" pode ser desabilitado ou
ocultado na UI quando há um termo de busca ativo — decisão visual, não bloqueia o backend.

### 2.6 Termo curto e composição com os demais filtros

**Termo curto (CA E.1):** `q` com menos de 2 caracteres (após `Trim()`) é tratado exatamente como
`q` ausente — nenhum filtro de busca é aplicado, sem erro, sem chamar estágio 1/2. Critério
simples e testável (`string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2`), evita ruído de
`similarity()`/`tsquery` de 1 caractere (que empurra qualquer coisa pra cima sem sinal real) sem
impedir o usuário de continuar digitando.

**Composição com outros filtros (CA 6.1):** `q` é **mais um `.Where()` na mesma `IQueryable`**
já filtrada por `category`/`subcategory`/`minPrice`/`maxPrice` — AND lógico automático, sem
código extra (mesma composição que já existe hoje entre os filtros atuais). Isso vale tanto para
o estágio 1 quanto o estágio 2 (os dois partem da mesma base `_db.Products.Where(status ==
Published).Where(outros filtros)`, só o predicado/ordem de texto muda entre os dois estágios).

## 3. Fluxo de dados

```
FilterBar (Client Component)
  ├─ novo input de texto na filter-bar (mesmo componente, mesmo grupo visual dos demais)
  ├─ estado local (searchDraft) a cada onChange — não escreve na URL a cada tecla
  ├─ debounce 350ms (SEARCH_COMMIT_DEBOUNCE_MS) → commitSearch()
  │     mesmo mecanismo de draft+debounce já usado para minPrice/maxPrice (design.md #230) —
  │     reaproveita o padrão, não inventa um novo
  └─ commitSearch(): router.replace(pathname?...&q=searchDraft) — replace (não push), mesmo
       raciocínio já aplicado ao preço: digitar é refinamento contínuo, não deve empilhar
       histórico por tecla/pausa

app/page.tsx (Server Component, re-renderiza a cada navegação de searchParams)
  ├─ buildFilters() ganha `q: searchParams.q`
  └─ fetchDeals(page, pageSize, filters) → GET /api/public/deals?...&q=...

app/loading.tsx (NOVO — Suspense fallback da rota, ver §4)
  └─ exibido automaticamente pelo App Router enquanto page.tsx aguarda fetchDeals/fetchCategories
     durante QUALQUER navegação de searchParams (busca ou outro filtro) — cobre CA 2.2 (loading
     se a resposta ultrapassar tempo perceptível), sem plumbing de estado manual

Backend — PublicController.GetDeals(..., string? q, ...)
  base = _db.Products.Where(Status == Published).Where(outros filtros já existentes)

  if (irrelevante(q))            // ausente ou < 2 chars, ver §2.6
      ordered = base.OrderBy(sort existente)               // comportamento 100% atual

  else {
      tier1 = base.Where(search_vector @@ tsquery_prefixo(q))
                   .OrderByDescending(ts_rank(search_vector, tsquery_prefixo(q)))
      (total1, page1) = tier1.ToPagedResultAsync(...)

      if (total1 > 0)
          retorna page1, IsApproximateSearch = false

      else {
          tier2 = base.Where(GREATEST(similarity(title,q), similarity(category,q),
                                       similarity(description,q)) >= 0.15)
                       .OrderByDescending(combined_score)   // §2.2
          (total2, page2) = tier2.ToPagedResultAsync(...)
          retorna page2, IsApproximateSearch = (total2 > 0 ? true : false)
          // total2 == 0 → PagedResult vazio, IsApproximateSearch=false → "vazio genuíno" (CA 5.1)
      }
  }
```

## 4. Componentes afetados

| Componente | Mudança | Escopo |
|---|---|---|
| `AfiliadoBot.Infrastructure.Data.Configurations.ProductConfiguration` | Shadow property `SearchVector` (`tsvector`) via `HasComputedColumnSql(sql, stored: true)`, mapeada para a coluna gerada — necessário para o model snapshot do EF não tentar "corrigir"/dropar a coluna gerada por SQL raw em migrations futuras; novo índice GIN `IX_products_search_vector` | Backend |
| Migration nova (`AddProductSearchVector` ou similar) | `CREATE EXTENSION unaccent/pg_trgm`, função `immutable_unaccent`, `ALTER TABLE ... ADD COLUMN search_vector ... GENERATED ALWAYS AS (...) STORED`, `CREATE INDEX ... USING gin`, tudo via `migrationBuilder.Sql(...)` (feature Postgres-específica, sem equivalente na API fluente genérica do EF) | Backend |
| `AfiliadoBot.Api.Controllers.PublicController.GetDeals` | Novo `[FromQuery] string? q`; lógica de 2 estágios (§2.5/§3); ignora `sort` quando `q` relevante | Backend |
| `AfiliadoBot.Api.Common.PagedResult<T>` | Novo campo aditivo `IsApproximateSearch` (`bool?`, default `null`) | Backend |
| Constantes novas (`PublicController` ou classe estática `SearchConstants`) | `MinQueryLength = 2`, `ApproximateSimilarityThreshold = 0.15` | Backend |
| `website/lib/api.ts` (`DealFilters`) | Novo campo opcional `q?: string`, propagado para a querystring de `fetchDeals` | Frontend |
| `website/lib/types.ts` (`PagedResult<T>`) | Novo campo opcional `isApproximateSearch?: boolean \| null` | Frontend |
| `website/components/FilterBar.tsx` | Novo grupo/input de busca textual; estado local `searchDraft` + debounce 350ms + `router.replace`, mesmo mecanismo já usado pro preço (design.md #230); nova pílula de filtro ativo para `q` no `Pills()`; `q` entra em `RESTRICTIVE_KEYS` (conta pro badge "Limpar filtros") | Frontend |
| `website/app/page.tsx` | `buildFilters`/`buildPaginationQuery`/`HomePageProps.searchParams` ganham `q`; bloco de resultado ganha 3 estados: normal / "resultados aproximados para 'q'" (`isApproximateSearch === true`) / "nenhum produto encontrado para 'q'" (`items.length === 0 && q` presente, distinto do vazio sem filtro já existente) | Frontend |
| `website/app/loading.tsx` (**novo arquivo**) | Suspense fallback simples (skeleton/spinner, visual a cargo do UX/UI ou reaproveitando tokens existentes) — cobre CA 2.2 para busca e, como efeito colateral positivo, para todos os filtros já existentes (hoje não há nenhum loading state, gap que este arquivo fecha) | Frontend |
| Testes (`PublicControllerTests`, integração via Testcontainers já usada no projeto — `ClaudeBudgetServiceIntegrationTests` é o precedente — necessário aqui porque `tsvector`/`pg_trgm` não existem no provider InMemory do EF Core) | Cobrir os cenários de §5 | Backend |
| Testes (`FilterBar.test.tsx`, `page.test.tsx`, Playwright e2e) | Cobrir os cenários de §5 | Frontend |

## Contrato de componentes globais

| Componente | Renderiza em | NÃO renderiza em |
|---|---|---|
| `RootLayout` (`app/layout.tsx`) — `<html>/<body>` + `PushSubscriptionManager` | `app/layout.tsx` (inalterado) | Screens individuais |
| `Header` | Dentro de cada page (`app/page.tsx`, `app/categoria/[categoria]/page.tsx`) — **não** está no `RootLayout` hoje; convenção já existente no projeto, não alterada por esta issue | `app/layout.tsx` |
| `FilterBar` (com o novo campo de busca) | `app/page.tsx` apenas (tela de listagem) | `app/categoria/[categoria]/page.tsx`, `app/oferta/[slug]/page.tsx` — sem mudança de escopo |
| `app/loading.tsx` (**novo**) | Suspense fallback automático de toda a árvore `app/` (App Router) durante navegação/fetch pendente | Não é renderizado explicitamente por nenhum componente — convenção de arquivo do Next.js |

Não há Provider novo nesta issue.

## 5. Casos de teste a cobrir (mapeamento para os critérios de aceite)

- `PublicControllerTests` (Testcontainers, Postgres real — obrigatório pra exercitar
  `tsvector`/`pg_trgm` de verdade):
  - `q` vazio/ausente → comportamento idêntico ao atual, `IsApproximateSearch == null` (CA 1.2,
    não-regressão).
  - `q` = 1 caractere → tratado como ausente, sem erro (CA E.1).
  - `q` com match exato/substring em só um campo (ex. só na `description`) → produto aparece (CA
    3.1).
  - `q` com match em título de um produto e só na descrição de outro → o do título vem primeiro
    (CA 3.2); combinar título+categoria+descrição de produtos diferentes → ordem título > categoria
    > descrição (CA 3.3); produto com match em 2+ campos rankeia acima de um só com 1 campo.
  - `q` com erro de digitação comum (troca/falta/sobra de letra) sem match exato → estágio 2
    aciona, retorna aproximados, `IsApproximateSearch == true` (CA 4.1/4.3).
  - `q` plural/singular e variação de acento (ex. "tenis"/"tênis") → resolvido pelo estágio 1
    (stemmer + `immutable_unaccent`), `IsApproximateSearch == false` (cobertura "de graça", §1).
  - `q` sem nenhuma relação com o catálogo (abaixo do threshold 0.15 em tudo) → lista vazia,
    `IsApproximateSearch == false` (vazio genuíno, CA 5.1, distinto do caso acima).
  - `q` combinado com `category`/`minPrice`/`maxPrice` ativos → resultado respeita a interseção
    (AND, CA 6.1).
  - `q` presente + `sort=price_asc` → ordenação por relevância prevalece (§2.5), não por preço.
  - Nenhuma chamada à Anthropic/Claude API disparada em nenhum dos cenários acima (CA 7.1) —
    asserção explícita (mock/spy do client de IA nunca invocado).
- `FilterBar.test.tsx`: digitar no campo não navega a cada tecla (debounce); após parar de digitar,
  navega com `q` na URL via `replace` (não `push`, sem empilhar histórico); campo vazio remove `q`
  da URL; pílula de busca ativa aparece e é removível; conta para "Limpar filtros".
- `page.test.tsx`: com `isApproximateSearch === true` exibe banner "resultados aproximados";
  `items.length === 0` com `q` presente exibe mensagem de vazio genuíno distinta da mensagem de
  "nenhuma oferta com esses filtros" já existente (CA 5.1); sem `q`, nenhuma mudança de
  comportamento.
- Playwright e2e: fluxo completo — digitar termo com erro de digitação → ver resultados
  aproximados sinalizados; digitar termo sem nenhuma relação → ver estado de vazio genuíno; limpar
  o campo → lista volta ao estado padrão.

## 6. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Estágio 2 (`similarity() >= threshold`) faz sequential scan, sem índice acelerando o predicado (§2.4) | Aceito por ora (YAGNI) — volume atual (~100-200 produtos) torna isso irrelevante; documentado o caminho de migração (`%` + `gin_trgm_ops` + `SET LOCAL` transacional) para quando houver medição real de degradação |
| `immutable_unaccent` mal implementada (ex. sem `IMMUTABLE` correto) quebra a criação da coluna gerada em produção | Migration testada em homologação antes do merge para main (gate já existente do pipeline); função é o padrão documentado da comunidade Postgres para esse problema, baixo risco de implementação |
| Threshold `0.15` gerar resultados aproximados percebidos como "não relacionados" pelo usuário real | Constante isolada e nomeada — QA calibra em homologação com termos reais do catálogo antes do Gate 2; ajustar não exige mudança de query/lógica |
| `q` como novo vetor de scraping/abuso (queries arbitrárias no endpoint público) | Mesma política de rate limit já existente (`public-read`, 60 req/min/IP) se aplica sem mudança — nenhuma superfície nova de ataque além do que os filtros já existentes já expõem |
| Ignorar `sort` quando `q` presente pode surpreender um usuário que espera ordenar por preço dentro de uma busca | Decisão de negócio implícita no CA 3.2/3.3 (ranking por relevância é o requisito explícito); LT/UX decide se o dropdown de ordenação fica desabilitado/oculto durante busca ativa (não bloqueia backend) |
| `Description` pode ser um texto longo — `to_tsvector`/`similarity` sobre texto longo tem custo maior por linha que sobre `title`/`category` | Aceitável no volume atual; se o catálogo crescer muito, o índice GIN do estágio 1 já absorve esse custo (não faz sequential scan); estágio 2 é o único exposto a esse custo e só roda na minoria dos casos (§2.4) |

## 7. Dependências

- Extensões Postgres `unaccent` e `pg_trgm` — contrib padrão da imagem `postgres:16.14-alpine` já
  em uso (`docker-compose.yml`), sem mudança de infraestrutura.
- Depende de `Product.Title`/`Category`/`Description` (colunas já existentes, `NOT NULL`) — sem
  mudança de schema além da coluna gerada nova.
- Reaproveita `PagedResult<T>`/`PaginationExtensions` (Issue #11) e o padrão de filtro combinável
  aditivo de `PublicController.GetDeals` (Issue #167) — sem alterar contrato dos filtros
  existentes.
- Reaproveita o mecanismo de estado local + debounce + `router.replace` já implementado pro preço
  em `FilterBar.tsx` (Issue #230) — mesma técnica, não uma nova.

## 8. Fora de escopo

- Chamada à IA (Claude) por requisição de busca — restrição de negócio vinculante e definitiva
  (não reaberta aqui).
- Busca fonética "verdadeira" (ex. `soundex`/`metaphone`, mais forte para erros de pronúncia do que
  `pg_trgm`) — não avaliada porque o `pg_trgm` já cobre satisfatoriamente a meta qualitativa
  (erros de digitação comuns) dentro da restrição de banco de dados; `soundex`/`metaphone` do
  Postgres (`fuzzystrmatch`) são calibrados para nomes próprios em inglês, não para português — se
  o Gerente medir cobertura insuficiente em produção, é uma extensão futura isolada (mais um
  estágio 3 opcional), não bloqueante aqui.
- Campo `Subcategory` no escopo de busca (decisão explícita, §2.2).
- Índice `gin_trgm_ops` para o estágio 2 (adiado, YAGNI, §2.4).
- Sugestões de autocomplete/typeahead (lista de termos enquanto digita, antes de aplicar o filtro)
  — não pedido nos critérios de aceite (busca filtra a listagem, não sugere termos).
