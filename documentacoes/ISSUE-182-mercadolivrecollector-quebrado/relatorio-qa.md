# Relatório de QA — Issue-pai #182 (MercadoLivreCollector quebrado — reconstruir com Highlights API)

**Status: APROVADO** (2ª rodada)

**Branch validada:** `homolog` (commit `f4088a82daeec9a266ee00b78ac946e0e49643df`, merge do PR #189 +
PR #194 + PR #197, confirmado em `git log --oneline -8` antes de qualquer teste — commit no topo de
`homolog` após `git fetch`/`git pull`).

**Ambiente:** stack local via `docker compose` (db + api + dashboard), build `--no-cache`, `.env` local com
credenciais de teste (`operador@omuletachou.local`) e `CLAUDE_API_KEY` real configurada.

---

## 1. Testes automatizados (pré-requisito antes da inspeção qualitativa)

| Suíte | Resultado | Evidência |
|---|---|---|
| `dotnet build -c Release` | OK | 0 erros |
| `dotnet test -c Release --no-build` (backend) | **436/436** ✅ | Docker Desktop disponível — inclui os 3 testes de integração `ClaudeBudgetServiceIntegrationTests` via Testcontainers/Postgres real |
| `npx ng test --watch=false --browsers=ChromeHeadless` (dashboard) | **124/124** ✅ | Executado nesta sessão, inclui os 4 testes novos do fix #195 (linhas 260-401 de `mercadolivre-links.component.spec.ts`, cobrindo explicitamente o cenário "painel de skipped precisa continuar no DOM mesmo com o import-card pai sem produtos pendentes") |
| `npx tsc --noEmit -p tsconfig.app.json` (dashboard) | OK | Sem erros de tipo |

Todos os testes passaram — prosseguiu-se para a inspeção qualitativa + validação integrada.

## 2. Gate Visual (d2)

`dashboard/package.json` **não tem** o script `npm run test:visual` (confirmado por leitura direta —
mesma lacuna já registrada na 1ª rodada e em `.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md`,
ainda `status: pendente` para `dashboard/`). Por regra estrita do processo: **E2E/screenshots formalmente
N/A (projeto/dashboard sem `test:visual`)**.

Dado o histórico de falso-positivo do Gate Visual documentado na melhoria acima, esta sessão novamente
usou o Playwright já instalado em `website/node_modules` para rodar um script ad-hoc (não commitado, fora
de qualquer `package.json`) contra o `dashboard` real em `http://localhost:8081`, focado especificamente
em reproduzir o cenário exato do Achado 1 da 1ª rodada. Screenshots arquivados em `{docs_path}/screenshots/`
com prefixo `r2-` (para não colidir com as evidências já commitadas da 1ª rodada):
- Header/sidebar (`omuletachou` + menu lateral) aparece exatamente 1x — sem duplicação.
- Nenhum componente estrutural duplicado.
- Layout visual consistente com o padrão dos demais módulos (Angular Material), sem regressão de CSS.

Nenhum problema de layout encontrado.

## 3. Foco principal — confirmação do fix do Achado 1 (painel de skipped)

**Cenário reproduzido (idêntico ao da 1ª rodada, via script Playwright ad-hoc + SQL direto no Postgres):**
1. 2 produtos ML seedados em `AwaitingAffiliateLink` (`QA2-D` linha 1, `QA2-C` linha 2), confirmados via
   `GET /api/products?status=AwaitingAffiliateLink` na mesma ordem.
2. Colados 2 links de afiliado reais (`mercadolivre.com.br/social/...?matt_word=...&matt_tool=...&ref=...`)
   no textarea, pareados por linha — contador "2 de 2" habilitou o botão.
3. Condição de corrida simulada: `UPDATE products SET status = 0 (Pending) WHERE external_id = 'QA2-D'`
   diretamente no banco, momentos antes do clique em "Importar links" — mesmo procedimento do relatório
   da 1ª rodada.
4. Clique em "Importar links" → backend importou `QA2-C` (`Queued`, `affiliate_link` real e distinto do
   `source_url`) e pulou `QA2-D` (motivo: "Status atual é Pending, esperado AwaitingAffiliateLink").
5. **Resultado no DOM, após a snackbar desaparecer (aguardado 6.5s) e a lista de pendentes chegar a
   zero:**
   - `page.locator('.import-card').count()` → **1** (antes do fix seria 0 — este era o bug)
   - `page.locator('[data-testid="skipped-panel"]').count()` → **1**, `isVisible()` → **true**
   - Mensagem "Nenhum produto aguardando link de afiliado no momento." exibida corretamente no card 1
     (`empty-message`, count 1) — os dois estados (vazio + skipped) coexistem sem conflito visual
   - Expansão do painel via clique confirma o item e o motivo corretos: `"QA2 Produto D (vai virar
     Pending) — Status atual é Pending, esperado AwaitingAffiliateLink"`

**Conferido direto no Postgres** (não apenas o DOM): `QA2-C` com `status=1 (Queued)` e `affiliate_link`
populado (`.../social/omuletachou?matt_word=OMULETACHOU&matt_tool=REALTAGC1&ref=qa2c`, distinto do
`source_url`); `QA2-D` preservado em `status=0 (Pending)` sem `affiliate_link`.

