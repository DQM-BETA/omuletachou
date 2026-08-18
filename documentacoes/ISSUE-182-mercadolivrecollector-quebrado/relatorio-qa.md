# Relatório de QA — Issue-pai #182 (MercadoLivreCollector quebrado — reconstruir com Highlights API)

**Status: REPROVADO**

**Branch validada:** `homolog` (commit `cabc2f96387ea547ca2e9a3ab68656df6354cc7b`, merge do PR #189 + PR #194,
confirmado em `git log --oneline -5` antes de qualquer teste).

**Ambiente:** stack local via `docker compose` (db + api + dashboard), build `--no-cache`, `.env` local com
credenciais de teste (`operador@omuletachou.local`) e `CLAUDE_API_KEY` real configurada.

---

## 1. Testes automatizados (pré-requisito antes da inspeção qualitativa)

| Suíte | Resultado | Evidência |
|---|---|---|
| `dotnet build -c Release` | OK | 0 erros |
| `dotnet test -c Release --no-build` (backend) | **436/436** ✅ | Inclui os 3 testes de integração `ClaudeBudgetServiceIntegrationTests` via Testcontainers/Docker (Docker Desktop disponível nesta sessão) |
| `dotnet test --collect:"XPlat Code Coverage"` | line-rate 89.4%, **branch-rate 80.7%** (≥80%) | `coverage.cobertura.xml` (removido após medição, não persistido no repo) |
| `ng test --watch=false --browsers=ChromeHeadless` (dashboard) | **120/120** ✅ | Executado nesta sessão, não apenas lido do relato do CR |

Todos os testes passaram — prosseguiu-se para a inspeção qualitativa + validação integrada (regra `d.` do processo).

## 2. Gate Visual (d2)

`dashboard/package.json` **não tem** o script `npm run test:visual` (confirmado por leitura direta, junto com
`website/package.json`, que **tem** `"test:visual": "playwright test"` — mas a UI desta issue vive em `dashboard/`,
não em `website/`). Por regra estrita do processo (decisão exclusivamente pela existência do script no
`package.json` do projeto com a UI em questão): **E2E/screenshots formalmente N/A (projeto/dashboard sem
`test:visual`)**.

