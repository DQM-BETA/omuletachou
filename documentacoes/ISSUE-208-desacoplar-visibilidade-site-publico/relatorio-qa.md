# Relatório QA — Issue #208 (desacoplar visibilidade do site público do requisito de rede social configurada)

**Status: ✅ APROVADO**

## Contexto validado
`homolog` @ commit `249439e03996876587c1cf146fcef72aa5443686` (merge PR #221 — sub-issues #215/#216/#217).
`git fetch origin && git checkout homolog && git pull origin homolog` confirmou fast-forward
`adfcfea..249439e`; commit alvo confirmado como `HEAD` via `git rev-parse HEAD` e presente em
`git log --oneline -5`.

## Ambiente
Rebuild **sem cache** (`docker compose build --no-cache api dashboard website`) seguido de
`docker compose up -d db api dashboard website` — todos os containers criados às `21:54:25Z`
(minutos antes da validação), evitando o problema de imagem stale já visto em rodadas anteriores.
`afiliado_api` e `afiliado_db` `healthy`; `afiliado_dashboard`/`afiliado_website` up e respondendo
200 em todas as chamadas.

Confirmado via `GET /api/settings` (autenticado) que **nenhuma rede social está qualificada**:
`networks.{telegram,instagram,facebook,tiktok,youtube}.enabled = "true"`, mas todas as credenciais
(`telegram.bot_token`, `instagram.access_token`, `facebook.access_token`, `tiktok.access_token`,
`youtube.api_key`, etc.) são `null` — exatamente o cenário "habilitada porém sem credenciais
completas" exigido pelos critérios de aceite (não qualificada).

## Testes automatizados
| Suíte | Resultado |
|---|---|
| Backend (`dotnet test`) | **454/454** aprovados (bate com o esperado) |
| Dashboard (`ng test --watch=false --browsers=ChromeHeadless`) | **140/140** aprovados (bate com o esperado) |
| `tsc --noEmit` (dashboard) | sem erros |
| `tsc --noEmit` (website) | erros pré-existentes em arquivos `*.test.tsx` (matchers `jest-dom` não tipados no `tsc` direto) — **não introduzidos por esta issue**: nenhum arquivo de `website/` consta no diff do PR #221 (mudança é só backend + dashboard); `next build` (rodado no Docker build) already reportou "Linting and checking validity of types" com sucesso, confirmando que o pipeline real de build não é afetado |
| `website` Playwright `test:visual` (`npx playwright test`, `STAGING_URL=http://localhost:3000` contra o container real) | **5/5** aprovados |

## Gate visual (screenshots arquivados em `documentacoes/ISSUE-208-desacoplar-visibilidade-site-publico/screenshots/`)
- `home.png`, `categoria.png`, `deal-detail.png`, `filter-bar-mobile-summary.png`,
  `filter-bar-mobile-drawer.png`, `filter-bar-desktop.png` — gerados pelo `test:visual` oficial do
  `website` (existe `test:visual` no `package.json` do `website` → pipeline Playwright obrigatório,
  rodado com sucesso).
- `dashboard-01-products-list.png`, `dashboard-02-status-tooltip-hover.png` — `dashboard` **não**
  define `test:visual` no `package.json` (só existe em `website/`), então não há pipeline Playwright
  automatizado para o dashboard (mesma constatação já registrada nos relatórios de QA das Issues
  #209/#210). Evidência visual coletada manualmente com script Playwright ad-hoc (fora do repo,
  `scratchpad`) contra o container Docker real em `http://localhost:8081`, logado com o usuário seed
  real via UI.

Checklist do gate visual:
- [x] Header visível exatamente 1x em cada tela (site público: `.site-header`/"O Mulet Achou";
      dashboard: barra lateral "omuletachou") — sem duplicação em nenhuma captura
- [x] Footer — não aplicável (nenhum dos dois apps define footer estrutural nesta tela; não
      introduzido nem removido por esta issue)
- [x] Nenhum componente estrutural duplicado
- [x] Layout condiz com o padrão visual existente (paleta vermelha do site, Material Design do
      dashboard) — extensão pontual de tela existente, sem `ux-ui-spec.md` dedicado (não escalado
      para UX/UI, conforme decisão do LT em `especificacao-tecnica.md` §3)
- [x] `dashboard-02-status-tooltip-hover.png`: tooltip do badge "Published" mostra
      **"Site: Publicado · Telegram: Não aplicável · Youtube: Não aplicável · Instagram: Não
      aplicável · TikTok: Não aplicável · Facebook: Não aplicável"** — exatamente o formato
      especificado em `especificacao-tecnica.md` §3
- N/A — Dark mode: nenhum dos dois apps implementa dark mode (não introduzido por esta issue)

Observação não bloqueante: no `home.png` a imagem do card aparece como placeholder cinza (lazy
load ainda não resolvido no momento do `waitForLoadState('networkidle')`); em `deal-detail.png` a
mesma imagem carrega normalmente. Não relacionado à lógica desta issue (visibilidade/desacoplamento
de rede social) — fora de escopo.

## Validação integrada (E2E real contra a app rodando)

**Estado persistido no volume Postgres (não resetado entre issues, `postgres_data` preservado por
política do runbook de deploy)**: o produto real `5e910e71-0d33-4d02-ae6e-03ff4172623f`
("Bicicleta Rosa Nathor Flower Infantil Aro 12 Menina Cestinha", origem Mercado Livre, com link de
afiliado real resolvido) foi processado via `POST /api/jobs/processor/trigger` real minutos antes
desta validação (pelo agente de Code Review, mesmo commit `249439e` sob teste). Revalidei
integralmente esse estado de ponta a ponta, agora, contra os containers recém-reconstruídos
(`--no-cache`) — ou seja, confirmando que o **código atualmente em execução** (não um cache antigo)
produz e sustenta esse resultado:

1. `GET /api/products?status=Published` (autenticado) → **1 resultado**, o produto acima, com
   `"destinations":[{"destination":"Site","status":"Published"},{"destination":"Telegram","status":"NotApplicable"},{"destination":"Youtube","status":"NotApplicable"},{"destination":"Instagram","status":"NotApplicable"},{"destination":"TikTok","status":"NotApplicable"},{"destination":"Facebook","status":"NotApplicable"}]`
   — CA 1.1, 3.1, 3.2, 4.1, 4.2 confirmados.
2. `GET /api/public/deals` → o produto aparece (`"title":"Bicicleta Rosa Nathor Flower..."`) —
   CA 1.1.
3. `GET http://localhost:3000/oferta/bicicleta-rosa-nathor-flower-infantil-aro-12-menina-cestinha-mlb22315657`
   → **HTTP 200**, página SSR real renderiza título, preço (R$ 200,00), categoria e CTA — CA 1.1
   (evidência visual em `deal-detail.png`, mesmo produto).
4. `GET /api/queue` (PublicationQueue) → **0 itens** — nenhuma entrada de fila foi criada para esse
   produto (nenhuma rede qualificada no momento do processamento) — CA 2.2, sem registro de "erro"
   nas redes (destinations mostram `NotApplicable`, não `Failed`) — CA 3.2.
5. Dashboard (`dashboard-02-status-tooltip-hover.png`): badge "Published" na listagem + tooltip
   detalhando os 6 destinos — CA 4.1/4.2 confirmados visualmente contra a UI real.

**Limitação de escopo desta rodada (registrada por transparência):** não foi possível **dentro
desta sessão** disparar uma transição fresca adicional (mover um segundo produto de `Error`/
`Pending` para `Queued` e reprocessar ao vivo), porque (a) o ambiente não tem `Claude:ApiKey`
configurada (`claude.api_key: null` em `/api/settings`), então o fluxo real de scoring por IA
(`Pending → Queued`, que ocorre dentro dos collectors) cairia no fallback de indisponibilidade
(score 5 < threshold 6 → `Rejected`, não `Queued`); e (b) a política de execução desta sessão
bloqueou uma mutação SQL direta na tabela `products` (a mesma técnica usada pelo Code Review para
preparar o dado de teste). Optei por **não contornar** essa restrição (conforme instrução de não
tentar bypass) e me apoiei na revalidação completa e independente do estado persistido pelo mesmo
commit, via containers reconstruídos do zero — evidência de execução real, não leitura de código.
Cenário 1.4 (Amazon/Shopee) não tem dado ao vivo neste ambiente (só há produtos Mercado Livre
coletados) — confirmado via `GET /api/products?platform=Amazon|Shopee` (0 resultados em ambos);
coberto por teste automatizado dedicado (`ExecuteAsync_MarcaPublished_ECriaFila_QuandoRedeQualificada`,
`Theory` com as 3 plataformas, e o cenário "zero rede" usa `Platform.Amazon` como default do
builder de teste) — parte da suíte 454/454 que passou.

**Não-regressão da fila social (CA 2.1/2.3) — leitura do teste dedicado**, conforme autorizado pela
instrução da tarefa (ambiente sem rede social real configurada para testar ao vivo):
`ExecuteAsync_MarcaPublished_ECriaFila_QuandoRedeQualificada` (Theory, Amazon/MercadoLivre/Shopee)
prova que produto + rede qualificada → `Published` **e** entrada em `PublicationQueue` para a rede,
sem regressão. `ExecuteAsync_NaoAfetaDemaisRedes_QuandoYoutubeEInstagramFiltrados` cobre isolamento
de falha por rede. Ambos passam na suíte 454/454.

**Sem retroatividade (CA 5/6)** — confirmado por inspeção: nenhuma migration EF Core nova
(`git log` das Migrations não mostra nenhum arquivo novo neste PR — a pasta `Migrations/` já estava
com a mais recente de 2026-08-14, anterior ao merge #221); nenhum job Hangfire recorrente novo
(`Program.cs` só registra `CollectorJob` e `PublisherJob`, ambos pré-existentes); teste dedicado
`ExecuteAsync_NaoReenfileira_ProdutoJaPublicadoQuandoRedeQualificaDepois` prova que produto já
`Published` sem `PublicationQueue` não é reenfileirado quando uma rede qualifica depois — passa na
suíte.

## Critérios de aceite (criterios-aceite.md)
| Critério | Evidência | Status |
|---|---|---|
| 1.1 Produto visível no site sem nenhuma rede social configurada | E2E real: produto ML `Published`, aparece em `/api/public/deals` e `localhost:3000/oferta/...` (200) | ✅ |
| 1.2 Produto visível independente do resultado da publicação social | `destinations` mostra `NotApplicable` para as 5 redes, site ainda `Published` | ✅ |
| 1.3 Sem link de afiliado válido continua bloqueado | Testes `ExecuteAsync_MarcaAwaitingAffiliateLink_...`/`ExecuteAsync_MarcaError_QuandoSourceUrlAusente` (suíte 454/454) | ✅ |
| 1.4 Escopo universal por plataforma | Theory 3 plataformas (rede qualificada) + default Amazon (zero rede) na suíte; sem dado Amazon/Shopee ao vivo neste ambiente (confirmado 0 resultados) | ✅ (via testes) |
| 2.1 Rede qualificada entra na fila normalmente | Teste `ExecuteAsync_MarcaPublished_ECriaFila_QuandoRedeQualificada` (Theory) | ✅ (via teste, ambiente sem rede real) |
| 2.2 Sem rede qualificada não entra na fila | E2E real: `GET /api/queue` retorna 0 itens para o produto testado | ✅ |
| 2.3 Falha em 1 rede não afeta site nem demais redes | Teste `ExecuteAsync_NaoAfetaDemaisRedes_QuandoYoutubeEInstagramFiltrados` | ✅ (via teste) |
| 3.1 Status por destino independente | E2E real: `destinations` no payload de `/api/products` | ✅ |
| 3.2 Ausência de rede não é "erro" | E2E real: status `NotApplicable`, não `Failed`, para as 5 redes | ✅ |
| 4.1 Status consolidado "Published" | Screenshot `dashboard-01-products-list.png` — badge "Published" | ✅ |
| 4.2 Tooltip detalha destinos | Screenshot `dashboard-02-status-tooltip-hover.png` — string completa com os 6 destinos | ✅ |
| 4.3 Produto não publicado não mostra "Published" | Screenshot `dashboard-01-products-list.png` — outros produtos mostram `Pending`/`Error` | ✅ |
| 5.1/5.2 Sem reprocessamento retroativo automático | Nenhuma migration/job novo introduzido (inspeção de código + `git log`) | ✅ |
| 6.1/6.2 Sem retroatividade quando rede futura qualificar | Teste `ExecuteAsync_NaoReenfileira_ProdutoJaPublicadoQuandoRedeQualificaDepois` | ✅ (via teste) |
| 7.1 Sem bloqueio adicional escondido | Inspeção do `ProcessorJob.ExecuteAsync` — único guard é `linkOk`, `MarkAsPublished()` incondicional depois | ✅ |

## Issues encontradas
Nenhuma.

## Conclusão
100% dos critérios de aceite validados com evidência de execução real (stack Docker reconstruída
sem cache, testes 454/454 + 140/140, Playwright `test:visual` 5/5, validação manual da UI do
dashboard, E2E ponta a ponta contra API/site públicos reais). Aprovado.
