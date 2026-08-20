# Estado — Issue #230

## Identificação
- issue: 230
- titulo: revisar filtros da tela de produtos do site público (desconto, preço) — item 4 (busca inteligente) separado para Issue #260
- repo: DQM-BETA/omuletachou
- repo_path: repos/omuletachou
- docs_path: repos/omuletachou/documentacoes/ISSUE-230-revisar-filtros-site-publico
- openspec_change: repos/omuletachou/openspec/changes/issue-230-revisar-filtros-site-publico
- openspec_path: repos/omuletachou/openspec/changes/issue-230-revisar-filtros-site-publico

## Pipeline
- rota: normal
- etapa_atual: Concluído
- ultimo_agente: coordenador
- status_comment_id: 5360444175
- tech_stacks: [nodejs]
- repos:
  - omuletachou: https://github.com/DQM-BETA/omuletachou

## Sub-issues
- #261 (stack:nodejs, task_id:T-01) — Remover filtro de desconto mínimo (item 1) — **CONCLUÍDA/MERGED**
- #262 (stack:nodejs, task_id:T-02) — Corrigir bug do slider de preço + digitar min/max (itens 2+3) — **CONCLUÍDA/MERGED**
- sub_issues_frontend: {}
- desenv_tasks_merged: [261, 262]
- pr_261_feature_desenv: https://github.com/DQM-BETA/omuletachou/pull/263 (MERGED squash em desenv, 2026-08-20; sub-issue #261 fechada)
- pr_262_feature_desenv: https://github.com/DQM-BETA/omuletachou/pull/264 (MERGED squash em desenv, 2026-08-20, commit `99b801e`; sub-issue #262 fechada)
- pr_homologacao: https://github.com/DQM-BETA/omuletachou/pull/265 (desenv→homolog, MERGED — merge commit `7d343cd`, confirmado presente em `homolog` na validação do QA em 2026-08-20)
- pr_release: https://github.com/DQM-BETA/omuletachou/pull/266 (homolog→main, MERGED — merge commit `9aedc95`, 2026-08-20 19:04:32 UTC)
- code_review_homolog_pr: 265 (Code Review aprovou e mergeou `desenv→homolog` via `gh pr merge 265 --merge`, commit `7d343cd`, 2026-08-20 — ver seção "Code Review — PR #265")
- qa_status: **aprovado (2026-08-20)** — ver `relatorio-qa.md`
- closedAt: 2026-08-20T19:04:37Z

## Resumo da demanda
Escopo restrito aos itens 1-3 do pedido original (item 4 — busca inteligente — virou Issue #260,
separada):
1. Remover filtro de desconto mínimo (10%+/30%+/50%+) da barra de filtros.
2. Corrigir bug no filtro de preço (slider) — causa raiz identificada no refinamento técnico (ver
   `design.md`): rajada de `router.push()` sem debounce a cada `onChange` do range durante arrasto
   rápido excede o throttle de `history.pushState` do Chromium (~100 chamadas/10s) →
   `SecurityError` não tratado → sem `error.tsx` na árvore `app/` → Next.js cai no fallback de erro
   genérico sem mensagem. Correção: estado local de rascunho + commit via `router.replace` no
   soltar do gesto/debounce + clamp min<=max + `error.tsx` como defesa em profundidade.
3. Permitir digitar preço mínimo/máximo (campos numéricos sincronizados com o slider), com
   validação de min > max e valores negativos.

## Gate 1 — respondido pelo Gerente (2026-08-20)
1. Escopo confirmado: separar item 4 (Issue #260). #230 fica só com itens 1-3.
2. Item 4: já refletido na Issue #260 (decisão: sem chamada à IA por requisição — abordagem via
   banco).
3. Pista do bug do slider: arrastar rápido → página de erro sem mensagem.
4. Definições de pronto confirmadas conforme propostas (ver proposal.md).
5. Rota: `normal`.

## Ambiguidade arquitetural
Avaliada como **inexistente**. Os 3 itens são mudanças de UI/bugfix pontuais no componente
`filter-bar` já existente do `website/` (Next.js) — sem decisão de stack, integração externa nova
ou trade-off de infraestrutura. Seguiu direto para o **Líder Técnico**.