**Evidência:** `{docs_path}/screenshots/r2-01-links-loaded.png` a `r2-05-skipped-panel-expanded.png`.
Destaque em `r2-04-after-snackbar-gone-dom-state.png` (empty state + painel de skipped visíveis juntos)
e `r2-05-skipped-panel-expanded.png` (item e motivo corretos após expansão).

**Conclusão do foco principal: Achado 1 (bloqueante da 1ª rodada) está CORRIGIDO.** O painel de itens
pulados permanece visível e funcional mesmo quando `products.length` chega a 0 após o import — exatamente
o requisito pedido nesta rodada. Isolamento de falha por item no backend confirmado novamente (1
importado, 1 pulado, sem abortar o lote).

## 4. Revalidação rápida dos pontos já aprovados na 1ª rodada (sem regressão)

- **Fluxo ponta a ponta do link de afiliado real**: revalidado no próprio cenário da Seção 3 acima —
  `affiliate_link` persistido no banco, distinto do `source_url`, contendo tag rastreável (`ref=qa2c`).
  Sem regressão.
- **Isenção de desconto mínimo para Mercado Livre (Seção 9)**: `ClaudeAiService.cs` (linhas 39-84,
  branch `isMercadoLivre = product.Platform == Platform.MercadoLivre`) inspecionado — código idêntico ao
  já validado pelo Code Review na rodada anterior; **arquivo não faz parte do diff do PR #197** (que só
  tocou `mercadolivre-links.component.html`/`.spec.ts` e documentação). Os 5 testes dedicados aos
  cenários 9.1-9.4 (`ScoreProductAsync_MercadoLivre_*` e `ScoreProductAsync_AmazonEShopee_MantemCriterioDeDescontoInalterado`
  em `ClaudeAiServiceTests.cs`) estão entre os 436/436 verdes. Sem regressão em Amazon/Shopee.
- **Isolamento de falha do coletor**: reconfirmado no próprio cenário de reprodução (item com status
  inesperado é pulado individualmente, sem abortar o lote — mesmo padrão já validado no CR do PR #189/
  #194/#197).
- **Amazon/Shopee não afetados**: `AmazonCollector.cs`/`ShopeeCollector.cs` fora do diff dos PRs #194 e
  #197 (confirmado via `git log`/diff do merge `cabc2f9..f4088a8`, que só trouxe o fix Angular +
  documentação).

Nenhuma regressão encontrada nos pontos revalidados.

## 5. Riscos herdados (schema `/highlights` e permalink `/p/{catalog_product_id}`)

Retentados nesta sessão com acesso de rede real do host:
- `GET https://api.mercadolibre.com/sites/MLB/categories` → **403** `PA_UNAUTHORIZED_RESULT_FROM_POLICIES`
- `GET https://www.mercadolivre.com.br/p/MLB16855791` → **403**, mesmo bloqueio

Mesmo bloqueio já documentado por Dev/LT/Code Review/QA 1ª rodada em todos os ambientes testados até
agora. **Não é bloqueador desta aprovação** — segue como pendência de monitoramento do primeiro ciclo real
do `CollectorJob` em produção (parsing defensivo do schema + checkpoint humano no fluxo semi-manual do
permalink mitigam o risco), conforme já recomendado pelas sessões anteriores. Reafirmado, não investigado
com ferramental novo nesta rodada (fora do foco pedido).

## 6. Cobertura dos critérios de aceite (Given/When/Then) — resumo da 2ª rodada

| Seção | Cenário | Resultado | Evidência |
|---|---|---|---|
| 1-6, 8 | Mapeamento categorias, Highlights, upsert, isolamento por categoria, cron, sem regressão Amazon/Shopee/categorização/fila | ✅ (herdado, sem mudança neste ciclo) | Já validado nas rodadas de Code Review e QA 1ª rodada; nenhum arquivo relevante alterado pelo PR #197 |
| 7.1/7.2 | Link de afiliado real, validado por conteúdo | ✅ | Revalidado na Seção 3/4 deste relatório |
| 9.1-9.5 | Isenção de desconto ML sem regressão Amazon/Shopee | ✅ | Código + 5 testes dedicados, 436/436 |
| Painel de itens pulados permanece acessível quando `products.length` chega a 0 | ✅ **CORRIGIDO** | Seção 3 — reprodução ao vivo do cenário exato da 1ª rodada, DOM + Postgres conferidos |

## 7. Conclusão

**QA APROVADO** (2ª rodada). O bug bloqueante da 1ª rodada (painel de itens "skipped" inacessível quando
a lista de pendentes esvazia após o import) está **confirmado corrigido** — reproduzido ao vivo o mesmo
cenário do relatório anterior, com o painel agora visível e funcional. Testes automatizados 100% verdes
(436/436 backend + 124/124 dashboard), sem regressão nos pontos já validados (link de afiliado real,
isenção de desconto ML, isolamento de falha, Amazon/Shopee intocados). Gate Visual formalmente N/A
(dashboard sem `test:visual`), mitigado por inspeção visual real via Playwright ad-hoc — sem duplicação
de header/layout.

Riscos herdados (schema `/highlights`, permalink `/p/{id}`) permanecem não confirmados ao vivo por
nenhum agente (mesmo bloqueio `PA_UNAUTHORIZED_RESULT_FROM_POLICIES` em todos os ambientes testados) —
tratado como pendência de monitoramento do primeiro ciclo real em produção, não como bloqueio desta
aprovação.
