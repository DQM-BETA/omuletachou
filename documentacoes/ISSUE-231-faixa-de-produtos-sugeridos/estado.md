---
issue: 231
titulo: feat: rastreio de cliques + faixa de produtos sugeridos (site público)
etapa_atual: QA
ultimo_agente: code-review
openspec_change: openspec/changes/issue-231-faixa-de-produtos-sugeridos
tech_stacks:
  - Backend (ASP.NET Core 8.0)
  - Frontend (Next.js 14+ — website público)
  - Banco (PostgreSQL 16)
repos:
  - omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-231-faixa-de-produtos-sugeridos
openspec_path: repos/omuletachou/openspec/changes/issue-231-faixa-de-produtos-sugeridos
sub_issues:
  - "#276 (stack:dotnet, task_id:T-01) — Schema: ProductClick + Product.ClickCount + índices"
  - "#277 (stack:dotnet, task_id:T-02) — Endpoint POST /api/public/products/{id}/click"
  - "#278 (stack:dotnet, task_id:T-03) — Endpoint GET /api/public/products/suggested"
  - "#279 (stack:nodejs, task_id:T-04) — Rastreio de clique no card (frontend)"
  - "#280 (stack:nodejs, task_id:T-05) — Faixa/carrossel de produtos sugeridos (frontend)"
desenv_tasks_merged:
  - "#276"
  - "#277"
  - "#278"
  - "#279"
  - "#280"
sub_issues_frontend:
  T-04: "#279"
  T-05: "#280"
pr_homologacao: 286
pr_release: ~
code_review_homolog_pr: "286 (aprovado 2026-08-21, 2ª rodada — fix do bug de double-encoding do teste e2e confirmado via execução real; merge desenv→homolog concluído, merge commit 24b39641e86b78aa0263b6140abb0ef9121ea38b)"
qa_status: ~
figma_url: "https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library (consultado — apenas conteúdo padrão de template do Figma, sem frames/tokens reais do projeto; ver ux-ui-spec.md §0)"
blockers: ~
status_comment_id: ~
---

## Descrição

Pedido do Gerente. Escopo restrito aos itens 1-2 do pedido original (rastreio de cliques + faixa de produtos sugeridos) — o item 3 (grid do dashboard Products) virou a **Issue #275** (issue separada, independente).

### 1. Faixa de produtos sugeridos inteligente (site público, tela de produtos)
- Carrossel horizontal com setas de navegação, baseado na categoria dos produtos atualmente filtrados.
- Fallback "mais clicados em geral" quando o filtro atual não retorna produtos.

### 2. Rastreio de cliques (pré-requisito do item 1)
- Evento anônimo (produto + timestamp), disparado ao clicar em qualquer card de produto (listagem normal ou carrossel de sugeridos), sem alterar o destino atual do clique.

## Gate 1 — respostas do Gerente (2026-08-21, postadas na Issue)

1. Escopo confirmado: separar. Item 3 → Issue #275. Esta issue fica só com itens 1-2.
2. Critério de ordenação dentro da categoria: **mais clicados** (não AI Score, não mais recentes). Quantidade/fallback/mínimo: decisão de produto do PM (ver proposal.md).
3. Destino do clique **não muda**. Evento **anônimo** confirmado. Cliques no carrossel de sugeridos contam igual aos da listagem normal.
4. Investigação de `discount_pct` fica para Arquiteto/LT decidir, não é obrigatória nesta issue.
5. Rota: **`normal`**.

## Refinamento de Negócio (Fase 2 — concluído 2026-08-21)

- `proposal.md` e `criterios-aceite.md` escritos em `openspec/changes/issue-231-faixa-de-produtos-sugeridos/proposal.md` e `documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/criterios-aceite.md`.
- **Decisões de produto do PM** (não especificadas pelo Gerente, documentadas na proposal.md, sujeitas a ajuste no Code Review/QA):
  - Quantidade por carregamento do carrossel: 10 produtos.
  - Fallback "mais clicados": geral, sem corte por plataforma (apenas produtos ativos/disponíveis).
  - Mínimo de produtos para a faixa aparecer: 4.
  - Critério de desempate em 0 cliques/empate: delegado ao refinamento técnico (ex.: mais recentes primeiro).
- Sumário do PRD postado como comentário na Issue #231.

## Ambiguidade arquitetural — avaliação do PM

**Sim, há ambiguidade.** Pontos que exigiram decisão do Arquiteto antes do refinamento do LT:
1. Onde persistir a contagem de cliques (campo agregado no `Product` vs. tabela de eventos `product_clicks`).
2. Estratégia de agregação de "mais clicados por categoria" com performance aceitável (query on-the-fly vs. contador desnormalizado/job).
3. Contrato do endpoint da faixa de sugeridos (payload, síncrono vs. fila/Hangfire para registro de clique).
4. Investigação de `discount_pct` (Amazon/Shopee vs. Mercado Livre) — não obrigatória, mas registrada se relevante à modelagem do `Product`.

## Design Arquitetural (Arquiteto — concluído 2026-08-21)

`openspec/changes/issue-231-faixa-de-produtos-sugeridos/design.md` completo: tabela de eventos
`product_clicks` (histórico granular, append-only) + contador desnormalizado `products.click_count`
atualizado de forma síncrona; 2 índices compostos (ranking por categoria + fallback geral);
`GET /api/public/products/suggested?categories=&hasResults=` (fallback decidido no backend);
`POST /api/public/products/{id}/click` (sem corpo, pensado para `navigator.sendBeacon`).

## Investigação `discount_pct` — CONCLUÍDA (sessão principal, 2026-08-21)

O Arquiteto não tinha acesso de leitura ao código-fonte para executar a investigação. A sessão
principal executou por inspeção do código-fonte dos 3 collectors (banco local só tem produtos do
Mercado Livre — sem amostra de Amazon/Shopee para rodar a query SQL, mas a leitura de código é
conclusiva independente de amostra):
- `AmazonCollector.cs` (~linhas 253-274): `discountPct` real, calculado a partir de `SavingBasis`
  (Amazon PA-API).
- `ShopeeCollector.cs` (~linhas 119-259): query GraphQL já pede `discount` direto da API da Shopee,
  usado como recebido.