## Refinamento técnico (concluído 2026-08-20)
- Causa raiz do bug do slider (item 2) determinada por tracing estático completo do código (LT não
  executa aplicação — fora de escopo de ferramentas do papel); reprodução empírica ao vivo fica a
  cargo do Dev via teste e2e Playwright (`filter-bar-price.spec.ts`), exigido como critério de
  aceite de T-02 (reproduz o crash pré-fix e comprova a ausência dele pós-fix).
- Avaliação de split: itens 2+3 mantidos numa única sub-issue (T-02) por tocarem a mesma região de
  código (`PriceGroup`, mesmo mecanismo estado-local→commit→URL) — separar criaria dependência
  forte e risco de retrabalho. Item 1 isolado em sub-issue própria (T-01), sem overlap funcional.
- UX/UI dedicado avaliado como **desnecessário**: os campos de texto min/max (item 3) reaproveitam
  100% os tokens/padrões já existentes em `filter-bar.css` (mesma altura/borda dos
  `dropdown-trigger`), composição trivial do design system já estabelecido — não há tela/fluxo novo
  que justifique spec visual dedicada.

## Documentos produzidos
- `openspec/changes/issue-230-revisar-filtros-site-publico/proposal.md` (PM)
- `documentacoes/ISSUE-230-revisar-filtros-site-publico/criterios-aceite.md` (PM)
- `openspec/changes/issue-230-revisar-filtros-site-publico/design.md` (LT — investigação da causa
  raiz do bug + decisões de correção)
- `documentacoes/ISSUE-230-revisar-filtros-site-publico/especificacao-tecnica.md` (LT)
- `openspec/changes/issue-230-revisar-filtros-site-publico/tasks.md` (LT — T-01, T-02)
- `documentacoes/ISSUE-230-revisar-filtros-site-publico/relatorio-qa.md` (QA — validação integrada,
  aprovado)

## Implementação — T-01 / Sub-issue #261 (concluída pelo Dev, 2026-08-20; MERGED pelo LT, 2026-08-20)
- Branch `feature/ISSUE-261-remover-desconto-minimo` (worktree
  `.worktrees/feature-ISSUE-261-remover-desconto-minimo`), base `desenv`.
- PR feature→desenv: #263 — https://github.com/DQM-BETA/omuletachou/pull/263 — **MERGED (squash)**
  em `desenv` 2026-08-20 (commit `f6e4d4f`). Sub-issue #261 fechada (`completed`).
- Removido o seletor "Desconto mínimo" de `FilterBar.tsx` + código órfão (state, handler, CSS,
  referências em `page.tsx`/`lib/api.ts`) — checklist da especificação técnica cumprido
  integralmente; grep confirmou ausência de qualquer referência residual a `minDiscount` fora do
  teste de regressão dedicado.
- Testes: 116/116 passando (100%), cobertura de `FilterBar.tsx` 92%/`page.tsx` 100% linhas/`api.ts`
  100% (threshold do projeto é 80%). `npm run build` sucesso; `npm start` inicializa sem erro.
