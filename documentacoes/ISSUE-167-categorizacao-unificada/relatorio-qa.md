# Relatório QA — ISSUE-167: Categorização unificada + remoção de distinção de plataforma

**Branch validada:** `homolog` @ commit `9cd7154` (fast-forward confirmado via `git fetch && git checkout homolog && git pull origin homolog`; log mostra `9cd7154` como merge do PR #176).
**PR:** #176 (`desenv` → `homolog`, merge commit).
**Sub-issues:** #168, #169, #170, #171 (todas mergeadas em `desenv` antes do PR #176).
**Validação independente do QA** — evidência própria, não reaproveita o comentário de Code Review do PR #176.

## Veredito: ✅ APROVADO (27/27 critérios)

---

## 1. Testes automatizados

| Suíte | Resultado |
|---|---|
| `dotnet test` (backend) | **414/414 passando**, 0 falhas |
| `npm test` (website) | **102/102 passando**, 0 falhas (15 suítes) |
| `dotnet test --filter ClaudeBudgetServiceIntegrationTests` (reexecutado isoladamente) | **3/3 passando** — Testcontainers.PostgreSql, prova de atomicidade real contra Postgres |
| `npx tsc --noEmit` (website) | Falhas pré-existentes (matchers `@testing-library/jest-dom` ausentes de `tsconfig.json > types`) — **não é regressão desta PR** (tsconfig.json não foi tocado pelo diff #176; a lacuna já existia desde ISSUE-18/117). Registrado como observação, não bloqueante. |

## 2. Gate Visual (Playwright — `npm run test:visual`)

Rodado com `SCREENSHOTS_DIR={docs_path}/screenshots`, stack Next.js dev + API real dockerizada (ver seção 3). **5/5 testes passando.** Screenshots inspecionados individualmente (paths relativos a `docs_path`):

- `screenshots/home.png` — grid de ofertas, header "O Mulet Achou" 1x, `FilterBar` (resumo mobile "☰ Filtros" + "Relevância"), sem chip/badge de plataforma.
- `screenshots/categoria.png` — página de categoria "Eletrônicos", header 1x, grid filtrado corretamente.
- `screenshots/deal-detail.png` — detalhe do produto, categoria "ELETRÔNICOS" exibida, header 1x, sem badge de plataforma, seção "Mais ofertas".
- `screenshots/filter-bar-mobile-summary.png` / `filter-bar-mobile-drawer.png` — resumo compacto (`.filter-bar__summary`) e drawer completo (Categoria/Subcategoria/Preço/Desconto mínimo/Limpar filtros/Ver resultados) sobrepondo com overlay, sem overflow horizontal.
- `screenshots/filter-bar-desktop.png` (1280px) — os 5 controles em linha única (Categoria, Subcategoria, Preço, Desconto mínimo, Ordenar por) + "Limpar filtros", grid de 12 produtos, paginação.

**Checklist obrigatório:**
- [x] Header visível exatamente 1x em cada tela (confirmado via inspeção visual + `Header.tsx` tem um único `<header className="site-header">`; a Home também renderiza um `<h1>O Mulet Achou</h1>` de hero/tagline — elemento distinto do `<header>`, não é duplicação estrutural).
- [x] Nenhum componente estrutural duplicado (Nav/Sidebar/container).
- [ ] Footer — **N/A**: não existe componente de footer no site (confirmado — nenhum arquivo `*footer*` no repo; pré-existente, fora do escopo desta issue).
- [x] Layout condiz com `ux-ui-spec-filterbar.md` (paleta vermelho/branco existente, grid 8pt, classes BEM `filter-bar__*`, estados de dropdown dependente, drawer mobile, linha única desktop).
- [ ] Dark mode — **N/A**: aplicação não implementa dark mode (sem `prefers-color-scheme`/tokens de tema no CSS; pré-existente, fora do escopo desta issue).

## 3. Validação integrada (d3) — stack real via Docker

`docker compose up -d --build db api` (Postgres 16 + API .NET 8 reais, migration `20260814193834_AddSubcategoryAndCategorizationBudget` aplicada automaticamente no boot — `db.Database.Migrate()`). `/health` retornou `200 healthy`. 14 produtos semeados via SQL direto no Postgres do container, cobrindo as 9 categorias/13 subcategorias do dicionário + 2 produtos "Geral" (sem match) + 1 produto não-publicado (para confirmar visibilidade). Website rodado localmente (`npm run dev`) apontando `API_INTERNAL_URL=http://localhost:8080` (porta exposta via `docker-compose.override.yml` temporário, removido ao final).

Endpoints exercitados via `curl` contra a API real e via HTML SSR real do site:
- `GET /api/public/deals` sem filtros → 13 itens (produto não-publicado corretamente ausente), ordenado por `AiScore desc`.
- `GET /api/public/deals?category=Eletrônicos&subcategory=Celulares e Smartphones` → 1 item exato.
- `GET /api/public/deals?minPrice=100&maxPrice=500&minDiscount=30` → 5 itens, todos dentro da faixa.
- `GET /api/public/deals?sort=price_asc` / `sort=discount_desc` → ordenação confirmada item a item.
- `GET /api/public/deals?category=CategoriaInexistente` → `200 {"items":[],...}` (nunca 400/500).
- `GET /api/public/categories` → árvore `Category > [Subcategory]` com contagens corretas (9 categorias + "Geral" com 2, subcategorias corretas por categoria). Mojibake observado inicialmente no console Windows foi confirmado ser artefato do terminal (decodificação explícita em UTF-8 via Python mostrou `Eletrodomésticos`, `Climatização`, `Áudio` etc. corretos) — **não é bug da API**.
- `GET /api/public/deals/{slug}` → JSON com chaves `['affiliateLink','category','collectedAt','discountPct','mediaLocalPath','mediaUrl','originalPrice','salePrice','slug','subcategory','title']` — **`platform` ausente**.
- `GET /api/public/deals/category/{categoria}` (rota antiga) → **404** confirmado.
- Produto não-publicado via slug → **404** confirmado (não vazado ao público).
- HTML SSR real (`curl` na Home/categoria com querystrings de filtro/sort) → grid filtrado/ordenado corretamente refletido no HTML renderizado; nenhuma menção a "Amazon"/"MercadoLivre"/"Shopee" em nenhuma página pública (Home ou detalhe).
- Estado vazio: `/?minPrice=99999` → "Nenhuma oferta encontrada com esses filtros." + CTA "Ver todas as ofertas"; `/categoria/categoria-inexistente-teste-qa` → "Nenhuma oferta encontrada nesta categoria." — ambos 200, sem erro/quebra de layout.

## 4. Orçamento de IA — simulação de estouro do teto

Confirmado `claude.monthly_budget_limit_brl = 30` (default, seed da migration). Simulado ao vivo via `UPDATE app_settings` no Postgres do container:
- `spend_brl = 999.99` no mês corrente → query equivalente à lógica de `IsCategorizationBudgetAvailableAsync` (`spend < limit`) retornou **`false`** (orçamento indisponível).
- Incremento atômico simulado (`UPDATE...CASE` idêntico ao `ClaudeBudgetService.RecordUsageAsync`): 2 chamadas de +R$2,50 somaram corretamente a R$5,00 (prova de que o contador acumula por chamada bem-sucedida).
- Reset mensal: usage do mês anterior (`2026-07`, R$999) + nova chamada no mês corrente → reinicializado para R$3,00 (não carrega saldo do mês anterior), confirmando o reset lazy (CA 4.5).
- Código confirmado (leitura + testes de integração já passando): `ProcessorJob.EnsureCategoryFallbackAsync` só chama `ClassifyCategoryAsync` quando `Category == "Geral"`; `ClassifyCategoryAsync` checa `IsCategorizationBudgetAvailableAsync` **antes** de qualquer chamada HTTP e retorna `null` (sem exceção) quando indisponível — produto permanece "Geral".
- `ScoreProductAsync`/`GenerateCaptionAsync` **não referenciam** `IClaudeBudgetService` em nenhum ponto do código (`grep` confirmou) — scoring e legenda não são afetados pelo teto (CA 4.4).

Nota: não foi possível disparar o `ProcessorJob` real fim-a-fim contra a API do Claude (sem `claude.api_key` real neste ambiente de QA, mesma limitação enfrentada pelos Devs nas validações anteriores). A validação acima combina simulação SQL live (mesma lógica exata do serviço) + testes de integração real contra Postgres (Testcontainers) reexecutados nesta sessão — evidência suficiente e específica para os 5 critérios da seção 4.

## 5. Tabela completa — 27 critérios

| # | Cenário | Resultado | Evidência |
|---|---|---|---|
| 1.1 | Migration aditiva, produtos existentes ilesos | ✅ | Migration `AddSubcategoryAndCategorizationBudget` aplicada automaticamente no boot do container contra Postgres real, sem erro; `ALTER TABLE ADD COLUMN subcategory ... NULL` confirmado via `\d products` |
| 1.2 | `Category`/`Subcategory` livres, sem constraint | ✅ | INSERT ao vivo com `category='CategoriaTotalmenteNova123'`, `subcategory='SubcategoriaInventada456'` — sucesso sem erro de constraint |
| 2.1 | Dicionário roda em `CollectAsync`, todos os collectors, sem IA | ✅ | `grep` confirma `CategoryDetector.Detect()` chamado nos 3 collectors (Amazon/MercadoLivre/Shopee); nenhuma referência a `IAiService`/`ClassifyCategory` nesse trecho, só `ScoreProductAsync` (scoring, não categorização) |
| 2.2 | Sem match → "Geral"/null | ✅ | Produtos "Produto Genérico XYZ 3000" e "Gadget Misterioso Modelo Alpha" (sem keywords) persistidos com `category=Geral`, `subcategory=NULL`; árvore de categorias confirma `Geral: count=2, subcategories=[]` |
| 2.3 | Cobertura das 9 categorias, testes por categoria/subcategoria | ✅ | `CategoryDetector.cs` mapeia 9 categorias × 4-5 subcategorias cada; `CategoryDetectorTests.cs` com 33 `InlineData` + 6 métodos de teste, todos passando |
| 3.1 | Fallback acionado (Queued + Geral + orçamento OK) | ✅ | Código: `EnsureCategoryFallbackAsync` só roda se `Category=="Geral"`; `Status==Queued` garantido pela query do topo do job; roda antes de `EnsureSlug` (confirmado na ordem do método `ExecuteAsync`) |
| 3.2 | Fallback NÃO acionado — produto não aprovado | ✅ | Query do topo de `ProcessorJob.ExecuteAsync` filtra `Status == Queued` — produtos rejeitados nunca entram no loop |
| 3.3 | Fallback NÃO acionado — dicionário já classificou | ✅ | `EnsureCategoryFallbackAsync` retorna cedo (`return`) se `Category != "Geral"` |
| 3.4 | `ScoreProductAsync` não ganhou responsabilidade de categoria | ✅ | `ProductScore(int Score, string Reason, bool Approve)` — sem campos de categoria/`needsAiCategory` |
| 4.1 | Teto default R$30 | ✅ | `SELECT value FROM app_settings WHERE key='claude.monthly_budget_limit_brl'` → `30` |
| 4.2 | Custo somado ao contador por chamada | ✅ | Simulação live do `UPDATE...CASE` atômico: 2× +R$2,50 → R$5,00 acumulado corretamente |
| 4.3 | Camada 2 desativada ao estourar teto | ✅ | Simulação live: `spend=999.99 >= limit=30` → `budget_available=false`; código confirma `ClassifyCategoryAsync` retorna `null` sem chamar a API nesse caso |
| 4.4 | Scoring/legenda não afetados pelo teto | ✅ | `grep` confirma `ScoreProductAsync`/`GenerateCaptionAsync` não referenciam `IClaudeBudgetService` |
| 4.5 | Reset mensal | ✅ | Simulação live: usage de mês anterior (`2026-07`, R$999) + nova chamada no mês corrente → reinicializa para R$3,00 (não soma ao saldo antigo) |
| 5.1 | `Platform` ausente do DTO público | ✅ | `GET /api/public/deals` e `GET /api/public/deals/{slug}` — chave `platform` ausente em 100% do JSON (confirmado programaticamente, não só visualmente) |
| 5.2 | `Platform` preservado no DTO interno | ✅ | `grep -n "Platform" ProductDtos.cs` → 2 ocorrências (`ProductDto`/dto de update), campo mantido |
| 5.3 | `AffiliateLink` não afetado | ✅ | `ProcessorJob.EnsureAffiliateLinkAsync` (linha 195) continua checando `product.Platform != Platform.MercadoLivre` — lógica idêntica, não tocada por esta PR |
| 6.1 | Filtros opcionais, comportamento default preservado | ✅ | `GET /api/public/deals` sem filtros → 13 itens, ordenado por `AiScore desc` |
| 6.2 | Filtro categoria+subcategoria | ✅ | `?category=Eletrônicos&subcategory=Celulares e Smartphones` → exatamente 1 item correto |
| 6.3 | Filtro preço+desconto mínimo | ✅ | `?minPrice=100&maxPrice=500&minDiscount=30` → 5 itens, todos dentro da faixa (`salePrice` 150-320, `discountPct` ≥30) |
| 6.4 | Ordenação via `sort` | ✅ | `sort=price_asc` (crescente confirmado item a item) e `sort=discount_desc` (decrescente confirmado) |
| 6.5 | Ordenação padrão inalterada | ✅ | Sem `sort` → `AiScore desc` (mesmo resultado do CA 6.1) |
| 6.6 | Filtro não reconhecido → 200 vazio | ✅ | `?category=CategoriaInexistente` → `200 {"items":[],"totalItems":0}` |
| 6.7 | Árvore de categorias com contagem | ✅ | `GET /api/public/categories` → 9 categorias + "Geral", contagens batendo com os produtos semeados (confirmado decodificação UTF-8 explícita) |
| 7.1 | Dropdowns dependentes | ✅ | Código (`FilterBar.tsx` `subcategoryOptions`/`subcategoryDisabled`) + visual (screenshot desktop mostra "Subcategoria" — "Escolha uma categoria" quando nenhuma categoria selecionada) |
| 7.2 | Slider de preço + botões de desconto combinados | ✅ | Visual (slider R$0–R$5.000+, botões 10%+/30%+/50%+) + `updateParams` combina múltiplas chaves na mesma URLSearchParams |
| 7.3 | Seletor de ordenação sem alterar filtros | ✅ | `handleSortChange` só atualiza a chave `sort`, preservando as demais via `URLSearchParams` existente; confirmado ao vivo `?sort=price_asc` mantendo resultado filtrável |
| 7.4 | Sem badge de plataforma | ✅ | `grep -i "amazon\|mercadolivre\|shopee"` no HTML renderizado da Home e do detalhe → **vazio** (nenhuma menção) |
| 7.5 | Estado sem resultados | ✅ | `/?minPrice=99999` e `/categoria/categoria-inexistente-teste-qa` → mensagens claras, sem erro/quebra, HTTP 200 |

## 6. Observações não-bloqueantes

1. **`tsc --noEmit` com erros pré-existentes** em arquivos `*.test.tsx` (matchers do `@testing-library/jest-dom` não declarados em `tsconfig.json > types`). Confirmado que `tsconfig.json` não foi tocado pelo diff do PR #176 (gap já existia desde ISSUE-18/117). Não bloqueia — `jest` (runtime real dos testes) passa 100%.
2. **Footer / dark mode**: aplicação não implementa nenhum dos dois (pré-existente, fora do escopo desta issue) — marcados N/A no Gate Visual, não reprovação.
3. **Infra — Docker Desktop crashou durante a etapa de limpeza final** (`docker compose down -v`), com erro `dockerInference: The file cannot be accessed by the system` — mesmo problema de reparse-points órfãos já diagnosticado em `.claude/melhorias/2026-07-30-devops-docker-desktop-reparse-points-orphaned.md` (requer reboot completo do host; fora da alçada do QA). **Isso ocorreu DEPOIS de toda a validação funcional/integrada/visual já ter sido concluída com sucesso** — não afeta o veredito dos 27 critérios, todos validados com a stack real rodando. Containers `afiliado_db`/`afiliado_api` e arquivos `.env`/`docker-compose.override.yml` locais foram removidos manualmente onde possível; a limpeza completa via `docker compose down -v` fica pendente até o próximo restart do Docker Desktop (não requer ação de código).

## 7. Estado final do repositório
`repo_path` deixado em `desenv` (não `homolog`), conforme instruído. Nenhum commit criado por este QA.