- `MercadoLivreCollector.cs` (~linha 339): único collector com `discountPct` hardcoded em `0`
  (limitação já tratada na Issue #182/#192).

**Decisão: `discount_pct` NÃO deve ser removida** — dado real para 2 das 3 plataformas. Item 4 da
issue original resolvido como "manter, sem ação necessária". Detalhe completo em
`openspec/changes/issue-231-faixa-de-produtos-sugeridos/design.md` §9. Nenhuma sub-issue desta
issue mexe em `discount_pct`.

## Refinamento Técnico (Líder Técnico — concluído 2026-08-21)

- `especificacao-tecnica.md` escrito com nomes de arquivo/classe reais confirmados contra o código
  (`PublicController.cs`, `ProductConfiguration.cs`, `Product.cs`, `DealCard.tsx`, `lib/api.ts`,
  `lib/push.ts`, `app/page.tsx`), resolvendo pontos que o Arquiteto deixou em aberto por falta de
  acesso de leitura ao código-fonte:
  - Novo controller `PublicProductsController.cs` (`api/public/products`), em vez de sobrecarregar
    `PublicController` (`api/public/deals`) com rotas absolutas.
  - `DealCard.tsx` é hoje **Server Component** — extraído `DealCardLink.tsx` (novo Client Component,
    só o `<a>` do CTA) para não converter o card inteiro em client.
  - `trackProductClick`/`fetchSuggestedProducts` são **client-side**, seguindo o padrão já
    estabelecido em `lib/push.ts` (`NEXT_PUBLIC_API_URL`, nunca `API_INTERNAL_URL` server-only).
  - `SuggestedProductsCarousel` busca do lado do cliente (isola naturalmente a falha, CA 1.8) — não
    entra no `Promise.all` server-side de `app/page.tsx`.
  - `PublicDealDto` precisa ganhar `Id` (uuid) — hoje só expõe `Slug`; o frontend precisa do `id`
    para registrar o clique (T-03 adiciona, T-04 consome).
- 5 sub-issues criadas (task breakdown completo em
  `openspec/changes/issue-231-faixa-de-produtos-sugeridos/tasks.md`, ordem de merge sugerida ao
  final do arquivo).
- `design.md` do Arquiteto commitado junto (estava pendente, não commitado antes desta invocação),
  com a seção 9 (`discount_pct`) atualizada com o achado real e uma seção 12 nova registrando os
  ajustes de nomes/arquitetura do refinamento técnico face ao código real.

## UX/UI (concluído 2026-08-21)

`documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/ux-ui-spec.md` escrito para a sub-issue #280
(T-05, `SuggestedProductsCarousel`):
- **Design System no Figma consultado, mas sem conteúdo utilizável para este projeto** — o arquivo
  contém apenas o template padrão do Figma (paleta/tipografia de exemplo, telas de onboarding), sem
  frames do site público real. A spec usa tokens semânticos e determina reaproveitar as
  classes/variáveis CSS já existentes em `DealCard.tsx`/`filter-bar.css` (fora do escopo de leitura
  do UX/UI) em vez de hardcodar valores novos. Registrada sugestão (fora de escopo desta issue) para
  o Gerente popular o Figma com o design real do site.
- **Posição:** abaixo do grid principal, acima da paginação (confirma o default do LT, com racional
  de UX).
- **Título:** "Em alta em {Categoria}" (categoria com resultado) / "Em alta na loja" (fallback) —
  mesma palavra nos dois cenários para dar identidade reconhecível à faixa.
- **Layout:** 1 card + peek (mobile) / 2 + peek (tablet) / 4 + peek (desktop), cards reaproveitando
  exatamente as dimensões do `DealCard` do grid (não redefinidas).
- **Setas:** obrigatórias em todos os breakpoints (decisão do Gerente), 40×40px com hit area 44×44px,
  `disabled` nativo nos extremos, recalculado a cada `onScroll` (inclui arrasto manual).
- **Loading:** skeleton (título + N cards esqueleto, mesma quantidade visível do breakpoint) — não
  espaço em branco nem spinner central.
- **Sem sugestões (fallback também vazio) ou erro:** faixa desaparece completamente, sem mensagem —
  confirma a decisão técnica já registrada (`return null`), com racional de UX (evita empilhar duas
  mensagens de "nada aqui" quando o grid principal também está vazio).
- Checklist de heurísticas de Nielsen verificável pelo QA incluído (§10 da spec).

## Dev — sub-issue #276 (T-01, concluído 2026-08-21)

- `Product.cs`: `+ ClickCount` (int, default 0) e método de domínio `RegisterClick()`.
- Nova entidade `ProductClick.cs` (id long, ProductId uuid, ClickedAt timestamptz), sem navegação
  para `Product` (padrão `JobRun`).
- `ProductConfiguration.cs`: `+ click_count` + 2 índices compostos novos
  (`IX_products_status_category_clickcount`, `IX_products_status_clickcount`).
- Nova `ProductClickConfiguration.cs`: mapeia `product_clicks` (FK cascade) + índice em
  `product_id`.
- `AfiliadoBotDbContext.cs`: `+ DbSet<ProductClick> ProductClicks`.
- Migration `AddProductClicksAndClickCount` gerada via `dotnet ef migrations add`, **aplicada e
  validada contra Postgres real** (`docker exec afiliado_db psql` — coluna, tabela, FK e os 3
  índices confirmados via `\d`/`\di`).
- TDD: testes novos em `ProductTests.cs` (RegisterClick), `ProductClickTests.cs` (entidade nova),
  `ProductConfigurationTests.cs` (índices/coluna novos), `ProductClickConfigurationTests.cs`
  (mapeamento completo). Suíte completa: 526/526 passando (100%, sem regressão).
- Boot da aplicação confirmado sem exceção: imagem buildada a partir da branch, container efêmero
  conectado ao Postgres real via rede Docker (`omuletachou_omuletachou_net`), `/health` → 200,
  Hangfire/DI inicializaram normalmente. Container/imagem de teste removidos após validação.
- PR #281 (`feature/ISSUE-276-schema-product-clicks` → `desenv`) aberto, pronto para merge do LT.

## Líder Técnico — merge sub-issue #276 (concluído 2026-08-21)

- PR #281 mergeado via **squash** em `desenv` (commit `f9ff443240b073c55cc09cfa53cb8826fffa769f`).
- Sub-issue #276 fechada (`gh issue close 276 --reason completed`).
- `desenv_tasks_merged: ["#276"]`. Faltam #277, #278, #279, #280 — **NÃO** criado PR
  `desenv→homolog` ainda.
- #277 (T-02) e #278 (T-03) agora **desbloqueadas**: dependiam de T-01 estar em `desenv` (schema
  `ProductClick`/`ClickCount` disponível).

## Dev — sub-issue #277 (T-02, concluído 2026-08-21)

- Novo `PublicProductsController.cs` (`backend/src/AfiliadoBot.Api/Controllers/`,
  `api/public/products`, `[AllowAnonymous]`) — separado de `PublicController` (recurso "product"
  por id, não "deal"/listagem).