- Sem ambiguidade/dúvida técnica — não precisou de decisão de arquitetura.
- T-02 (sub-issue #262) segue pendente, mesmo arquivo (`FilterBar.tsx`) em região diferente
  (`PriceGroup`) — LT tentou fundir sequencialmente conforme nota de dependência em `tasks.md`,
  mas encontrou conflito real (ver seção "Implementação — T-02" e "Notas").

## Implementação — T-02 / Sub-issue #262 (concluída pelo Dev, 2026-08-20; MERGED pelo LT, 2026-08-20)
- Branch `feature/ISSUE-262-fix-slider-preco-minmax` (worktree
  `.worktrees/feature-ISSUE-262-fix-slider-preco-minmax`), base `desenv`.
- PR feature→desenv: #264 — https://github.com/DQM-BETA/omuletachou/pull/264 — **MERGED (squash)**
  em `desenv` 2026-08-20 (commit `99b801e`). Sub-issue #262 fechada (`completed`). Conflito com
  `desenv` (introduzido pelo merge do #263 no mesmo arquivo) resolvido pelo Dev antes deste merge
  (ver seção "Resolução do conflito PR #264").
- Causa raiz confirmada empiricamente (não só por tracing estático do LT): teste e2e Playwright
  (`e2e/filter-bar-price.spec.ts`) rodado contra o app real (`npm run dev`) + API real (docker
  compose local, dados reais do catálogo) confirma que 150 eventos `input` em sucessão apertada
  no slider não navegam mais para a página de erro (CA 2.4).
- Fix: estado local de rascunho (`minDraft`/`maxDraft`) desacoplado da URL a cada evento; commit
  via `router.replace` ao soltar o gesto e/ou debounce de 250ms; clamp `min<=max` defensivo;
  `website/app/error.tsx` como defesa em profundidade.
- Campos numéricos min/max digitáveis, sincronizados bidirecionalmente com o slider, com
  validação completa (CA 3.4-3.7).
- **Bug arquitetural latente encontrado e corrigido durante o TDD** (fora do escopo original,
  mas bloqueava a própria correção): `PriceGroup` era um componente aninhado
  (`function PriceGroup() {}` declarado dentro do corpo de `FilterBar`) — o React desmonta/
  remonta essa subárvore inteira a cada re-render do pai (nova identidade de função), o que
  destruía o `<input type="range">` a cada `onChange` (perda de foco/pointer capture nativo) e
  impedia o `onBlur` dos campos de texto de disparar. Convertido para valor JSX estável
  (`const priceGroup = (...)`), mesmo padrão já usado em `groupCategory`/`groupSubcategory`/
  `groupSort`. Não mexido em `DiscountGroup`/`Dropdown` (mesmo padrão, mas fora do escopo de
  T-02 — região de T-01, sem overlap funcional).
- Testes: 131/131 Jest passando (100%, suíte completa sem regressão), cobertura global 92.5%
  stmts / 88.8% branch (threshold do projeto 80%). 13 casos novos em `FilterBar.test.tsx`
  (CA 2.1-2.4, 3.1-3.7) + 3 em `app/error.test.tsx`. e2e: 4/4 novos + 5/5 existentes
  (`visual.spec.ts`) passando contra app real. `npm run build` (corrigido 1 erro de lint
  `prefer-const` encontrado só no build) e `npm start` sem erro.
- Sem ambiguidade/dúvida técnica — não precisou de decisão de arquitetura além da correção do
  bug de nested component (aplicação direta do padrão já existente no próprio arquivo).

## Resolução do conflito PR #264 (Dev, 2026-08-20)
- Worktree existente `.worktrees/feature-ISSUE-262-fix-slider-preco-minmax` reutilizado (não
  recriado). `git fetch origin desenv` + `git merge origin/desenv` (merge, não rebase — branch já
  pushada e com PR aberto, rebase reescreveria histórico público desnecessariamente).
- Conflito real em 2 arquivos, ambos resolvidos manualmente segundo a regra: manter 100% das
  mudanças de `PriceGroup`/preço (T-02) + manter a remoção do `DiscountGroup` (T-01/#263), sem
  reintroduzir o filtro de desconto:
  - `website/components/FilterBar.tsx`: removidos `handleDiscountToggle` e as 2 chamadas
    `<DiscountGroup />` (resíduo do lado HEAD, já que o componente/estado do desconto tinha sido
    removido em `desenv` mas a branch de #262 ainda referenciava a função); removidos também
    `handleMinPriceChange`/`handleMaxPriceChange` (resíduo do lado `origin/desenv` — handlers
    antigos e simples, sem debounce, do preço, já superados pelos novos
    `handleMin/MaxPriceSliderChange` + `commitMinPriceText`/`commitMaxPriceText` que já existiam na
    branch de #262 fora da região de conflito). `priceGroup` (JSX) mantido nas 2 posições de
    render (desktop row + drawer mobile), sem `<DiscountGroup />`.
  - `website/app/styles/filter-bar.css`: mantido o CSS novo de `.filter-bar__price-inputs`/
    `.filter-bar__price-input-field`/`.filter-bar__price-error`; removido o bloco
    `.filter-bar__discount-group`/`.filter-bar__discount-btn*` (resíduo do lado HEAD).
  - `documentacoes/.../estado.md`, `website/app/page.tsx`, `website/app/page.test.tsx`,
    `website/lib/api.ts`, `website/lib/api.test.ts`, `website/components/FilterBar.test.tsx`:
    auto-merge limpo (sem conflito), trazendo as mudanças de remoção de `minDiscount` do #261/#263
    para dentro da branch de #262.
- **Gate obrigatório (passo g do processo):** grep em todo `website/` por `minDiscount`/
  `DiscountGroup`/`10%+`/`30%+`/`50%+`/`Desconto mínimo` pós-resolução — sem resíduo funcional.
  Encontrado **1 teste e2e obsoleto** não relacionado ao merge em si, mas que quebraria por causa
  dele: `website/e2e/visual.spec.ts` (`Desktop (>=1024px): os 5 controles em linha única, sem
  drawer`) ainda esperava o botão `10%+` do `DiscountGroup` (já removido em `desenv` pelo #263, mas
  esse teste Playwright não roda no Jest/CI local do Dev de #261 — só foi pego agora ao rodar a
  suíte completa de Playwright). Corrigido: assert trocado para os 2 sliders de preço
  (`getByRole('slider', { name: 'Preço mínimo' })`/`'Preço máximo'`), que agora ocupam a 3ª posição
  dos 5 controles.
- Testes pós-resolução: Jest 130/130 passando (100%, suíte completa, sem regressão). Playwright
  `test:visual` rodado contra stack Docker real (`docker compose -p omuletachou-local up --build
  db api website`, build a partir do worktree — não do repo raiz — para testar o código
  efetivamente resolvido): 8/8 passando + 1 skip pré-existente (sem oferta ativa no catálogo de
  teste, condição já existente no teste, não uma regressão) — inclui os 4 testes de
  `filter-bar-price.spec.ts` (CA 2.1/2.2/2.4/3.1-3.3) e os 4 (de 5) de `visual.spec.ts`. Stack
  Docker derrubada (`docker compose -p omuletachou-local down`) após a validação; arquivos locais
  temporários (`docker-compose.override.yml`, `.env`, copiados do repo raiz só para o teste)
  removidos do worktree antes do commit.
- `npm run build` já validado dentro do build da imagem Docker do `website` (sucesso); container
  `afiliado_website` respondeu HTTP 200 em `localhost:3000` — app inicializa sem erro.
- Push: `feature/ISSUE-262-fix-slider-preco-minmax` (2 commits novos: merge de resolução +
  correção do teste e2e obsoleto). `gh pr view 264` confirmou `mergeStateStatus: CLEAN` /
  `mergeable: MERGEABLE` — PR #264 mergeado pelo LT sem necessidade de novo PR.

## Merge #264 + PR desenv→homolog (LT, 2026-08-20)
- PR #264 mergeado via squash, sem conflitos (`gh pr merge 264 --squash`), commit `99b801e` em
  `desenv`. Sub-issue #262 fechada (`completed`).
- Como #262 era a última sub-issue pendente da Issue #230 (`desenv_tasks_merged` == `sub_issues`),
  criado o PR de promoção `desenv→homolog` #265 —
  https://github.com/DQM-BETA/omuletachou/pull/265 (merge commit, NUNCA squash — a ser mergeado
  pelo Code Review).

## Code Review — PR #265 (desenv→homolog), 2026-08-20 — APROVADO
- Jest: `npm test -- --watchAll=false --coverage` → 130/130 passando, cobertura global 92.77%
  stmts / 88.55% branch (threshold 80%).
- Build/boot real: `docker compose build --no-cache website api` (sucesso) +
  `docker compose up -d db api website` (3 containers healthy/Up). `curl :8080/health` → 200;
  `curl :3000/` → 200 (HTML contém `filter-bar`); `?minPrice=100&maxPrice=500` → 200;
  `?minPrice=900&maxPrice=100` (par invertido direto na URL) → 200, sem crash.
- Integração real (teste crítico): `npx playwright test` rodado contra o container Docker real
  (`reuseExistingServer: true` reaproveitou o container já no ar) → 9/9 passando, incluindo
  `filter-bar-price.spec.ts` CA 2.4 (150 eventos `input` em sucessão apertada, sem `pageerror`,
  sem cair no `error.tsx`/fallback genérico) — confirma causa raiz eliminada.
- Checklist de veto: sem `.first()`/`.nth()`/`.last()` em specs e2e (grep completo); sem segredos
  no diff; sem teste-lixo; cobertura ≥ 80%; segue design.md; cobre CA 1.1-1.2/2.1-2.5/3.1-3.7.
- `/code-review` (plugin Anthropic): sem comentários/reviews postados no PR no momento da
  revisão — nada a incorporar.
- Evidência completa postada como comentário no PR #265.
- Merge `desenv→homolog` executado: `gh pr merge 265 --merge` (merge commit `7d343cd`,
  2026-08-20T18:42:08Z).

## QA (2026-08-20) — APROVADO
- Sincronização confirmada: `git fetch origin` + `git checkout homolog` + `git pull origin homolog`
  trouxe o commit `7d343cd` (Merge pull request #265), presente no topo de `git log --oneline -5`.
- Jest: 130/130 passando, cobertura 92.77% stmts / 88.55% branch (>= threshold 80%).
- `docker compose build --no-cache website api` (a partir de `homolog`) + `docker compose up -d db
  api website`: build e boot reais sem erro; `api` healthy; `website` HTTP 200; `api/health` HTTP
  200.
- Playwright (`npm run test:visual`, `STAGING_URL=http://localhost:3000` contra o container Docker
  real, `SCREENSHOTS_DIR={docs_path}/screenshots`): **9/9 passando**, incluindo o teste crítico CA
  2.4 (150 eventos de arrasto rápido no slider sem navegar para página de erro) contra a aplicação
  real e catálogo real (105 itens).
- Gate visual: 6 screenshots inspecionadas manualmente — header 1x por tela, sem duplicação
  estrutural, seletor de desconto ausente, barra de filtros íntegra.
- Grep confirmou ausência de código órfão de `minDiscount`/`DiscountGroup` no código-fonte
  (CA 1.2). `website/app/error.tsx` inspecionado e funcional como defesa em profundidade.
  Implementação de `commitPriceParams`/`router.replace`/debounce inspecionada e bate com a causa
  raiz documentada em `design.md`.
- Logs do container `afiliado_api` durante os testes confirmam o filtro `minPrice` aplicado na
  query SQL real, sem exceções.
- Todos os 17 critérios de aceite (`criterios-aceite.md`) validados com evidência — ver
  `relatorio-qa.md` para a tabela completa. Nenhuma issue encontrada.
- Observação não bloqueante: `tsc --noEmit` standalone falha por config pré-existente
  (`@testing-library/jest-dom` ausente em `tsconfig.json`), não é regressão desta issue (afeta
  também arquivos de teste não tocados por #230); o type-check real do `next build` passou.
- Containers de validação (`db`, `api`, `website`) parados (`docker compose stop`) ao final.

## PR Release + Gate 2 (2026-08-20)
- Criado `homolog→main` #266 — https://github.com/DQM-BETA/omuletachou/pull/266 (merge commit,
  NUNCA squash), referenciando Issue #230, sub-issues #261/#262, PR de Code Review #265 e
  `relatorio-qa.md`.
- **Gate 2 aprovado pelo Gerente** (2026-08-20 19:04:32 UTC)
- Merge `homolog→main` executado: `gh pr merge 266 --merge` (merge commit `9aedc95`,
  2026-08-20T19:04:32Z).
- Issue #230 fechada com reason `completed` (2026-08-20T19:04:37Z).
- Reconciliação de `estado.md`: os commits de Code Review (`code_review_homolog_pr: 265` e seção
  "Code Review — PR #265") foram feitos em `desenv` (commit `ea7086a`) enquanto o QA validava a
  partir de `homolog` (working tree local, sem esse commit) — divergência esperada entre branches,
  não um conflito real. Este commit une as duas seções (Code Review + QA) e os campos do cabeçalho
  num único `estado.md` consistente em `desenv`.

## Notas
- Diretório duplicado `documentacoes/ISSUE-230-revisar-filtros-site-público/` (com acento) —
  confirmado como artefato órfão de stub inicial do Coordenador (backlog, pré-refinamento do PM),
  sem conteúdo não capturado no diretório correto (sem acento). **Removido** por este agente.
- openspec change criado via `npx @fission-ai/openspec new change` — nome exigido em kebab-case
  minúsculo (`issue-230-...`, não `ISSUE-230-...`); path real difere do padrão usado em docs_path
  (que mantém `ISSUE-230` maiúsculo por convenção da squad).
- **Merge sequencial (2026-08-20):** #263 mergeado (squash) primeiro sem conflito. Ao tentar
  fundir #264 em seguida, `mergeStateStatus` mudou de `CLEAN` para `DIRTY`/`CONFLICTING` (efeito
  colateral esperado do merge anterior no mesmo arquivo). `gh pr update-branch 264` falhou;
  confirmado via merge de teste local (branch temporária, sem push, revertida com `git merge
  --abort` + `git branch -D`) que o conflito é real em `FilterBar.tsx` (regiões próximas de
  `PriceGroup`/`DiscountGroup` divergiram o suficiente para não haver merge automático) e em
  `filter-bar.css`. Fora do escopo de ferramentas do LT resolver conflito de código — devolvido
  ao Dev responsável pela sub-issue #262 para sincronizar `feature/ISSUE-262-fix-slider-preco-minmax`
  com `desenv` (`git merge origin/desenv` ou `git rebase origin/desenv`), resolver as duas
  divergências e dar push. Após o push, novo LT retomou o merge de #264 (concluído nesta invocação,
  ver "Merge #264 + PR desenv→homolog") e criou o PR `desenv→homolog` (#265).
- **Campo `code_review_homolog_pr` não preenchido no `estado.md` visto pelo QA:** o QA encontrou o
  PR #265 já mergeado em `homolog` no momento do spawn (barreira de sincronização confirmou o
  commit `7d343cd`), mas a cópia de `estado.md` em `homolog` não tinha o registro formal do Code
  Review. **Resolvido nesta invocação:** o Code Review de fato ocorreu e documentou sua evidência
  em `estado.md` — só que o fez em `desenv` (commit `ea7086a`, anterior ao merge #265), branch que
  o QA não consultou por trabalhar a partir de `homolog`. Campo `code_review_homolog_pr: 265` e a
  seção "Code Review — PR #265" confirmados e reconciliados neste commit.

## Consolidação de Custo (2026-08-20, Gate 2 + Encerramento)
- Issue criada: 2026-08-19 13:29:32 UTC
- Issue fechada: 2026-08-20 19:04:37 UTC
- **Tempo decorrido total:** 29h 35m 5s (1 dia, 5 horas, 35 minutos, 5 segundos)
- **Observação:** O ledger da sessão principal não foi preenchido com dados de tokens/modelo por etapa (pendente de consolidação no HANDOFF de cada worker). A tabela abaixo aguarda atualização:

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | PM Fase 1 | pm-analista-negocios | Sonnet | (preencher) | - | - |
| 2 | PM Fase 2 | pm-analista-negocios | Sonnet | (preencher) | - | - |
| 3 | Refinamento Técnico | lider-tecnico | Sonnet | (preencher) | - | - |
| 4 | Merge #263 + tentativa #264 (bloqueado) | lider-tecnico | Sonnet | (preencher) | - | - |
| 5 | Merge #264 + PR desenv→homolog | lider-tecnico | Sonnet | (preencher) | - | - |
| 6 | Code Review (PR #265) | code-review | Sonnet | (preencher) | - | - |
| 7 | QA | qa | Sonnet | (preencher) | - | - |
| 8 | Gate 2 + Encerramento | coordenador | Haiku | (preencher) | - | - |
| | **TOTAIS** | | | **TBD** | | **TBD** |
