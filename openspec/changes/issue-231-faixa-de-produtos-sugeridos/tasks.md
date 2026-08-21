# Tasks — ISSUE-231: Rastreio de cliques + faixa de produtos sugeridos (site público)

Devs leem apenas este arquivo (+ a sub-issue do GitHub correspondente). Detalhe técnico completo em
`documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/especificacao-tecnica.md` — cada task abaixo
referencia a seção relevante.

---

## T-01 (stack:dotnet) — Schema: `ProductClick` + `Product.ClickCount` + índices

**O que fazer:**
- `Product.cs`: `+ ClickCount` (int, default 0) e método `RegisterClick()`.
- Nova entidade `ProductClick.cs` (id long, ProductId uuid, ClickedAt timestamptz).
- `ProductConfiguration.cs`: mapear `click_count` + 2 índices compostos novos
  (`IX_products_status_category_clickcount`, `IX_products_status_clickcount`).
- Nova `ProductClickConfiguration.cs`: mapear tabela `product_clicks` + índice em `product_id`.
- `AfiliadoBotDbContext.cs`: `+ DbSet<ProductClick> ProductClicks`.
- Migration `AddProductClicksAndClickCount` via `dotnet ef migrations add` (não escrever a mão).

**Contexto técnico:** especificacao-tecnica.md §2 (código exato de cada trecho). design.md §4/§5
(justificativa das decisões de persistência/índice).

**Critérios de aceite:**
- **Given** o schema atual do banco **When** a migration é aplicada **Then** `products` ganha
  coluna `click_count integer NOT NULL DEFAULT 0` e existem os 2 novos índices compostos
  **And** a tabela `product_clicks` existe com `id, product_id (FK cascade), clicked_at`
  e índice em `product_id`.
- **Given** um `Product` recém-criado **When** `RegisterClick()` é chamado **Then**
  `ClickCount` incrementa em 1 e `UpdatedAt` é atualizado.
- **Given** a suíte de testes existente do projeto (xUnit) **When** rodada após esta mudança
  **Then** nenhum teste pré-existente quebra (migration não é destrutiva).

---

## T-02 (stack:dotnet) — Endpoint `POST /api/public/products/{id}/click`

**Depende de:** T-01 (precisa de `ClickCount`/`ProductClick` existirem). Branch a partir de `desenv`
já com T-01 merged, ou coordenar com o LT a ordem de merge.

**O que fazer:**
- Novo controller `PublicProductsController.cs` (`api/public/products`, `[AllowAnonymous]`).
- `POST /api/public/products/{id:guid}/click` — sem corpo, sempre `202 Accepted` (mesmo se produto
  não existir), aplica `RateLimiterConfigurator.PublicWritePolicy`.
- Insere `ProductClick` + chama `product.RegisterClick()` + `SaveChangesAsync` no mesmo request.

**Contexto técnico:** especificacao-tecnica.md §3.1/§3.2 (código completo). design.md §7
(justificativa: sem corpo, síncrono, sem fila, sem dedup).

**Critérios de aceite (CA 2.1-2.4 de criterios-aceite.md):**
- **Given** um produto existente **When** `POST /api/public/products/{id}/click` **Then** retorna
  `202` **And** um registro é criado em `product_clicks` com aquele `product_id` e um `clicked_at`
  próximo de "agora" **And** `products.click_count` daquele produto incrementa em 1.
- **Given** um `id` de produto inexistente **When** o endpoint é chamado **Then** retorna `202`
  (não `404`) — nunca expõe erro que o cliente do `sendBeacon` não vai ler.
- **Given** o registro (evento anônimo) **When** inspecionado **Then** contém apenas `id, product_id,
  clicked_at` — nenhum dado de usuário/sessão/IP persistido (CA 2.3).
- **Given** múltiplas chamadas rápidas ao mesmo endpoint pelo mesmo IP **When** excede
  `PublicWritePolicy` (10/min) **Then** recebe `429` (comportamento herdado da policy já existente,
  sem código novo de rate limit).

