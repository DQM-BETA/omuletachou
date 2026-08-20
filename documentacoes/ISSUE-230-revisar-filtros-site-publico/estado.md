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
- etapa_atual: Em Desenvolvimento
- ultimo_agente: lider-tecnico
- status_comment_id: (gerenciado pelo Coordenador — não criado ainda por este agente)
- tech_stacks: [nodejs]
- repos:
  - omuletachou: https://github.com/DQM-BETA/omuletachou

## Sub-issues
- #261 (stack:nodejs, task_id:T-01) — Remover filtro de desconto mínimo (item 1)
- #262 (stack:nodejs, task_id:T-02) — Corrigir bug do slider de preço + digitar min/max (itens 2+3)
- sub_issues_frontend: {}
- desenv_tasks_merged: []
- pr_261_feature_desenv: https://github.com/DQM-BETA/omuletachou/pull/263 (aberto, aguardando merge do LT)
- pr_262_feature_desenv: https://github.com/DQM-BETA/omuletachou/pull/264 (aberto, aguardando merge do LT — fundir DEPOIS do #263, mesmo arquivo)
- pr_homologacao: ~
- pr_release: ~
- code_review_homolog_pr: ~
- qa_status: ~

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

## Implementação — T-01 / Sub-issue #261 (concluída pelo Dev, 2026-08-20)
- Branch `feature/ISSUE-261-remover-desconto-minimo` (worktree
  `.worktrees/feature-ISSUE-261-remover-desconto-minimo`), base `desenv`.
- PR feature→desenv: #263 — https://github.com/DQM-BETA/omuletachou/pull/263 (aberto, aguardando
  merge do LT).
- Removido o seletor "Desconto mínimo" de `FilterBar.tsx` + código órfão (state, handler, CSS,
  referências em `page.tsx`/`lib/api.ts`) — checklist da especificação técnica cumprido
  integralmente; grep confirmou ausência de qualquer referência residual a `minDiscount` fora do
  teste de regressão dedicado.
- Testes: 116/116 passando (100%), cobertura de `FilterBar.tsx` 92%/`page.tsx` 100% linhas/`api.ts`
  100% (threshold do projeto é 80%). `npm run build` sucesso; `npm start` inicializa sem erro.
- Sem ambiguidade/dúvida técnica — não precisou de decisão de arquitetura.
- T-02 (sub-issue #262) segue pendente, mesmo arquivo (`FilterBar.tsx`) em região diferente
  (`PriceGroup`) — LT funde sequencialmente conforme nota de dependência em `tasks.md`.

## Implementação — T-02 / Sub-issue #262 (concluída pelo Dev, 2026-08-20)
- Branch `feature/ISSUE-262-fix-slider-preco-minmax` (worktree
  `.worktrees/feature-ISSUE-262-fix-slider-preco-minmax`), base `desenv`.
- PR feature→desenv: #264 — https://github.com/DQM-BETA/omuletachou/pull/264 (aberto, aguardando
  merge do LT — fundir **depois** do #263, mesmo arquivo `FilterBar.tsx`, regiões diferentes).
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

## Notas
- Diretório duplicado `documentacoes/ISSUE-230-revisar-filtros-site-público/` (com acento) —
  confirmado como artefato órfão de stub inicial do Coordenador (backlog, pré-refinamento do PM),
  sem conteúdo não capturado no diretório correto (sem acento). **Removido** por este agente.
- openspec change criado via `npx @fission-ai/openspec new change` — nome exigido em kebab-case
  minúsculo (`issue-230-...`, não `ISSUE-230-...`); path real difere do padrão usado em docs_path
  (que mantém `ISSUE-230` maiúsculo por convenção da squad).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | PM Fase 1 | pm-analista-negocios | Sonnet | (preencher pela sessão principal via usage do HANDOFF) | - | - |
| 2 | PM Fase 2 | pm-analista-negocios | Sonnet | (preencher pela sessão principal via usage do HANDOFF) | - | - |
| 3 | Refinamento Técnico | lider-tecnico | Sonnet | (preencher pela sessão principal via usage do HANDOFF) | - | - |
