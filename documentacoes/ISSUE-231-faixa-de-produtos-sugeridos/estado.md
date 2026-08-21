---
issue: 231
titulo: feat: rastreio de cliques + faixa de produtos sugeridos (site público)
etapa_atual: Em Desenvolvimento
ultimo_agente: lider-tecnico
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
sub_issues_frontend:
  T-04: "#279"
  T-05: "#280"
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
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
- [ ] Dev #280 (T-05): faixa/carrossel de produtos sugeridos — pendente (desbloqueada, depende de
      `DealCardLink.tsx` já em `desenv`).
- [ ] Líder Técnico: quando #280 estiver mergeada, criar PR `desenv→homolog`.

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
