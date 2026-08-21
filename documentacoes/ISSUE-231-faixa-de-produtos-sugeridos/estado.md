---
issue: 231
titulo: feat: rastreio de cliques + faixa de produtos sugeridos (site público)
etapa_atual: Em Desenvolvimento
ultimo_agente: ux-ui
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
desenv_tasks_merged: []
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

## Próximos passos

- [x] Arquiteto: completar `design.md`.
- [x] Líder Técnico: refinamento técnico + task breakdown + sub-issues.
- [x] UX/UI: `ux-ui-spec.md` da sub-issue #280 (T-05) — posição, título, layout responsivo, estados
      (loading/vazio/erro), setas de navegação, heurísticas de Nielsen.
- [ ] Dev(s): implementar T-01 a T-05 (ver `tasks.md` para ordem de dependência/merge; T-05 consome
      `ux-ui-spec.md`).

---

_Criado: 2026-08-19 — Coordenador_
_Atualizado: 2026-08-19 — Coordenador (complemento UI: detalhe de navegação do carrossel)_
_Atualizado: 2026-08-21 — PM (levantamento Fase 1 postado na Issue, blocker #230 removido)_
_Atualizado: 2026-08-21 — PM (Fase 2: PRD completo, escopo restrito aos itens 1-2 após split para Issue #275, ambiguidade arquitetural identificada, proximo: Arquiteto)_
_Atualizado: 2026-08-21 — Líder Técnico (design.md do Arquiteto commitado + investigação discount_pct registrada + especificacao-tecnica.md + tasks.md + 5 sub-issues criadas: #276-#280; proximo: UX/UI depois Dev(s))_
_Atualizado: 2026-08-21 — UX/UI (ux-ui-spec.md concluído para a sub-issue #280/T-05; proximo: Dev(s))_