Dito isso, dado o histórico documentado em
`.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md` (ainda `status: pendente` para
`dashboard/` — só `website/` ganhou Playwright na Issue #154) e a explícita instrução do spawn para validar a UI
ponta a ponta, esta sessão usou o Playwright já instalado em `website/node_modules` para rodar um script Playwright
**ad-hoc** (fora do projeto, não commitado, não parte de nenhum `package.json`) contra o `dashboard` real rodando em
`http://localhost:8081`, e arquivou os PNGs em
`{docs_path}/screenshots/`. Isso não substitui a lacuna formal (recomendo reabrir/atualizar a melhoria pendente
para incluir `dashboard/`), mas permitiu inspeção visual real em vez de apenas ler o HTML.

Inspeção das 8 screenshots (`01`–`09`, exceto `07`, que ficou vazia por timing — ver Achado 1 abaixo):
- Header/sidebar aparece exatamente 1x em todas as telas — sem duplicação.
- Nenhum componente estrutural duplicado.
- Layout consistente com o padrão visual dos demais módulos do dashboard (Angular Material).
- Tela "Links de Afiliado — Mercado Livre" (`02`, `03`) confere com `ux-ui-spec.md`: lista numerada de produtos
  pendentes + botão "Copiar URLs", textarea de colagem com contador de pareamento (`"N de M links colados"`),
  botão "Importar links" habilitado só quando a contagem bate.

Nenhum problema de layout/CSS encontrado.

## 3. Validação integrada (d3) — fluxo semi-manual de link de afiliado (requisito crítico do Gerente)

Stack subida via Docker (`docker compose build --no-cache api dashboard` + `up -d db api dashboard`), ambos
`healthy`, `/health` 200. Login real via `POST /api/auth/login` (JWT real). Produtos de teste inseridos
diretamente no Postgres do container em `AwaitingAffiliateLink` (`Platform=MercadoLivre`).

**Fluxo feliz (2 produtos, pareamento por ordem):**
1. `GET /api/products?status=AwaitingAffiliateLink` retorna os 2 produtos com `sourceUrl` populado.
2. Tela `mercadolivre-links` renderiza a tabela na mesma ordem, com botão "Copiar URLs (2)".
3. Colei 2 links de formato real de afiliado do ML (`mercadolivre.com.br/social/...?matt_word=...&matt_tool=...&ref=...`)
   no textarea — contador mudou para "2 de 2 links colados — pronto para importar." e o botão "Importar links"
   habilitou.
4. Cliquei "Importar links" → snackbar "2 produtos importados com sucesso." → lista ficou vazia.
5. **Conferido direto no Postgres** (não apenas HTTP 200): `affiliate_link` de cada produto **diferente do
   `source_url`/permalink**, contendo a tag/identificação rastreável (`ref=REALTAGTEST1`/`REALTAGTEST2`), pareado
   corretamente por ordem/linha (produto na linha 1 da tabela recebeu o link da linha 1 do textarea, produto da
   linha 2 recebeu o da linha 2) e `status` mudou para `Queued`. Isso satisfaz a intenção da Seção 7 dos critérios
   de aceite (link real e distinto do permalink, inspecionado no conteúdo, não só o HTTP 200) — ver nota sobre
   defasagem de wording no item 6 abaixo.

**Isolamento de falha por item (painel de skipped) — ver Achado 1 (bloqueante) na seção 5.**

## 4. Riscos herdados (schema `/highlights` e permalink `/p/{catalog_product_id}`)

Retentados nesta sessão com acesso de rede real (host **e** de dentro do container `afiliado_api`):

- `GET https://api.mercadolibre.com/sites/MLB/categories` → **403** `PA_UNAUTHORIZED_RESULT_FROM_POLICIES`
  (`blocked_by: PolicyAgent`).
- `GET https://www.mercadolivre.com.br/p/MLB16855791` → **403**, mesmo bloqueio.
- **Achado adicional (refina o diagnóstico anterior, não resolve o risco):** `POST
  https://api.mercadolibre.com/oauth/token` (mesmo host) **não** foi bloqueado — retornou uma resposta real da API
  do Mercado Livre (`400 invalid_client`, credenciais de teste inválidas). Isso indica que o bloqueio `PolicyAgent`
  não é um bloqueio genérico de rede do ambiente/sandbox ao domínio inteiro `api.mercadolibre.com` (como as sessões
  anteriores concluíram), e sim um bloqueio **específico do próprio Mercado Livre** a determinados endpoints de
  leitura pública (`/sites/.../categories`, `/highlights/...` por extensão, e a página pública `/p/{id}`),
  provavelmente por reputação de IP/datacenter (WAF anti-bot), independente de qual agente/sessão está rodando.
  Isso não muda a conclusão prática (o schema real do Highlights e o padrão de permalink continuam **não
  confirmados ao vivo**), mas é uma pista útil para quem for investigar em produção: o bloqueio pode persistir
  mesmo na VM Oracle se o IP dela também cair em faixa de datacenter reconhecida pelo WAF do ML — o teste real só
  será conclusivo no primeiro ciclo do `CollectorJob` rodando com credenciais reais de produção.
- Disparo real do job (`POST /api/jobs/collector/mercadolivre/trigger`, com `client_id`/`client_secret` de teste
  configurados) confirmou a degradação esperada: falha ao obter token OAuth2 (`400`) → **log de warning, ciclo
  abortado sem exceção, `count: 0`, HTTP 200** — sem crash, consistente com o isolamento de falha total (Cenário
  5.3).

**Tratado como pendência para o primeiro ciclo real do `CollectorJob` em produção**, como já recomendado pelas
sessões anteriores — não é motivo de reprovação isolado (parsing defensivo + checkpoint humano no permalink
mitigam), mas seria negligente aprovar sem registrar que o achado sobre o `oauth/token` não muda esse status.

## 5. Achados

### Achado 1 (BLOQUEANTE) — Painel de itens pulados (skipped) fica inacessível quando a lista de pendentes esvazia após a importação

**Severidade:** Alta (funcional — perda de informação operacional).

**Cenário reproduzido (script Playwright ad-hoc, reprodutível 2x de forma consistente):**
1. 2 produtos em `AwaitingAffiliateLink` (D, C). Colei 2 links no textarea (contagem bate, botão habilita).
2. Simulei uma condição de corrida plausível (operador altera o status do produto em outra aba entre o carregamento
   da lista e o clique em "Importar"): mudei o `status` de D para `Pending` diretamente no banco, momentos antes do
   clique em "Importar links".
3. Cliquei "Importar links". Resultado no backend: C importado (`Queued`, `affiliate_link` preenchido), D pulado
   (skip por `"Status atual é Pending, esperado AwaitingAffiliateLink"`) — **o isolamento de falha por item no
   backend funciona corretamente** (não derruba o lote inteiro, mesmo padrão já validado pelo Code Review com
   `productId` inexistente).
4. **Porém, no frontend:** a snackbar mostrou corretamente "1 importados, 1 pulados." com ação "Ver detalhes", mas
   como os 2 produtos (o importado e o pulado) saíram da lista de `AwaitingAffiliateLink` — o `import()` do
   `MercadolivreLinksComponent` chama `this.load()` ao final, que recarrega a lista e a deixa vazia — o card inteiro
   que contém o painel de skipped some do DOM, porque em `mercadolivre-links.component.html:120` o
   `<mat-card class="import-card">` (que envolve o `<mat-expansion-panel data-testid="skipped-panel">`, linha 166)
   está condicionado a `*ngIf="!loading && !errorMessage && products.length > 0"`. Quando `products.length` chega a
   zero após o reload (cenário plausível e provavelmente comum: lote diário inteiro resolvido, seja por sucesso ou
   por skip, sem novos produtos chegando no meio), **o painel de skipped nunca aparece** — confirmado via inspeção
   de DOM (`page.locator('.import-card').count()` = 0, `page.locator('[data-testid="skipped-panel"]').count()` = 0
   mesmo após clicar em "Ver detalhes", que só seta uma flag interna `panelExpanded=true` sem efeito, pois o
   elemento já não existe).
5. O operador fica sem nenhuma forma de saber **qual** produto foi pulado nem **por quê**, além do texto genérico
   e efêmero da snackbar ("1 importados, 1 pulados.", que desaparece em 6s) — informação crítica para o fluxo
   operacional (ele precisaria descobrir manualmente qual produto ficou sem link, olhando a tela de Produtos).

**Evidência:** screenshots `06-skip-scenario-after-import.png` (snackbar visível, mas import-card já sumiu),
`08-repro-after-import-dom-state.png` e `09-repro-after-ver-detalhes-click.png` (painel ausente do DOM mesmo após
clique em "Ver detalhes") em `{docs_path}/screenshots/`. Confirmação direta no Postgres do resultado do backend
(`SELECT title, status, affiliate_link FROM products ...`) mostrando D preservado como `Pending` sem
`affiliate_link` e C corretamente movido a `Queued`.

**Por que isso reprova a issue:** o spawn desta validação pediu explicitamente `"o painel de itens pulados
(skipped)"` como item a validar, e ele está funcionalmente inacessível no caso mais provável de uso real (lote
processado até esvaziar a fila). O requisito de negócio original (Gate 1.5 / especificação técnica §3.6) previa
esse painel justamente para o operador revisar skips sem precisar caçar manualmente — a lacuna anula esse
propósito.

**Sugestão de correção (não implementada por mim — fora do escopo do QA):** desacoplar a visibilidade do
`mat-expansion-panel` de skipped da condição `products.length > 0` do card pai (ex.: mover o painel para fora do
`*ngIf` do `import-card`, ou usar uma condição própria tipo `*ngIf="!loading && (products.length > 0 || (skipped &&
skipped.length > 0))"`).

### Achado 2 (não-bloqueante, documentação) — `criterios-aceite.md` Seções 3 e 7 com wording desatualizado pós Gate 1.5

- **Seção 3** ("Resolução via multi-get `/items?ids=...`", cenários 3.1–3.3) descreve uma arquitetura de batching
  que foi abandonada pelo pivô do Gate 1.5 (confirmado em `blockers`/`design.md` §10: `GET /items/{id}` retorna 403
  mesmo autenticado). A implementação atual usa `GET /products/{id}` + `GET /products/{id}/items` **por item,
  sequencialmente, sem lote** (confirmado por leitura de `MercadoLivreCollector.ResolveAndUpsertAsync`). A intenção
  do cenário (resolver produtos sem estourar limite de API) está preservada — só a mecânica mudou. Não reprovo por
  isso (mudança de arquitetura já aprovada e documentada formalmente no Gate 1.5), mas o documento de critérios
  nunca foi atualizado para refletir o pivô, ao contrário da Seção 9 (que foi formalizada corretamente após o
  Achado 2 do `/code-review`). Sugiro ao PM atualizar a Seção 3 (e a referência a `affiliate-tools/links` na Seção
  7, que também descreve o endpoint morto removido por `EnsureAffiliateLinkAsync`) para não confundir futuras
  validações.
- Já registrado como sugestão, sem necessidade de nova issue — recomendo ao Coordenador anotar em
  `.claude/melhorias/` se julgar relevante manter rastreável.

## 6. Cobertura dos critérios de aceite (Given/When/Then)

| Seção | Cenário | Resultado | Evidência |
|---|---|---|---|
| 1.1/1.2 | Mapeamento 8 categorias → `MLB####` | ✅ | `CategoryMap` no código, 8 entradas documentadas com justificativa inline |
| 2.1/2.2 | Highlights por categoria, até 10 IDs, 1x por categoria/ciclo | ✅ | `CollectAsync` — loop único por `CategoryMap`, sem paginação adicional |
| 3.1/3.2 | Resolução multi-get em lotes respeitando limite | ⚠️ Arquitetura mudou (ver Achado 2) — intenção preservada via resolução individual `/products/{id}` | Código + `blockers` |
| 3.3 | Item não resolvido no multi-get é ignorado | ✅ (adaptado: item não resolvido em `/products/{id}`/`/items`) | `ResolveAndUpsertAsync` catch + log warning, retorna `null` |
| 4.1 | Produto mapeado com mesmos campos dos demais collectors | ✅ | `UpsertProductAsync` |
| 4.2 | Upsert reaproveitado (`UpdateFromCollector`) | ✅ | Código + 436 testes verdes |
| 4.3 | Mesmo produto em 2 categorias no ciclo → 1 registro | ✅ | `resolvedInCycle` HashSet no `CollectAsync` |
| 5.1 | Falha em 1 categoria não aborta ciclo | ✅ | try/catch por categoria, testado ao vivo (trigger real do job) |
| 5.2 | Falha em 1 "lote" não aborta ciclo | ✅ (adaptado para falha por item) | try/catch por item em `ResolveAndUpsertAsync` |
| 5.3 | Falha total não derruba `CollectorJob` | ✅ | Testado ao vivo: token OAuth2 falhou (400), `count:0`, HTTP 200, sem exceção |
| 6.1 | Cron mantido | ✅ | `schedule.collector_cron = "0 6 * * *"` no banco, não alterado |
| 7.1/7.2 | Link de afiliado real, validado por conteúdo, não só 200 | ✅ (fluxo semi-manual) | DB confirmou `affiliate_link` distinto de `source_url` com tag rastreável, pareado corretamente |
| 7.3 | Achado de defeito na geração de link tratado separadamente | ✅ | Gate 1.5 documentado, achado tratado como decisão do Gerente, não corrigido silenciosamente |
| 8.1 | Scoring inalterado (Amazon/Shopee) | ✅ | Comparação de código (branch `else` idêntico) + testes `[Theory]` |
| 8.2 | Categorização (#167) inalterada | ✅ | `CategoryDetector.Detect` chamado igual aos demais collectors |
| 8.3 | Fila de publicação inalterada | ✅ | `PublicationQueue`/`PublisherJob` fora do diff do PR |
| 8.4 | Amazon/Shopee não afetados | ✅ | `AmazonCollector.cs`/`ShopeeCollector.cs` fora do diff do PR |
| 9.1–9.4 | Isenção de desconto mínimo só para ML, sem regressão Amazon/Shopee | ✅ | Código lido (`ClaudeAiService.ScoreProductAsync`, branch condicional por `Platform`) + 5 testes novos + 436/436 |
| 9.5 | Isenção não impede reavaliação futura | ✅ | Comentário no código referencia explicitamente o cenário |
| Fluxo semi-manual + tela `mercadolivre-links` + painel skipped | Pareamento por ordem/linha | ✅ pareamento; **❌ painel de skipped (Achado 1)** | Screenshots + DB |

## 7. Conclusão

**QA REPROVADO** — não por falha nos testes automatizados (436/436 backend + 120/120 dashboard, todos executados
ao vivo nesta sessão) nem por regressão em Amazon/Shopee/scoring/categorização/fila (tudo confirmado sem
regressão), mas pelo **Achado 1**: o painel de itens pulados da tela "Links de Afiliado — Mercado Livre" fica
inacessível no cenário mais provável de uso real (lote inteiro resolvido), contrariando explicitamente um dos
pontos de atenção pedidos nesta validação e o propósito do próprio painel (dar visibilidade operacional sobre
skips). É um bug de UI localizado e pequeno de corrigir (uma condição de `*ngIf`), não uma falha estrutural — mas
por regra do processo, QA não aprova parcialmente.

Riscos herdados (schema `/highlights`, permalink `/p/{id}`) permanecem não confirmados ao vivo (mesmo bloqueio
`PolicyAgent`, agora com evidência adicional de que não é bloqueio genérico de sandbox — ver Seção 4) e devem
continuar sendo tratados como pendência de monitoramento do primeiro ciclo real em produção, não como bloqueio
desta issue.