---

## T-03 (stack:dotnet) — Endpoint `GET /api/public/products/suggested`

**Depende de:** T-01. Pode ser desenvolvido em paralelo a T-02 (endpoints e controllers distintos
dentro do mesmo `PublicProductsController.cs` — coordenar merge sequencial via LT para evitar
conflito de arquivo).

**O que fazer:**
- `GET /api/public/products/suggested?categories=&hasResults=` em `PublicProductsController.cs`
  (mesmo controller de T-02).
- Lógica de fallback (categorias vazias OU `hasResults=false` → fallback geral; senão → filtro por
  categoria), ordenação `ClickCount DESC, CreatedAt DESC`, `LIMIT 10`, corte mínimo de 4 (retorna
  lista vazia se não atingir).
- **Adicionar `Id` (uuid) ao `PublicDealDto`** — necessário para o frontend identificar o produto
  no clique (T-04 depende disso). Ver especificacao-tecnica.md §4.1 — `id` não é dado sensível,
  apenas ausente do DTO até agora porque não havia necessidade.
- `RateLimiterConfigurator.PublicReadPolicy` (mesma policy de `GetDeals`).

**Contexto técnico:** especificacao-tecnica.md §3.3 (código completo), §4.1 (motivo do `Id` no DTO).
design.md §6 (contrato/decisão de fallback no backend), criterios-aceite.md itens 1.1/1.2/1.5/1.6/1.7.

**Critérios de aceite:**
- **Given** uma categoria com ≥4 produtos publicados **When** `GET .../suggested?categories=X&hasResults=true`
  **Then** retorna até 10 produtos daquela categoria, ordenados por `click_count` desc (empate por
  `created_at` desc) **And** cada item inclui `id`.
- **Given** `hasResults=false` (ou `categories` vazio) **When** o endpoint é chamado **Then** retorna
  o fallback geral (todas categorias, produtos Published), mesma ordenação/limite.
- **Given** a lista resultante (categoria ou fallback) tem menos de 4 produtos **When** o endpoint
  é chamado **Then** retorna `[]` (lista vazia, não erro).
- **Given** nenhum produto tem `click_count > 0` ainda **When** o endpoint é chamado **Then** ainda
  retorna produtos (desempate por `created_at` desc garante que a faixa não fica vazia por falta de
  histórico — CA 1.7).

---

## T-04 (stack:nodejs) — Rastreio de clique no card (frontend)

**Depende de:** T-02 e T-03 mergeados em `desenv` (endpoint precisa existir; `deal.id` vem de T-03).

**O que fazer:**
- Novo `lib/tracking.ts` (`'use client'`) — `trackProductClick(productId)` via `navigator.sendBeacon`
  com fallback `fetch(..., { keepalive: true })`.
- Novo `components/DealCardLink.tsx` (`'use client'`) — extrai o `<a>` CTA de `DealCard.tsx`,
  chama `trackProductClick` no `onClick`, mantém `href`/`target`/`rel` idênticos ao atual.
- `DealCard.tsx` passa a renderizar `DealCardLink` no lugar do `<a>` inline (mantém `DealCard` como
  Server Component).
- `lib/types.ts` (`Deal`): `+ id: string`.

**Contexto técnico:** especificacao-tecnica.md §4.1/§4.2 (código completo, decisão de boundary
client/server). design.md §7 (por que `sendBeacon`, sem bloquear navegação).

**Critérios de aceite (CA 2.1, 2.2, 2.4 de criterios-aceite.md):**
- **Given** o visitante clica no CTA de um `DealCard` na listagem normal **When** o clique ocorre
  **Then** `trackProductClick` é chamado com o `id` do produto **And** a navegação para
  `affiliateLink` prossegue exatamente como hoje (mesmo `href`, `target="_blank"`, `rel="nofollow"`).
