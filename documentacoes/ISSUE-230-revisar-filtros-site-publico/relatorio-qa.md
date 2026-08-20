# Relatório de QA — Issue #230: Revisar filtros da tela de produtos do site público (desconto, preço)

## Status: **APROVADO**

## Contexto da validação
- Branch validada: `homolog` (sincronizada via `git fetch` + `git pull origin homolog`).
- Commit confirmado em `homolog`: `7d343cd` (Merge pull request #265 from DQM-BETA/desenv) — presente em `git log --oneline -5` no topo, conforme esperado.
- Sub-issues: #261 (T-01, remoção do desconto) e #262 (T-02, fix do slider + campos min/max), ambas mergeadas em `desenv` e promovidas via PR #265.
- Validação executada com a stack Docker real (`docker compose build --no-cache website api` + `docker compose up -d db api website`), a partir do código de `homolog`, com dados reais do catálogo (105 itens na API).

## Testes automatizados

### Jest (unitário)
- `npm test -- --coverage`: **130/130 passando (100%)**.
- Cobertura global: **92.77% stmts / 88.55% branch / 91.5% funcs / 94.66% lines** — acima do threshold do projeto (80%).
- `FilterBar.tsx`: 90.26% stmts / 86.79% branch.

### Type check
- `npx tsc --noEmit` isolado aponta erros pré-existentes de configuração (`toBeInTheDocument`/`toHaveAttribute`/etc. não reconhecidos pelo `tsconfig.json` — falta `@testing-library/jest-dom` em `compilerOptions.types`). **Não é regressão desta issue**: o mesmo erro ocorre em arquivos de teste não tocados por #230 (`Header.test.tsx`, `DealDetail.test.tsx`, `PushSubscriptionManager.test.tsx`, `lib/push.test.ts`).
- Confirmação de que o código de produção está tipado corretamente: `next build` (dentro do Docker build) roda "Linting and checking validity of types" como parte do build e **passou sem erro** — o type-check real do projeto (via next, não tsc standalone) está limpo.

### Build e boot real (Docker)
- `docker compose build --no-cache website api`: sucesso (build a partir de `homolog`, sem cache).
- `docker compose up -d db api website`: `afiliado_db` healthy, `afiliado_api` healthy, `afiliado_website` up.
- `curl http://localhost:3000` → **200**. `curl http://localhost:8080/health` → **200**.
- Logs da API durante os testes: sem exceções; filtro `minPrice` aplicado corretamente na query SQL real (`WHERE p.sale_price >= @__minPrice_Value_0`), confirmando integração front→API→banco ponta a ponta.

### E2E Playwright (`npm run test:visual`)
Rodado com `STAGING_URL=http://localhost:3000` (contra o container Docker real, não `npm run dev`) e `SCREENSHOTS_DIR={docs_path}/screenshots`:

```
9 passed (12.0s)
```
Inclui os 4 testes de `filter-bar-price.spec.ts` (CA 2.1, 2.2, 2.4, 3.1/3.3) e os 5 de `visual.spec.ts` (home, categoria, detalhe de oferta, filter-bar mobile drawer, filter-bar desktop).

**Screenshots arquivadas em `documentacoes/ISSUE-230-revisar-filtros-site-publico/screenshots/`** (path correto, não na raiz do repo): `home.png`, `categoria.png`, `deal-detail.png`, `filter-bar-desktop.png`, `filter-bar-mobile-drawer.png`, `filter-bar-mobile-summary.png`.

## Gate visual (inspeção manual das screenshots)
- Header visível exatamente 1x em todas as 6 telas — OK.
- Nenhum componente estrutural duplicado — OK.
- Seletor "Desconto mínimo" ausente em todas as telas (home, categoria, filter-bar desktop/mobile) — OK.
- Barra de filtros íntegra: Categoria, Subcategoria, Preço (slider + campos "Mín."/"Máx." digitáveis), Ordenar por — sem espaço vazio quebrado no lugar do filtro removido — OK.
- Layout condiz com o padrão visual já estabelecido do projeto (cards, botões vermelhos "Ver oferta", tipografia) — sem tela nova/dedicada para este item (UX/UI avaliado como desnecessário no refinamento, confirmado: campos min/max reaproveitam tokens existentes).
- Dark mode: não se aplica — o site público não tem dark mode implementado (fora de escopo desta issue, não introduzido nem regressivo).

## Critérios de aceite validados

| CA | Descrição | Evidência | Resultado |
|---|---|---|---|
| 1.1 | Seletor "Desconto mínimo" removido, layout íntegro | Screenshots `filter-bar-desktop.png`/`filter-bar-mobile-drawer.png`/`filter-bar-mobile-summary.png` — sem seletor, sem espaço quebrado | OK |
| 1.2 | Sem código órfão | `grep -rniE "minDiscount\|DiscountGroup\|Desconto mínimo\|10%+\|30%+\|50%+"` em `website/` (exceto `node_modules`) — só ocorrências em testes negativos (confirmam ausência) e 1 comentário obsoleto em `.next/static` (artefato de build, não fonte) | OK |
| 2.1 | Arrasto lento aplica filtro sem erro | Playwright `filter-bar-price.spec.ts` "CA 2.1" passou contra app real | OK |
| 2.2 | Clique único no trilho aplica sem erro | Playwright `filter-bar-price.spec.ts` "CA 2.2" passou contra app real | OK |
| 2.3 | Valores extremos aceitos sem erro | `clampToPriceRange` (PRICE_MIN=0/PRICE_MAX=5000) cobre os limites; Jest CA 3.7 exercita o clamp | OK |
| 2.4 | Arrasto rápido (150 eventos) não navega para página de erro | Playwright `filter-bar-price.spec.ts` "CA 2.4" — 150 eventos `input` em sucessão + `mouseup`, `pageErrors` vazio, `[data-testid="app-error"]` ausente, grid/estado vazio segue visível — passou contra o container Docker real | OK |
| 2.5 | Causa raiz documentada, correção não só suprime sintoma | `design.md` §"Investigação do bug do item 2" documenta a cadeia causal (rajada de `router.push()` sem debounce → excede throttle de `history.pushState` do Chromium → `SecurityError` não tratado → sem `error.tsx` → fallback genérico). Código inspecionado: `commitPriceParams` usa `router.replace` só ao soltar o gesto/debounce (não a cada evento); `router.push` seguiu em uso apenas para category/subcategory/sort/clear (eventos discretos, fora do escopo do bug) | OK |
| 3.1 | Digitar mínimo move o slider e aplica filtro | Playwright "CA 3.1/3.3" passou; Jest "CA 3.1/3.2" | OK |
| 3.2 | Digitar máximo move o slider e aplica filtro | Jest "CA 3.1/3.2" (mesmo mecanismo bidirecional) | OK |
| 3.3 | Arrastar slider atualiza campos de texto, nunca divergem | Jest "CA 3.3" | OK |
| 3.4 | min > max bloqueado com mensagem clara | Jest "CA 3.4: digitar um mínimo maior que o máximo é bloqueado com mensagem clara e não commita" | OK |
| 3.5 | Valor negativo não aplicado, com indicação | Jest "CA 3.5: valor negativo não é aplicado — normalizado para 0 com feedback visível" | OK |
| 3.6 | Entrada vazia/não numérica não gera exceção | Jest "CA 3.6: campo vazio ao perder o foco não lança exceção e reverte ao último valor válido" | OK |
| 3.7 | Fora dos limites do catálogo tratado sem erro | Jest "CA 3.7: valor digitado acima do limite do catálogo é clampado sem erro" | OK |
| `error.tsx` | Defesa em profundidade funcional | `website/app/error.tsx` lido — Client Component, `data-testid="app-error"`, mensagem amigável + botão "Tentar novamente" (`reset()`), conforme contrato do App Router | OK |
| Não-regressão | Categoria, subcategoria, ordenação seguem funcionais | Playwright `visual.spec.ts` (5/5, incluindo os 2 testes de filter-bar desktop/mobile) + screenshots confirmam os 4 controles (Categoria/Subcategoria/Preço/Ordenar) presentes e funcionais | OK |

## Issues encontradas
Nenhuma. Todos os 17 critérios de aceite (itens 1-3, incluindo o caso obrigatório 2.4 apontado pelo Gerente) passaram na validação integrada contra a aplicação real.

## Observação não bloqueante
`tsc --noEmit` standalone falha por configuração pré-existente (`@testing-library/jest-dom` ausente em `tsconfig.json > compilerOptions.types`), afetando arquivos de teste não relacionados a esta issue. Não é regressão desta mudança — registrado para eventual limpeza técnica futura (fora do escopo de #230).