- `POST /api/public/products/{id:guid}/click`: sem corpo (compatível com `navigator.sendBeacon`),
  sempre `202 Accepted` (mesmo produto inexistente, CA 2.2), protegido por
  `RateLimiterConfigurator.PublicWritePolicy` (10 req/min/IP, mesma policy de
  `POST /api/public/push/subscribe`). Insere `ProductClick` + chama `product.RegisterClick()`
  (da #276) na mesma transação implícita do `SaveChangesAsync`.
- TDD: `PublicProductsControllerTests.cs` novo (5 testes, `WebApplicationFactory`) cobrindo CA
  2.1-2.4 — clique em produto existente incrementa `ClickCount` e persiste `ProductClick`, múltiplas
  chamadas incrementam por chamada, produto inexistente retorna `202` sem criar evento, shape do
  evento (reflexão de propriedades) confirma ausência de dado de usuário/sessão, endpoint aceita
  `POST` sem corpo/content-type. Suíte completa: 531/531 passando (100%, sem regressão; eram 526 +
  5 novos).
- Boot da aplicação confirmado sem exceção: imagem buildada a partir da branch, container efêmero
  conectado ao Postgres real via rede Docker (`omuletachou_omuletachou_net`), `/health` → 200.
  Validação ponta a ponta contra Postgres real: produto seedado via `psql`, `curl -X POST` no
  endpoint → `202`, `product_clicks` com 1 linha (`product_id`/`clicked_at` corretos),
  `products.click_count` incrementado de 0 para 1; produto inexistente → `202` sem erro. Dados de
  teste, container e imagem removidos após validação.
- PR #282 (`feature/ISSUE-277-endpoint-registrar-clique` → `desenv`) aberto, pronto para merge do LT.

## Dev — sub-issue #278 (T-03, concluído 2026-08-21)

- `PublicProductsController.cs` (mesmo arquivo de T-02/#277 — conflito trivial esperado no merge
  sequencial, já sinalizado em `tasks.md`) ganha `GET /api/public/products/suggested?categories=&hasResults=`,
  `[EnableRateLimiting(RateLimiterConfigurator.PublicReadPolicy)]` (mesma policy de `GetDeals`).
- Fallback decidido no backend (design.md §6): `categories` vazio/ausente OU `hasResults=false`
  ignora o filtro de categoria e retorna o ranking geral; caso contrário, restringe às categorias
  informadas (CSV, `Contains`). Ordenação `ClickCount DESC, CreatedAt DESC`, `LIMIT 10`, corte
  mínimo de 4 (`< 4` → `[]`, não erro).
- `PublicDealDto` ganha `Id` (uuid) — reaproveitado sem novo contrato, campo adicionado conforme
  especificacao-tecnica.md §4.1. Teste existente `GetDeals_ApenasCamposAutorizados_...` (CA-D2)
  atualizado para incluir `"id"` na lista de campos autorizados (Gate g — verificado, nenhuma outra
  asserção de shape de `PublicDealDto` na suíte precisou de ajuste).
- TDD: `PublicProductsControllerTests.cs` novo (8 testes, `WebApplicationFactory` + InMemory)
  cobrindo ranking por categoria (`ClickCount` desc), presença de `id` no item, fallback geral
  (`hasResults=false` ignora categoria), fallback com `categories` ausente, corte mínimo (`< 4` →
  `[]`), desempate por `CreatedAt` desc quando nenhum produto tem clique, filtro de status
  `Published`, limite de 10. Suíte completa: 534/534 passando (100%, sem regressão; eram 531 + 8
  novos − 5 já contados de #277 aplicados nesta branch a partir de `desenv`).
- Boot da aplicação confirmado sem exceção: imagem buildada a partir da branch, container efêmero
  conectado ao Postgres real (`afiliado_db`) via rede Docker (`omuletachou_omuletachou_net`).
  Validação ponta a ponta contra dados reais via `psql`: 4 produtos com `click_count` distintos
  numa categoria de teste → ranking retornado na ordem correta (mais clicado primeiro); categoria
  com apenas 1 produto → `[]` (corte mínimo confirmado); `hasResults=false` com categoria filtrada
  → fallback geral (ignora a categoria, traz produtos reais do catálogo). Dados de teste, container
  e imagem removidos após validação.
- PR #283 (`feature/ISSUE-278-endpoint-suggested` → `desenv`) aberto, pronto para merge do LT.

## Líder Técnico — merge sub-issue #277 (concluído 2026-08-21)

- PR #282 mergeado via **squash** em `desenv` (`gh pr merge 282 --squash`, confirmado `state: MERGED`).
- Sub-issue #277 fechada (`gh issue close 277 --reason completed`).
- `desenv_tasks_merged: ["#276", "#277"]`.
- **PR #283 (#278) NÃO mergeado** — conflito real em `PublicProductsController.cs` (mesmo arquivo
  criado por #277 e alterado por #278, ambos com ações no mesmo controller). `gh pr merge 283
  --squash` falhou (`the merge commit cannot be cleanly created`); `gh pr update-branch 283`
  também falhou (`Cannot update PR branch due to conflicts`) — GitHub não consegue sincronizar
  automaticamente via API, é preciso resolução manual local (rebase/merge da branch
  `feature/ISSUE-278-endpoint-suggested` com `desenv`, mantendo **ambas** as actions —
  `RegisterClick`/`RegisterClickAsync` de #277 e `GetSuggested` de #278 — no mesmo controller).
  Resolução de conflito em código é fora do escopo do LT (não edita src/); devolvido ao Dev.
- Suíte de testes **não executada pelo LT** (fora do escopo do LT — rodar/validar código de
  aplicação é responsabilidade do Dev/Code Review, não do LT).

## Dev — resolução do conflito do PR #283 (#278, concluído 2026-08-21)

- `git fetch origin desenv` + `git merge origin/desenv` no worktree existente
  (`.worktrees/feature-ISSUE-278-endpoint-suggested`). 2 conflitos, ambos `add/add` (mesmo
  arquivo criado nos dois lados do merge sequencial já sinalizado em `tasks.md`):
  - `PublicProductsController.cs`: resolvido mantendo **ambas** as actions no mesmo controller —
    `RegisterClick` (POST `{id}/click`, #277) e `GetSuggested` (GET `suggested`, #278) — usings
    unificados (`AfiliadoBot.Api.Public`, `AfiliadoBot.Domain.Entities`, `AfiliadoBot.Domain.Enums`,
    `Microsoft.EntityFrameworkCore`), doc-comment de classe consolidado com a versão de #277 (menciona
    ambos os recursos "product" por id) e as constantes/campo `SuggestedLimit`/`SuggestedMinimumToShow`
    de #278 preservados.
  - `PublicProductsControllerTests.cs`: resolvido mantendo os 2 conjuntos de testes (helpers
    `SeedProductAsync` de #277 + `SeedPublishedProductAsync`/`SeedPendingProductAsync` de #278, sem
    colisão de nomes) na mesma classe `PublicProductsControllerTests`, doc-comment de classe
    consolidado citando os dois escopos (CA 2.1-2.4 de #277 + T-03 de #278).
- `dotnet build`: sucesso, 0 erros (1 warning pré-existente do Hangfire, não relacionado).
- `dotnet test`: **539/539 passando (100%)**, sem regressão (esperado ~540+; 531 de #277 + 8 novos
  de #278 = 539 — o "540+" da estimativa original incluía uma contagem aproximada, confirmado exato).
- Boot real confirmado: `dotnet publish` da branch mergeada, container efêmero
  (`omuletachou-api:latest` como runtime base, dll publicada montada) conectado ao Postgres real
  `afiliado_db` via rede Docker `omuletachou_omuletachou_net`, `Jwt__SigningKey`/`ConnectionStrings`
  via `.env` local. Log limpo: migrations aplicadas, Hangfire instalado/iniciado, `Application
  started` sem exceção. Endpoints das duas actions testados ponta a ponta contra dados reais:
  `GET /api/public/products/suggested?hasResults=false` → `200` com produtos reais do catálogo;
  `POST /api/public/products/{id-inexistente}/click` → `202`; `GET /health` → `200`. Container e
  build de publicação removidos após validação.
- Push da branch `feature/ISSUE-278-endpoint-suggested` (merge commit com `desenv`) para o remoto.
  PR #283 já existente, não recriado.

## Líder Técnico — merge sub-issue #278 (concluído 2026-08-21)

- PR #283 mergeado via **squash** em `desenv` (`gh pr merge 283 --squash`, confirmado `state: MERGED`,
  commit `162bad7a488c4f71ee9499c983c034b73343d415`).
- Sub-issue #278 fechada (`gh issue close 278 --reason completed`).
- `desenv_tasks_merged: ["#276", "#277", "#278"]`. Faltam #279, #280 (frontend) — **NÃO** criado PR
  `desenv→homolog` ainda (nem todas as sub-issues estão concluídas).
- `blockers` limpo (conflito do PR #283 já resolvido e mergeado pelo Dev na invocação anterior).

## Dev — sub-issue #279 (T-04, concluído 2026-08-21)

- Novo `website/lib/tracking.ts` (`'use client'`): `trackProductClick(productId)` — dispara
  `navigator.sendBeacon('/api/public/products/{id}/click')` (fire-and-forget), fallback
  `fetch(..., { keepalive: true })` com catch silencioso (CA 2.4) quando `sendBeacon` não existe.
- Novo `website/components/DealCardLink.tsx` (`'use client'`) — extrai o `<a>` CTA de
  `DealCard.tsx`, chama `trackProductClick` no `onClick`, mantém `href`/`target="_blank"`/
  `rel="nofollow"` idênticos ao atual (destino do clique não muda). `DealCard.tsx` permanece
  Server Component, boundary client isolado só no CTA.
- `website/lib/types.ts` (`Deal`): `+ id: string`, consumindo o `Id` já exposto por
  `PublicDealDto` (T-03/#278, já em `desenv`).
- Gate g: todos os `buildDeal`/mocks de `Deal` nos testes existentes (8 arquivos: `page.test.tsx`,
  `sitemap.test.ts`, `categoria/[categoria]/page.test.tsx`, `seo.test.ts`,
  `related-deals.test.ts`, `oferta/[slug]/page.test.tsx`, `DealDetail.test.tsx`, `api.test.ts`)
  atualizados com `id` — sem regressão de tipo/comportamento.
- TDD: `lib/tracking.test.ts` novo (sendBeacon chamado com URL correta, não chama fetch quando
  sendBeacon disponível, fallback fetch+keepalive, catch silencioso da falha), novo
  `components/DealCardLink.test.tsx` (href/target/rel preservados, `trackProductClick` chamado
  com o `id` do produto ao clicar), teste adicional em `DealCard.test.tsx` (integração ponta a
  ponta: clique no CTA real do card chama o tracking e preserva o link). Suíte completa:
  **19 suítes / 156 testes, 100% passando** (sem regressão). Cobertura global 92.96%
  stmts/89.42% branches/92.43% funcs/94.63% lines (≥ 80% mantida; `tracking.ts` e
  `DealCardLink.tsx` 100%).
- `npx next build` (produção, inclui type-check + lint do Next) e `npx next lint`: ambos sem
  erros. `npx next start` local confirmado subindo (200, "Ready") — único erro observado no log
  é `ENOTFOUND api` (hostname docker-only do backend, ambiente sem `docker compose` local, não é
  regressão desta mudança).
- Nota: `npx tsc --noEmit` bruto acusa erros pré-existentes de tipos do jest-dom
  (`toBeInTheDocument`/`toHaveAttribute` etc. não reconhecidos) em vários arquivos de teste
  **não tocados por esta sub-issue** — confirmado que os mesmos erros já existem no checkout de
  `desenv` antes desta mudança (`next build`, que é o gate real, não os reporta — só `tsc` cru).
  Não é regressão, registrado para eventual limpeza futura de infra (fora de escopo).
- PR #284 (`feature/ISSUE-279-rastreio-clique-card` → `desenv`) aberto, pronto para merge do LT.

## Líder Técnico — merge sub-issue #279 (concluído 2026-08-21)

- PR #284 mergeado via **squash** em `desenv` (`gh pr merge 284 --squash`, confirmado `state: MERGED`,
  commit `b4ef817e6ebc2aa623ba25086d1db9eda30787aa`).
- Sub-issue #279 fechada (`gh issue close 279 --reason completed`).
- `desenv_tasks_merged: ["#276", "#277", "#278", "#279"]`. Falta só #280 (carrossel) — **NÃO**
  criado PR `desenv→homolog` ainda (nem todas as sub-issues estão concluídas).
- #280 (T-05, carrossel) agora **desbloqueada de fato**: depende de `DealCardLink.tsx` (extraído
  por #279) estar em `desenv`, o que já ocorreu neste merge.

## Dev — sub-issue #280 (T-05, concluído 2026-08-21)

- Novo `website/lib/suggested.ts` (`'use client'`): `fetchSuggestedProducts(category, hasResults)`
  — chama `GET /api/public/products/suggested?categories=&hasResults=`; a lógica de fallback/corte
  mínimo é decidida inteiramente pelo backend (#278), o frontend só repassa o estado atual de
  filtro (mesmo padrão client-side de `lib/tracking.ts`/`lib/push.ts`).
- Novo `website/components/SuggestedProductsCarousel.tsx` (`'use client'`): fetch em `useEffect`,
  try/catch isolado (CA 1.8 — falha nunca quebra o grid principal, `return null`). Reaproveita
  `DealCard`/`DealCardLink` (#279) para cada item — mesmo componente do grid, garantindo rastreio
  de clique idêntico ao da listagem normal (CA 1.4).
  - Título dinâmico (`ux-ui-spec.md` §2): **"Em alta em {Categoria}"** (categoria com resultado,
    `hasResults=true` e `category` preenchida) vs. **"Em alta na loja"** (fallback/sem categoria).
  - Carrossel horizontal via `overflow-x` + `scroll-snap` + `scrollBy()` programático nas setas
    (sem lib nova, decisão já registrada em `especificacao-tecnica.md` §4.4), setas `<button
    disabled>` nativas com estado recalculado em `onScroll` (cobre clique e arrasto manual/touch,
    tolerância de subpixel).
  - Skeleton de loading (sem setas) enquanto o fetch está pendente; lista vazia (corte mínimo não
    atingido, CA 1.5) ou erro (CA 1.8) — mesmo resultado visual (`return null`), sem mensagem.
- `website/app/page.tsx`: renderiza `<SuggestedProductsCarousel category={filters.category}
  hasResults={deals.length > 0} />` abaixo do grid/estado vazio, acima da paginação — aparece
  tanto no caso normal (CA 1.1) quanto no fallback de grid vazio (CA 1.2).
- Novo `website/app/styles/suggested-carousel.css` (importado em `globals.css`) — layout do
  trilho/setas/skeleton reaproveitando os tokens já existentes (`tokens.css`), nenhuma
  cor/fonte/espaçamento novo, conforme `ux-ui-spec.md` §0/§4/§9.
- Novo `e2e/suggested-carousel.spec.ts` (Playwright) — fluxo categoria-com-resultado vs. fallback,
  clique num card do carrossel não quebra a navegação (mesmo padrão de `search.spec.ts`).
- Gate g: `app/page.test.tsx` — `SuggestedProductsCarousel` mockado (Client Component com fetch
  próprio, mesmo padrão do mock de `FilterBar` já existente) para os testes de página; 3 testes
  novos verificando que a página repassa `category`/`hasResults` corretos (categoria com
  resultado, fallback com grid vazio, sem filtro ativo). Componente real coberto isoladamente em
  `SuggestedProductsCarousel.test.tsx`.
- TDD: `lib/suggested.test.ts` (4 testes — categoria enviada, categoria ausente, hasResults=false
  repassado como está, erro HTTP lança exceção) e `SuggestedProductsCarousel.test.tsx` (17 testes
  — skeleton sem setas durante loading, título dinâmico nos 3 cenários, ordem de renderização
  preservada, lista vazia/erro não renderiza nada, integração de clique com `trackProductClick`,
  setas habilitadas/desabilitadas via scroll real (início/fim do trilho), `scrollBy` chamado com
  deslocamento correto em cada seta, refetch ao trocar filtro). Suíte completa do `website/`:
  **21 suítes / 176 testes, 100% passando** (eram 19/156 antes desta sub-issue). Cobertura global
  93.26% stmts/88.84% branches/93.18% funcs/94.78% lines (≥ 80% mantida; `suggested.ts` 100%,
  `SuggestedProductsCarousel.tsx` 95.12%/80%/100%/95.12%).
- `npx next build` (produção, inclui type-check + lint do Next) e `npx next lint`: ambos sem
  erros/warnings. `npx next start` local confirmado subindo (`curl` → 200) — único erro no log é
  `ENOTFOUND api` (hostname docker-only do backend, ambiente sem `docker compose` local, tratado
  pelo error boundary já existente, não é regressão).
- Nota (mesma já registrada por #279): `npx tsc --noEmit` bruto acusa erros pré-existentes de
  tipos do jest-dom em vários arquivos de teste **não tocados por esta sub-issue** — confirmado
  que os mesmos erros já existem no checkout de `desenv` antes desta mudança; `next build` (gate
  real) não os reporta. Não é regressão.
- PR #285 (`feature/ISSUE-280-carrossel-sugeridos` → `desenv`) aberto, pronto para merge do LT.
  **Última sub-issue pendente de #231** — quando mergeada, o LT pode criar o PR `desenv→homolog`.

## Líder Técnico — merge sub-issue #280 (concluído 2026-08-21)

- PR #285 mergeado via **squash** em `desenv` (`gh pr merge 285 --squash`, confirmado `state: MERGED`,
  commit `f5eafc620c152cbf13babd442cfc9e89ffa0c528`).
- Sub-issue #280 fechada (`gh issue close 280 --reason completed`).
- `desenv_tasks_merged: ["#276", "#277", "#278", "#279", "#280"]` — todas as 5 sub-issues de #231
  concluídas.
- PR #286 (`desenv` → `homolog`, **merge commit**, não squash) criado.
- `pr_homologacao: 286`, `etapa_atual: Code Review`.

## Code Review reprovou o PR #286 — mapeamento (Líder Técnico, concluído 2026-08-21)

**Veredito do CR:** todo o app (build/boot Docker real, migration/schema real, `dotnet test`
539/539, `npm test` 176/176, integração ponta a ponta contra Postgres real, OWASP básico,
`discount_pct` confirmado fora de escopo) **aprovado sem ressalvas**. O único blocker é um bug
isolado no teste e2e novo, não no app.

**Causa raiz:** `website/e2e/suggested-carousel.spec.ts` (introduzido por #280/T-05) faz
`encodeURIComponent()` sobre o valor de categoria retornado por `getRealCategoriaAndSlug()`
(`e2e/helpers.ts`), que **já vem URL-encoded** do `sitemap.xml`. Isso gera double-encoding
(`Casa%20e%20Cozinha` → `Casa%2520e%2520Cozinha`) na URL de teste, o backend não reconhece a
categoria (`hasResults=false`), e o teste sempre cai no fallback "Em alta na loja" em vez de
validar de fato a CA 1.1 ("Em alta em {Categoria}"). Confirmado pelo CR via curl: encoding único
funciona, double-encoded retorna vazio. O padrão correto já existe em `e2e/visual.spec.ts` (não
usa `encodeURIComponent`, porque o helper já entrega o valor codificado).

**Mapeamento:**
- Falha → sub-issue **#280** (T-05, único arquivo tocado: `website/e2e/suggested-carousel.spec.ts`).
  Não há impacto em código de app (backend/frontend) nem nas demais sub-issues (#276-#279,
  aprovadas sem ressalvas pelo CR).
- Sub-issue #280 **reaberta** (`gh issue reopen 280`, mesmo padrão de #228/#229/#230), com
  comentário de mapeamento detalhado postado
  (https://github.com/DQM-BETA/omuletachou/issues/280#issuecomment-5370894388).
- `desenv_tasks_merged` volta a `["#276", "#277", "#278", "#279"]` (remove #280 — pendente de
  novo merge após o fix). `code_review_homolog_pr` registrado como reprovado. `blockers` setado.
  `etapa_atual: Em Desenvolvimento`.
- PR #286 (`desenv→homolog`) segue **aberto**; o LT não fecha/recria — o próximo merge de #280
  soma um commit novo à branch `desenv`, que já está incluída no PR #286 (nenhuma ação adicional
  necessária no PR além do dev corrigir e o LT mergear a nova sub-issue).

**Escopo da correção (Dev, stack:nodejs):** ajustar `suggested-carousel.spec.ts` para não fazer
double-encode da categoria — seguir o padrão de `visual.spec.ts` (usar `categoria` direto na
URL/query, já vem codificada) ou `decodeURIComponent` antes de repassar para `URLSearchParams`.
Reexecutar a suíte Playwright completa contra o site real (não só `next build`/`next start`) e
confirmar os 17 specs passando, incluindo a CA 1.1 com uma categoria real, antes de abrir novo PR.

## Dev — correção da sub-issue #280 (T-05, fix pós-Code Review, concluído 2026-08-21)

- Worktree existente sincronizado com `desenv` (`git fetch` + `git merge origin/desenv`, sem
  conflito).
- `website/e2e/suggested-carousel.spec.ts` (linha ~19-22): removido `encodeURIComponent()`
  redundante sobre `categoria` (retornado já URL-encoded por `getRealCategoriaAndSlug`/
  `helpers.ts`, a partir do `sitemap.xml`) — mesmo padrão já usado em `visual.spec.ts`. Comentário
  adicionado explicando o porquê (evitar reincidência).
- Gate g: `Grep` por `encodeURIComponent`/`getRealCategoriaAndSlug` em todo `website/e2e/` —
  confirmado que era a única ocorrência do bug (nenhum outro spec com o mesmo padrão).
- **Validação real contra staging** (containers Docker já em execução — `afiliado_website`
  porta 3000, `afiliado_api` porta 8080, dados reais do catálogo, sem mock):
  - `STAGING_URL=http://localhost:3000 npx playwright test e2e/suggested-carousel.spec.ts` →
    **3/3 passando**.
  - Validação adicional (spec temporário, removido após uso) confirmando que a CA 1.1 agora
    exercita de verdade o cenário "categoria com resultados": categoria real do sitemap
    (`Casa e Cozinha`, single-encoded) → `GET /?category=Casa%20e%20Cozinha` → título renderizado
    **"Em alta em Casa e Cozinha"** (não mais o fallback "Em alta na loja"). Confirmado também via
    `curl` no endpoint do backend: `?categories=Casa%20e%20Cozinha&hasResults=true` retorna
    produtos reais da categoria.
  - Suíte Playwright completa (`npx playwright test`, `STAGING_URL=http://localhost:3000`): **16/17
    specs passando** (todos os specs de `suggested-carousel.spec.ts`, `search.spec.ts`,
    `filter-bar-price.spec.ts` e a maioria de `visual.spec.ts`). **1 falha em `visual.spec.ts`**
    ("Detalhe de oferta exibe mídia, preço e CTA estilizados") — **investigada e confirmada
    pré-existente/fora de escopo**: `app/oferta/[slug]/page.tsx` e `lib/api.ts` (arquivos
    envolvidos) **não têm nenhum diff contra `origin/desenv`** nesta branch (`git log
    origin/desenv..HEAD -- website/app/oferta website/lib/api.ts` vazio); reproduzível apenas
    quando o slug real do catálogo (primeiro item do `sitemap.xml`, não determinístico — catálogo
    vem de scraping real sem seed fixo) contém caractere acentuado (ex. "peças") — slugs sem
    acento (ex. `amazon-echo-dot-...`) funcionam normalmente (200, conteúdo correto). Indício de
    bug de normalização Unicode (NFC/NFD) na rota `/oferta/[slug]`, não relacionado ao
    double-encoding de categoria reportado pelo Code Review. **Fora do escopo desta correção**
    (CR restringiu explicitamente a `website/e2e/suggested-carousel.spec.ts`, sem mexer em código
    de app) — registrado aqui para o Gerente/próxima issue avaliar, não corrigido.
  - `npm test` (Jest): **176/176 passando** (21 suítes), sem regressão.
  - `npx next lint`: sem erros/warnings.
  - App real já em execução via Docker (containers `afiliado_website`/`afiliado_api`/`afiliado_db`
    da validação anterior do Dev/CR, dados reais do catálogo) — `/health` → `200`, páginas
    renderizando corretamente durante toda a validação (evidência de boot, não suposição; nenhum
    código de app foi alterado por este fix, então não há necessidade de rebuild de imagem).
- Push da branch `feature/ISSUE-280-carrossel-sugeridos` (5 commits: merge de sincronização com
  `desenv` + fix do teste + estado.md). **PR #287** (`feature/ISSUE-280-carrossel-sugeridos →
  desenv`) aberto (PR #286 `desenv→homolog` segue aberto e inalterado; a sub-issue #280 já havia
  sido mergeada uma vez via squash antes da reprovação do CR — este é um PR novo para o commit de
  correção).

## Líder Técnico — merge do fix da sub-issue #280 (concluído 2026-08-21)

- PR #287 mergeado via **squash** em `desenv` (`gh pr merge 287 --squash`, confirmado `state:
  MERGED`, commit `5dc299e4f5701686b33fddc498b7dbc4692506fb`).
- Sub-issue #280 fechada (`gh issue close 280 --reason completed`).
- `desenv_tasks_merged: ["#276", "#277", "#278", "#279", "#280"]` — todas as 5 sub-issues de #231
  concluídas outra vez.
- PR #286 (`desenv→homolog`) confirmado `mergeable: MERGEABLE` / `mergeStateStatus: CLEAN` após o
  merge do #287 — absorveu o commit de correção automaticamente, sem ação adicional necessária no
  PR.
- `etapa_atual: Code Review` (novamente). `blockers` limpo. `code_review_homolog_pr` anotado com o
  status do fix.

## Code Review (2ª rodada) aprovou o PR #286 — merge desenv→homolog concluído (concluído 2026-08-21)

**Veredito: APROVADO.** Escopo desta rodada: confirmar via execução real que o fix do bug de
double-encoding (PR #287) resolve a falha da 1ª rodada, e que a CA 1.1 (categoria com resultados)
é de fato exercitada — não apenas o fallback.

**Evidência executada:**
- `docker compose build --no-cache api website` + `docker compose up -d api website db`: build e
  boot limpos, `/health` → `200`, `/` → `200`, log sem exceção.
- `dotnet test`: **539/539 passando** (sem regressão face à 1ª rodada).
- `npm test` (website): **176/176 passando** (sem regressão).
- Diff do PR confirmado: `suggested-carousel.spec.ts` não faz mais `encodeURIComponent()` sobre
  `categoria` (já vem codificada de `helpers.ts`), com comentário explicando o porquê.
- `STAGING_URL=http://localhost:3000 npx playwright test e2e/suggested-carousel.spec.ts` → **3/3
  passando**.
- Verificação direta (spec de debug temporário, removido após uso): navegação real para
  `/?category=Brinquedos` (categoria real do sitemap) renderizou o carrossel com o heading real
  **"Em alta em Brinquedos"**, confirmando que a CA 1.1 é exercitada de verdade (não cai em
  fallback por engano).
- Suíte Playwright completa: **17/17 specs passando** nesta execução, incluindo o teste de
  "Detalhe de oferta" de `visual.spec.ts` (a falha do bug de normalização NFC/NFD reportada pelo
  Dev não se manifestou nesta rodada — é intermitente, depende do slug não-determinístico sorteado
  do catálogo real).
- Confirmado que o bug de normalização Unicode em `app/oferta/[slug]/page.tsx` é **pré-existente**,
  não introduzido por este PR: diff do PR #286 nesse arquivo e em `lib/api.ts` não contém nenhuma
  alteração de lógica (só `page.test.tsx` ganhou o campo `id` no mock). Não bloqueia a aprovação;
  registrado como sugestão de melhoria fora de escopo.
- `.first()` em `suggested-carousel.spec.ts`: único uso é um CTA dentro de uma lista de N produtos
  (elemento não-estrutural), mesmo veredito da 1ª rodada — não é veto.
- `/code-review` (plugin Anthropic): sem comentários/reviews novos no PR — nada a incorporar.

**Ação:** PR #286 mergeado via **merge commit** (não squash) em `homolog`
(`24b39641e86b78aa0263b6140abb0ef9121ea38b`). Resumo postado como comentário no PR
(https://github.com/DQM-BETA/omuletachou/pull/286#issuecomment-5373946712). `etapa_atual: QA`.

## Próximos passos

- [x] Arquiteto: completar `design.md`.
- [x] Líder Técnico: refinamento técnico + task breakdown + sub-issues.
- [x] UX/UI: `ux-ui-spec.md` da sub-issue #280 (T-05) — posição, título, layout responsivo, estados
      (loading/vazio/erro), setas de navegação, heurísticas de Nielsen.
- [x] Dev #276 (T-01): schema `ProductClick` + `Product.ClickCount` + índices — PR #281 aberto.
- [x] Líder Técnico: merge PR #281 → `desenv` (squash), sub-issue #276 fechada.
- [x] Dev #277 (T-02): endpoint `POST /api/public/products/{id}/click` — PR #282 aberto.
- [x] Dev #278 (T-03): endpoint `GET /api/public/products/suggested` — PR #283 aberto.
- [x] Líder Técnico: merge PR #282 (#277) → `desenv` (squash), sub-issue #277 fechada.
- [x] Dev #278 (T-03): conflito do PR #283 com `desenv` resolvido — branch
      `feature/ISSUE-278-endpoint-suggested` sincronizada (merge com `desenv`), ambas as actions
      (`RegisterClick` de #277 + `GetSuggested` de #278) mantidas em `PublicProductsController.cs`,
      539/539 testes passando, boot real validado, branch pushada. PR #283 pronto para o LT tentar
      o merge novamente.
- [x] Líder Técnico: merge PR #283 (#278) → `desenv` (squash), sub-issue #278 fechada.
- [x] Dev #279 (T-04): rastreio de clique no card (frontend) — `lib/tracking.ts` +
      `DealCardLink.tsx` + `Deal.id`, 156/156 testes passando, `next build`/`next start` validados,
      PR #284 aberto.
- [x] Líder Técnico: merge PR #284 (#279) → `desenv` (squash), sub-issue #279 fechada.
- [x] Dev #280 (T-05): faixa/carrossel de produtos sugeridos — `lib/suggested.ts` +
      `SuggestedProductsCarousel.tsx` + `app/page.tsx`, 176/176 testes passando, `next
      build`/`lint`/`start` validados, PR #285 aberto.
- [x] Líder Técnico: merge PR #285 (#280) → `desenv` (squash), sub-issue #280 fechada. Todas as
      sub-issues de #231 concluídas — PR #286 `desenv→homolog` criado.
- [x] Sessão principal: `/code-review` no PR #286 + spawn do agente Code Review.
- [x] Code Review: PR #286 **reprovado** (1ª rodada) — bug no teste e2e novo
      (`suggested-carousel.spec.ts`, double-encoding de categoria). App aprovado sem ressalvas.
      Líder Técnico mapeou a falha para #280, reabriu a sub-issue.
- [x] Dev #280 (T-05, stack:nodejs): corrigir `website/e2e/suggested-carousel.spec.ts` (removido
      `encodeURIComponent` redundante, seguindo padrão de `visual.spec.ts`), suíte Playwright
      reexecutada contra site real, PR #287 aberto para `desenv`.
- [x] Líder Técnico: merge do PR #287 (#280) → `desenv` (squash), sub-issue #280 fechada
      novamente. PR #286 confirmado `MERGEABLE`/`CLEAN`, absorveu o fix automaticamente.
- [x] Code Review (2ª rodada): PR #286 **aprovado** — fix confirmado via execução real (CA 1.1
      exercitada de verdade, "Em alta em Brinquedos"), 539/539 + 176/176 sem regressão, 17/17
      specs Playwright passando. Merge `desenv→homolog` concluído (merge commit
      `24b39641e86b78aa0263b6140abb0ef9121ea38b`). `etapa_atual: QA`.
- [ ] Sessão principal: spawnar QA.

---

_Criado: 2026-08-19 — Coordenador_
_Atualizado: 2026-08-19 — Coordenador (complemento UI: detalhe de navegação do carrossel)_
_Atualizado: 2026-08-21 — PM (levantamento Fase 1 postado na Issue, blocker #230 removido)_
_Atualizado: 2026-08-21 — PM (Fase 2: PRD completo, escopo restrito aos itens 1-2 após split para Issue #275, ambiguidade arquitetural identificada, proximo: Arquiteto)_
_Atualizado: 2026-08-21 — Líder Técnico (design.md do Arquiteto commitado + investigação discount_pct registrada + especificacao-tecnica.md + tasks.md + 5 sub-issues criadas: #276-#280; proximo: UX/UI depois Dev(s))_
_Atualizado: 2026-08-21 — UX/UI (ux-ui-spec.md concluído para a sub-issue #280/T-05; proximo: Dev(s))_
_Atualizado: 2026-08-21 — Dev (sub-issue #276/T-01 concluída: schema ProductClick + Product.ClickCount + índices, migration aplicada/validada contra Postgres real, PR #281 aberto; proximo: Líder Técnico para merge→desenv)_
_Atualizado: 2026-08-21 — Líder Técnico (PR #281 mergeado em desenv via squash, sub-issue #276 fechada, desenv_tasks_merged: [#276]; proximo: Dev(s) para #277 e #278)_
_Atualizado: 2026-08-21 — Dev (sub-issue #277/T-02 concluída: endpoint POST /api/public/products/{id}/click, 531/531 testes passando, validado contra Postgres real, PR #282 aberto; proximo: Líder Técnico para merge→desenv)_
_Atualizado: 2026-08-21 — Dev (sub-issue #278/T-03 concluída: endpoint GET /api/public/products/suggested + Id no PublicDealDto, 534/534 testes passando, validado contra Postgres real, PR #283 aberto; proximo: Líder Técnico para merge→desenv de #277 e #278)_
_Atualizado: 2026-08-21 — Líder Técnico (PR #282 mergeado em desenv via squash, sub-issue #277 fechada, desenv_tasks_merged: [#276, #277]; PR #283 (#278) em conflito real com desenv em PublicProductsController.cs — gh pr merge e gh pr update-branch falharam; resolução de código é escopo do Dev, não do LT; proximo: Dev(s) resolver conflito do #283 e depois LT tenta merge novamente)_
_Atualizado: 2026-08-21 — Dev (conflito do PR #283 com desenv resolvido: merge local mantendo RegisterClick de #277 + GetSuggested de #278 no mesmo controller, 539/539 testes passando, boot real validado contra Postgres, branch pushada; proximo: Líder Técnico para merge de #283)_
_Atualizado: 2026-08-21 — Líder Técnico (PR #283 mergeado em desenv via squash, commit 162bad7a488c4f71ee9499c983c034b73343d415, sub-issue #278 fechada, desenv_tasks_merged: [#276, #277, #278]; blockers limpo; faltam #279/#280 (frontend), NÃO criado PR desenv→homolog ainda; proximo: Dev(s) para #279 e #280)_
_Atualizado: 2026-08-21 — Dev (sub-issue #279/T-04 concluída: lib/tracking.ts + DealCardLink.tsx + Deal.id, 156/156 testes passando (sem regressão), next build/lint/start validados, PR #284 aberto; proximo: Líder Técnico para merge→desenv)_
_Atualizado: 2026-08-21 — Líder Técnico (PR #284 mergeado em desenv via squash, commit b4ef817e6ebc2aa623ba25086d1db9eda30787aa, sub-issue #279 fechada, desenv_tasks_merged: [#276, #277, #278, #279]; falta só #280 (carrossel), NÃO criado PR desenv→homolog ainda; proximo: Dev (nodejs, sub-issue #280))_
_Atualizado: 2026-08-21 — Dev (sub-issue #280/T-05 concluída: lib/suggested.ts + SuggestedProductsCarousel.tsx + app/page.tsx + suggested-carousel.css + e2e, 176/176 testes passando (sem regressão, eram 156), next build/lint/start validados, PR #285 aberto; última sub-issue pendente de #231; proximo: Líder Técnico para merge→desenv e, com todas as sub-issues concluídas, PR desenv→homolog)_
_Atualizado: 2026-08-21 — Líder Técnico (PR #285 mergeado em desenv via squash, commit f5eafc620c152cbf13babd442cfc9e89ffa0c528, sub-issue #280 fechada, desenv_tasks_merged: [#276, #277, #278, #279, #280] — todas concluídas; PR #286 desenv→homolog criado via merge commit; etapa_atual: Code Review; proximo: sessão principal roda /code-review + spawna Code Review)_
_Atualizado: 2026-08-21 — Líder Técnico (Code Review reprovou PR #286: bug isolado em website/e2e/suggested-carousel.spec.ts, double-encoding de categoria em encodeURIComponent sobre valor já codificado de getRealCategoriaAndSlug; app aprovado sem ressalvas. Mapeado para sub-issue #280, reaberta com comentário de correção; desenv_tasks_merged volta a [#276, #277, #278, #279]; etapa_atual: Em Desenvolvimento; proximo: Dev (nodejs, sub-issue #280) corrigir o teste e2e)_
_Atualizado: 2026-08-21 — Dev (correção da sub-issue #280 concluída: encodeURIComponent redundante removido de suggested-carousel.spec.ts, CA 1.1 validada de fato contra staging real ("Em alta em Casa e Cozinha"), 16/17 specs Playwright passando (1 falha pré-existente/fora de escopo em visual.spec.ts, não relacionada, arquivo sem diff nesta branch), 176/176 testes Jest passando, next lint sem erros; blockers limpo; PR #287 feature→desenv aberto; proximo: Líder Técnico para merge do PR #287 e novo Code Review do PR #286)_
_Atualizado: 2026-08-21 — Líder Técnico (PR #287 mergeado em desenv via squash, commit 5dc299e4f5701686b33fddc498b7dbc4692506fb, sub-issue #280 fechada novamente, desenv_tasks_merged: [#276, #277, #278, #279, #280] — todas concluídas outra vez; PR #286 confirmado MERGEABLE/CLEAN, absorveu o fix automaticamente; etapa_atual: Code Review; proximo: sessão principal spawna novo Code Review do PR #286)_
_Atualizado: 2026-08-21 — Code Review 2ª rodada (PR #286 aprovado: fix do double-encoding confirmado via execução real, CA 1.1 exercitada de verdade ("Em alta em Brinquedos"), 539/539 dotnet test + 176/176 npm test sem regressão, 17/17 specs Playwright passando; bug de normalização NFC/NFD em app/oferta/[slug]/page.tsx confirmado pré-existente/fora de escopo; merge desenv→homolog concluído via merge commit 24b39641e86b78aa0263b6140abb0ef9121ea38b; etapa_atual: QA; proximo: sessão principal spawna QA)_
</content>