- **Given** o mesmo card renderizado dentro do carrossel de sugeridos (T-05) **When** clicado
  **Then** o comportamento é idêntico ao da listagem normal (mesmo componente `DealCardLink`
  reaproveitado).
- **Given** o endpoint de clique falha/timeout **When** o visitante clica **Then** a navegação não é
  bloqueada nem atrasada (chamada é fire-and-forget, catch silencioso).
- **Given** a suíte de testes do `website/` **When** rodada (`npx jest --coverage`) **Then** cobertura
  ≥ 80% mantida, testes novos para `DealCardLink`/`lib/tracking.ts`.

---

## T-05 (stack:nodejs) — Faixa/carrossel de produtos sugeridos

**Depende de:** T-03 mergeado em `desenv` (endpoint `suggested` precisa existir). Pode ser feito em
paralelo a T-04 (componentes distintos), mas ambos tocam `DealCard`/card reaproveitado — coordenar
merge sequencial via LT.

**O que fazer:**
- Novo `lib/suggested.ts` (`'use client'`) — `fetchSuggestedProducts(category, hasResults)`.
- Novo `components/SuggestedProductsCarousel.tsx` (`'use client'`) — fetch em `useEffect` com
  try/catch isolado, carrossel horizontal (scroll nativo + setas via `scrollBy`), reaproveita
  `DealCard`/`DealCardLink` (de T-04) para renderizar cada item, `return null` se lista vazia/erro.
- Setas desabilitadas/ocultas nos extremos (checar `scrollLeft`/`scrollWidth` via `onScroll`).
- `app/page.tsx`: renderizar `<SuggestedProductsCarousel category={filters.category} hasResults={deals.length > 0} />`
  abaixo do grid principal (posição default — confirmar com UX/UI se houver spec de posição).

**Contexto técnico:** especificacao-tecnica.md §4.3/§4.4/§4.5 (código/posição). design.md §2 (tabela
de contrato de onde o componente renderiza), §6 (regra de fallback já decidida no backend — este
componente só consome, não decide fallback).

**Critérios de aceite (CA 1.1-1.8 de criterios-aceite.md):**
- **Given** um filtro de categoria ativo com resultado **When** a página renderiza **Then** a faixa
  aparece com produtos daquela categoria, ordenados por mais clicados (dado que já vem ordenado do
  backend — o componente só renderiza na ordem recebida).
- **Given** um filtro que não retorna produtos **When** a página renderiza **Then** a faixa mostra o
  fallback geral (envia `hasResults=false` para o endpoint).
- **Given** a faixa tem produtos suficientes **When** renderizada **Then** aparece como carrossel
  horizontal com setas esquerda/direita, desabilitadas/ocultas nos extremos.
- **Given** o clique em um item do carrossel **When** ocorre **Then** comportamento idêntico a um
  card da listagem normal (mesmo `DealCardLink`, T-04).
- **Given** o backend retorna lista vazia (`[]`, corte mínimo de 4) **When** a página renderiza
  **Then** a faixa não é exibida (nenhum carrossel vazio/parcial).
- **Given** o endpoint `suggested` está indisponível/erro **When** a página renderiza **Then** o
  grid principal funciona normalmente e a faixa é omitida sem erro visível ao usuário.
- **Given** a suíte de testes do `website/` **When** rodada **Then** cobertura ≥ 80% mantida, com
  testes novos para `SuggestedProductsCarousel`/`lib/suggested.ts`; e2e Playwright cobrindo o fluxo
  categoria-com-resultado vs. fallback (ver especificacao-tecnica.md §6).

---

## Ordem de merge sugerida (LT)
T-01 → (T-02, T-03 em paralelo, mesmo arquivo de controller — resolver conflito trivial no merge) →
(T-04, T-05 em paralelo, ambos tocam `DealCard`/`app/page.tsx` — resolver conflito trivial) → PR
`desenv→homolog`.
