---
issue: 154
titulo: "bug: Site público (website) sem nenhum estilo CSS implementado — apenas HTML puro"
etapa_atual: "Concluído"
ultimo_agente: coordenador
rota: normal
openspec_change: repos/omuletachou/openspec/changes/issue-154-site-sem-css
tech_stacks: [nodejs]
repos:
  omuletachou: main
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-154-site-sem-css
openspec_path: repos/omuletachou/openspec/changes/issue-154-site-sem-css
status_comment_id: "5293952020"
sub_issues: ["#156 (stack:nodejs, task_id:T-01) — fechada novamente (fix header ausente em deal-detail, PR #160 mesclado)"]
desenv_tasks_merged: ["#156"]
pr_feature: "#160 (feature/ISSUE-156-header-deal-detail -> desenv, squash merged, delete-branch) — fix pontual do achado de QA (PR #157 é histórico do ciclo anterior)"
sub_issues_frontend: {}
pr_homologacao: "#161 (desenv -> homolog, merge commit, MERGEADO) — cobriu o fix de #156 (PR #160); PR anterior #158 já mesclado/fechado em ciclo anterior"
pr_release: "#162 (homolog -> main, merge commit, MERGEADO) — Gerente aprovou, merge executado com sucesso"
code_review_homolog_pr: 161
qa_status: aprovado
figma_url: https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library
blockers: nenhum
createdAt: "2026-08-14T13:38:17Z"
closedAt: "2026-08-14T22:30:00Z"
merge_commit: "feade5c8cfb4b83390914d3a50a8c43dcce396be"
---

## Resumo

Site público (Next.js) renderiza como HTML puro sem estilo CSS — classes BEM estruturadas mas sem implementação visual. Crítico para UX.

## Contexto

- Confirmado visualmente: site rodando localmente via Docker mostra apenas texto corrido, sem layout, grid ou cards.
- Arquivos CSS vazios (boilerplate não customizado).
- Classes BEM bem estruturadas nos componentes (DealCard, Header) e páginas, mas sem regras CSS correspondentes.
- Pipeline anterior não tinha validação visual em navegador (foco em API e Jest).
- Causa raiz do gap de QA registrada em `.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md`: Gate Visual do QA depende do script `test:visual` no `package.json`, inexistente em `website`/`dashboard` desde o scaffold — Gate sempre resolveu N/A.

## PM Fase 1 (levantamento) — concluído

Bug técnico claro sobre feature já especificada (Issues #12/#94/#95/#96/#117) — sem requisitos de negócio novos. Levantamento restrito a decisões do Gerente, postado como comentário na Issue #154 (5 perguntas: identidade visual, prioridade de telas, escopo do Playwright/`test:visual`, responsividade, relação com PWA).

## Gate 1 — respostas do Gerente (comentário `5294013444`)

1. **Identidade visual**: sem brand book formal — âncora `theme-color: #e63946` (já em `app/layout.tsx`) + design system genérico do Figma da squad + conceito de site de ofertas/cupons de afiliado.
2. **Telas**: as 3 (Home, categoria, `deal-detail`) nesta rodada — mesma aplicação de design system, sem fatiamento.
3. **Playwright/`test:visual`**: entra no escopo desta issue para `website`. `dashboard` (Angular) vira issue técnica separada (#155, rota `backlog`).
4. **Mobile-first**: obrigatório — maioria do tráfego é mobile.
5. **PWA (Issue #117)**: CSS novo deve ser consistente com o manifest existente (cor de tema `#e63946`, ícones) — não é independente.

## PM Fase 2 (PRD + critérios de aceite) — concluído

- `openspec_change` criado: `openspec/changes/issue-154-site-sem-css/proposal.md` (objetivo, usuários, casos de uso/exceção, regras de negócio, integrações, restrições, definição de pronto).
- `criterios-aceite.md` escrito em `docs_path` (15 critérios Given/When/Then organizados por tema: identidade visual, cobertura de classes CSS — critério objetivo CA-3/CA-4 — estilização por tela, mobile-first, consistência com PWA, setup Playwright/`test:visual`, + 3 critérios transversais).
- Sumário do PRD postado como comentário na Issue #154.

### Avaliação de ambiguidade arquitetural — sem Arquiteto

Não há ambiguidade arquitetural real: não é decisão de arquitetura de sistema (sem escolha de stack, integração externa nova ou infraestrutura). É aplicação de CSS (CSS Modules e/ou globals — decisão de organização de arquivos, não de arquitetura) sobre uma estrutura de componentes/classes BEM já pronta e estável desde a Issue #12. Todas as decisões de produto relevantes (identidade visual, escopo de telas, mobile-first, integração com PWA) já foram resolvidas pelo Gerente no Gate 1. Segue direto para o **Líder Técnico**, que decide a organização técnica do CSS (module vs. global vs. escopo por componente), o setup do Playwright (`test:visual`) e o task breakdown (provavelmente sub-issue única de Dev, dado o escopo coeso).

## Líder Técnico — Refinamento Técnico — concluído

- `design.md` resumido escrito em `openspec_path` (proposal.md do PM já cobria a maior parte; sem Arquiteto envolvido, sem decisão de arquitetura de sistema em jogo).
- `especificacao-tecnica.md` escrito em `docs_path`: decisão de CSS global puro (não CSS Modules — classes BEM já são strings literais nos componentes, migrar exigiria reescrever componentes, fora de escopo por CA-T2; sem dependência nova). Organização recomendada em partials (`app/styles/tokens.css`, `reset.css`, `layout.css`, `deal-card.css`, `deal-detail.css`) importados via `@import` em `app/globals.css`. Estrutura de tokens CSS custom properties definida (nomes); valores exatos (exceto `--color-primary: #e63946`, já fixado) ficam a cargo do UX/UI a partir do Figma. Inventário completo das ~35 classes BEM a cobrir (base para CA-3). Fix do bug raiz: `import './globals.css'` ausente em `app/layout.tsx`. Remoção de `app/page.module.css` (órfão). Estratégia mobile-first com breakpoints `min-width` (640px/1024px). Setup do Playwright (`playwright.config.ts`, `e2e/helpers.ts`, `e2e/visual.spec.ts`) adaptado de `repos/dqm-digital-app/playwright.config.ts`: STAGING_URL first + webServer local fallback; descoberta de categoria/slug reais via `/sitemap.xml` (sem hardcode — catálogo não tem seed fixo, dados vêm de scraping real) em vez de fixture.
- `tasks.md` escrito em `openspec_path`: uma única tarefa (T-01), critérios de aceite + contexto técnico completo para o Dev.
- **Task breakdown: uma única sub-issue** — CSS e `test:visual` são a mesma mudança de projeto (`website/`), sem fronteira de PR/teste independente entre si (mesmo raciocínio da Issue #15). Sub-issue criada: **#156** (`stack:nodejs`, `task_id:T-01`).
- Próximo agente: **UX/UI** (produz spec visual a partir do Figma — valores de tokens, layout detalhado das 3 telas) antes do Dev, conforme decisão explícita do Gerente (é trabalho visual real, não CSS improvisado).

## Próximas Etapas

1. PM Fase 1: requisitos visuais — **feito**
2. Gate 1 — **feito**
3. PM Fase 2: PRD + critérios de aceite — **feito**
4. Líder Técnico: refinamento técnico (design.md + especificacao-tecnica.md + tasks.md + sub-issue #156) — **feito**
5. UX/UI: spec visual a partir do design system do Figma — **feito**
6. Dev: implementação (CSS + `test:visual`) na sub-issue #156 — **feito**
7. Líder Técnico: merge PR #157 → desenv + PR homologação #158 (desenv→homolog) — **feito**
8. Code Review + QA: validação visual (novo checkpoint — Gate Visual passa a funcionar de fato) — **feito, QA reprovou (header ausente em deal-detail)**
9. Líder Técnico: mapear falha do QA → reabrir sub-issue #156 — **feito**
10. Dev: fix pontual (`<Header />` em `app/oferta/[slug]/page.tsx`) — **feito** (PR #160)
11. Líder Técnico: merge PR #160 → desenv + novo PR de homologação #161 (desenv→homolog) — **feito**
12. Sessão principal: `/code-review` + Code Review — **feito, aprovado (PR #161 merge desenv→homolog concluído)**
13. QA (rodada 2, pós-fix) — **feito, aprovado**
14. Líder Técnico: PR homolog→main → Gate 2 (Gerente) → merge main — **feito**

## UX/UI — Spec visual — concluído

`ux-ui-spec.md` escrito em `docs_path`: consultado o Figma do design system da squad (`get_figma_data`) — arquivo ainda no estado padrão "boas-vindas", nunca customizado com paleta/tokens reais do produto (apenas estilos de texto nomeados reaproveitáveis, família Work Sans). Documentado explicitamente para rastreabilidade. Spec produzida com bloco `:root` de tokens CSS completo (`--color-primary: #e63946` + derivações hover/dark/light em HSL, paleta neutra quente, tipografia Work Sans com fallback, espaçamento 8pt, raio, sombra, breakpoints), mapeamento de 100% das ~35 classes BEM do inventário para regras CSS concretas (valores reais, não genéricos) com estados default/hover/active/disabled documentados, composição das 3 telas, grid responsivo mobile-first (1→2→3→4 colunas), touch targets ≥40-44px, heurísticas de Nielsen traduzidas em critérios verificáveis. Nenhuma mudança de `.tsx` necessária (respeita CA-T2). Comentário de resumo postado na Issue #154.

Nota: commit/push e atualização deste `estado.md` feitos pela sessão principal (fora do escopo de ferramentas do agente UX/UI).

## Próximo agente: Dev (`stack:nodejs`, sub-issue #156)

## Dev (nodejs) — Implementação — concluído

Branch `fix/156-css-website` (worktree `.worktrees/fix-156-css-website`, base `desenv`). Implementado exatamente conforme `especificacao-tecnica.md` + `ux-ui-spec.md`:

- **CSS**: `app/globals.css` reduzido a 5 `@import`s; partials criados em `app/styles/` (`tokens.css`, `reset.css`, `layout.css`, `deal-card.css`, `deal-detail.css`) com os valores exatos da spec do UX/UI (cor de marca, neutros, tipografia Work Sans, espaçamento 8pt, raio, sombra). Mobile-first com breakpoints `min-width` 640/1024/1280px.
- **Bug raiz corrigido**: `import './globals.css'` adicionado no topo de `app/layout.tsx`.
- **Limpeza**: `app/page.module.css` removido (órfão, nunca importado).
- **CA-3 verificado programaticamente**: extraídas todas as classes BEM usadas em `.tsx` (via grep) e cruzadas contra os seletores dos 5 arquivos CSS — 100% cobertas (nenhuma classe sem regra correspondente).
- **Playwright/`test:visual`**: `@playwright/test` instalado (devDependency, auto-registrado no `package.json` pelo `npm install`) + chromium baixado. `playwright.config.ts` (STAGING_URL first, webServer local fallback, viewport mobile via device Pixel 7, `outputDir`/screenshots redirecionáveis via env `SCREENSHOTS_DIR`). `e2e/helpers.ts` (`getRealCategoriaAndSlug` via `/sitemap.xml`, sem hardcode). `e2e/visual.spec.ts` cobrindo as 3 telas (Home, categoria, `deal-detail`) com screenshot + assert de "sem overflow horizontal" (CA-9). `jest.config.js` ajustado (`testPathIgnorePatterns`) para não tentar rodar os specs do Playwright (runner diferente).

### Validação executada (não só suposta)

- `npm test`: **79/79 passando**, sem regressão.
- `npm run build`: sem erros de TypeScript.
- Stack Docker real (`docker compose up -d --build db api website`, `.env` local descartável a partir de `.env.example`, `docker-compose.override.yml` local descartável só para expor portas de teste — nenhum dos dois commitado) com **5 produtos seedados via SQL direto** (`INSERT INTO products ...`, catálogo vazio no ambiente local, tabela/colunas via `ProductConfiguration.cs`) cobrindo os casos de exceção da spec (sem desconto, sem `affiliate_link`/CTA desabilitado, categoria com múltiplos produtos).
- `STAGING_URL=http://localhost:3000 SCREENSHOTS_DIR={docs_path}/screenshots npm run test:visual`: **3/3 passando**, PNGs confirmados em `documentacoes/ISSUE-154-site-sem-css/screenshots/` (`home.png`, `categoria.png`, `deal-detail.png`) — **inspecionados visualmente** (não só a suíte verde): grid de cards, header sticky com chips ativos, badge `-44%`, preço riscado/atual, CTA vermelho, CTA desabilitado (cinza, "Indisponível"), estado vazio de categoria estilizado (bloco tracejado + ícone). Screenshot adicional em desktop (1280px, ad-hoc, não commitado) confirmou grid em 4 colunas e header sem scroll horizontal.
- Ambiente Docker removido ao final (`docker compose down -v`), `.env`/`docker-compose.override.yml` locais apagados — nenhum resíduo no worktree.

### Achado fora de escopo (não corrigido, CA-T2 proíbe)

Bug pré-existente identificado durante a validação visual, **não relacionado a CSS**: categorias com espaço no nome (ex. "Casa e Decoracao") retornam o estado vazio em vez do grid — `app/categoria/[categoria]/page.tsx` usa `params.categoria` sem tratamento consistente entre `formatCategoriaLabel` (decodifica) e `fetchByCategory` (reenvia como veio), causando mismatch de encoding contra a API. Categorias de uma palavra (ex. "Eletronicos") funcionam normalmente — confirmado que o CSS de ambos os estados (grid populado e vazio) está correto; é puramente um bug de lógica de fetch, fora do escopo desta issue (CA-T2 proíbe alterar `lib/api.ts`/rotas/fetch aqui). Registrado no PR #157 para o LT avaliar abertura de issue técnica separada.

PR aberto: **#157** (`fix/156-css-website` → `desenv`), aguardando merge do Líder Técnico.

## Líder Técnico — Merge + PR de homologação — concluído

- Revisado o diff completo do PR #157 (`gh pr diff`): confirmado escopo apenas CSS + setup Playwright — único ponto de "lógica" é o `import './globals.css'` em `layout.tsx` (esperado, é o fix do bug raiz) e a remoção do `page.module.css` órfão (esperado). `jest.config.js`/`package.json`/`package-lock.json` só configuram o runner de teste (ignorar `e2e/` no Jest, adicionar `@playwright/test` como devDependency e o script `test:visual`). Sem mudança de lógica de negócio.
- Merge squash do PR #157 → `desenv` (`gh pr merge 157 --squash --delete-branch`), branch remota `fix/156-css-website` deletada.
- Sub-issue #156 fechada com comentário de resumo (`gh issue close 156 --reason completed`).
- PR de homologação criado: **#158** (`desenv` → `homolog`, merge commit — nunca squash), cobrindo a Issue #154 completa.
- **Issue técnica separada aberta para o bug pré-existente** (categorias com espaço no nome, mismatch de encoding `formatCategoriaLabel`/`fetchByCategory`): **#159**, label `bug`, rota `backlog` (documentar/planejar; não trabalhar agora), referenciando #154/#157 como origem do achado.
- `repo_path` deixado em `desenv`, atualizado com o merge (`git pull origin desenv` — fast-forward, PR #157 incorporado).

## Code Review — PR #158 (validação final)

Segunda camada de gate (validação ao vivo, execução real — não análise estática). `/code-review` (plugin Anthropic) já havia rodado sem achados bloqueantes (comentário do PR: "No issues found. Checked for bugs and CLAUDE.md compliance." — única nota sub-bar, não bloqueante: naming de branch `fix/156-...` em vez de `feature/ISSUE-156-...`, mas consistente com o padrão já estabelecido no repo).

**Execução realizada:**

1. `git fetch && git checkout desenv && git pull origin desenv` — HEAD confirmado em `26cfa2c` (inclui commits dos PRs #157/#158 antes do merge).
2. `npm test` (Jest, `website/`): **79/79 passando**, sem regressão.
3. `npm run build` (`website/`): **primeira tentativa falhou** — `Cannot find module '@playwright/test'` (type error no `playwright.config.ts` durante o type-check do build). Causa: `node_modules` local desatualizado em relação ao `package-lock.json` do PR (ambiente do CR, não bug do PR). Rodado `npm install` para sincronizar → build limpo na segunda tentativa (5 rotas geradas, sem erros de tipo).
4. `STAGING_URL=http://localhost:3000 SCREENSHOTS_DIR=... npx playwright test` (`website/`, contra o container Docker real, não o `webServer` local): **3/3 passando** (Home, categoria, `deal-detail`).
5. **Validação visual real** (o ponto central da issue): `docker compose up -d --build db api website` com `.env` local descartável (a partir de `.env.example`) + `docker-compose.override.yml` local descartável só para expor portas ao host (nenhum dos dois commitado — `.env` já coberto por `.gitignore`; override removido manualmente ao final). Seed de 4 produtos `Published` via `INSERT INTO products ...` direto no Postgres (catálogo vazio no ambiente). `curl http://localhost:3000/` confirmou `<link rel="stylesheet">` real (~11KB de CSS compilado, não vazio) e o HTML das classes BEM (`deals-grid`, `deal-card`, `deal-card__badge` etc.). **Screenshots inspecionados visualmente** (Home, categoria, `deal-detail`, gerados pelo próprio `test:visual` contra o container): grid de cards estilizado (1 coluna em mobile 375px), cor de marca `#e63946` nos badges de desconto/CTA/chip ativo do header, tipografia aplicada, preço atual/riscado, botão "Ver oferta"/"Comprar agora" com border-radius e cor de marca. Confirma de fato a correção do bug raiz (site antes renderizava só texto corrido) — não apenas suíte verde.
6. Checklist de veto:
   - Sem segredos commitados: `.env` (gitignored) e `docker-compose.override.yml` (temporário) usados só localmente, removidos ao final (`git status --short` limpo após limpeza).
   - Conformidade com `repos/omuletachou/CLAUDE.md`: convenções de branch/commit/merge respeitadas pelo LT (squash feature→desenv, merge commit desenv→homolog).
   - Integração real: build+boot via Docker real (não mock), API real (`ProductsController`/`PublicController`), Postgres real, teste visual contra o container real via `STAGING_URL` (não o fallback `webServer` local isolado).
   - Sem teste-lixo: os 3 specs de `visual.spec.ts` fazem asserts reais (overflow horizontal, visibilidade de elementos, screenshot) além do óbvio.
   - `.first()`/`.nth()`/`.last()`: nenhuma ocorrência em `e2e/visual.spec.ts` ou `e2e/helpers.ts` (confirmado via diff do PR) — sem veto aplicável.
   - Diff do PR (`gh pr diff 158`) confere com a descrição: CSS global real em `website/app/styles/` (5 partials), fix do bug raiz (`import './globals.css'` em `app/layout.tsx`), remoção de `page.module.css` órfão, setup completo do Playwright (`playwright.config.ts`, `e2e/helpers.ts`, `e2e/visual.spec.ts`, scripts em `package.json`). Sem segredos no diff (grep por password/secret/token sem resultado).
7. Ambiente Docker removido (`docker compose down -v`) e `.env`/`docker-compose.override.yml` apagados ao final — `git status --short` limpo.

**Veredito: aprovado.** Merge executado: `gh pr merge 158 --repo DQM-BETA/omuletachou --merge` (merge commit `6e65564d8e4172c5d437af2bb99e00245ee26424`, `desenv` → `homolog`). `repo_path` deixado checked out em `desenv`.

Observação para o QA: o Gate Visual passa a ser aplicável de fato pela primeira vez neste projeto (antes sempre N/A por falta do script `test:visual`) — reforçar inspeção visual real (não só suíte verde), como feito aqui.

## QA — homolog (rodada 1)

**Status: REPROVADO** (relatório da rodada 1 preservado no histórico de `relatorio-qa.md`).

Branch sincronizada (`git fetch && git checkout homolog && git pull origin homolog`), commit `6e65564d8e4172c5d437af2bb99e00245ee26424` (PR #158) confirmado em `git log`.

Nota de sincronismo: o `estado.md` encontrado em `homolog` no momento da checagem ainda dizia "aguardando /code-review" (desatualizado) — o `estado.md` de `desenv` já tinha a seção "Code Review — PR #158" (aprovado, merge feito). Validação prosseguiu normalmente pois o código/commit já estava confirmado em `homolog` (`git log`); a defasagem era só de bookkeeping do `estado.md` entre branches, não do código.

- `npm test` (website/): **79/79 passando**, sem regressão.
- `npm run build` (via docker build): sem erros de TypeScript — CA-T1 OK.
- **Validação integrada (d3)**: stack Docker real subida a partir de `homolog` (`db`+`api`+`website`), `.env`/`docker-compose.override.yml` locais descartáveis (portas 8080/3000 expostas para teste), catálogo vazio seedado via SQL direto (5 produtos, casos com/sem desconto, com/sem `affiliate_link`, categorias distintas). Confirmado via `curl` que o HTML carrega com `<link rel="stylesheet">` e o CSS compilado contém os tokens da marca (`--color-primary:#e63946`).
- **Gate Visual obrigatório (d2) — primeira execução real para `website`**: `STAGING_URL=http://localhost:3000 SCREENSHOTS_DIR=documentacoes/ISSUE-154-site-sem-css/screenshots-qa npm run test:visual` → **3/3 passed**. Screenshots inspecionados visualmente (Home, Categoria, `deal-detail` + 1 ad-hoc de categoria vazia para CA-7).
  - Home e Categoria: header (`site-header`) visível exatamente 1x, sem duplicação, layout condizente com `ux-ui-spec.md`. **OK.**
  - **`deal-detail`: header COMPLETAMENTE AUSENTE (0x)** — confirmado por inspeção visual do PNG e por `curl .../oferta/{slug} | grep -c site-header` → 0 ocorrências. Causa raiz: `website/app/oferta/[slug]/page.tsx` nunca importou/renderizou `<Header />` (diferente de `app/page.tsx` e `app/categoria/[categoria]/page.tsx`) — bug **pré-existente desde a Issue #95** (`git log` confirma o arquivo não foi tocado pelo PR #157/#158 desta issue), só agora capturado porque é a primeira vez que o Gate Visual do QA de fato dispara (CA-15). O Code Review anterior também não pegou esse achado (sua inspeção visual não cobriu ausência total de elemento).
  - Resto da tela `deal-detail` (mídia, preço, badge, CTA, "Mais ofertas") está corretamente estilizado.
- **Critérios reprovados: CA-1 e CA-8** (mesma causa raiz — header ausente em `deal-detail`). Demais 13 CAs + 3 transversais (T1-T3): PASS.
- Ambiente Docker removido ao final (`docker compose down -v`), `.env`/`docker-compose.override.yml` apagados — sem resíduo.
- `repo_path` deixado em `desenv` ao final.

**Encaminhamento**: issue funcional (não inconsistência de negócio) — Líder Técnico mapeia a falha (fix pequeno: importar/renderizar `<Header />` em `app/oferta/[slug]/page.tsx`, reaproveitando componente e CSS já estilizados nesta mesma issue) e aciona Dev.

## Líder Técnico — Mapeamento de falha do QA — concluído

- Causa raiz confirmada por leitura direta do código: `website/app/oferta/[slug]/page.tsx` não importa nem renderiza `<Header />`; comparado com `website/app/page.tsx` (`import Header from '@/components/Header'` + `<Header activePlatform={platform} />`) e `website/app/categoria/[categoria]/page.tsx` (`import Header ...` + `<Header />`), ambos corretos. O escopo original de #156 (item 1 do corpo da sub-issue) já listava `app/oferta/[slug]/page.tsx` entre as 3 páginas a cobrir — a renderização do Header nessa página ficou de fora na implementação, não é escopo novo.
- **Decisão de escopo**: reabri a sub-issue **#156** (`gh issue reopen 156`) em vez de criar sub-issue nova — mesmo componente/CSS já prontos, fix de 1 arquivo (import + `<Header />`), mesmo task_id T-01. Comentário de detalhamento postado em #156 (causa raiz, fix sugerido, critérios de aceite para a reabertura, contexto técnico/branch).
- Comentário de resumo postado na Issue #154 mapeando a falha para o Dev.
- `estado.md`: `desenv_tasks_merged` esvaziado (remove #156, que volta a ficar pendente até novo merge); `sub_issues` atualizado para refletir a reabertura; `etapa_atual` aponta para Dev.
- Próxima branch esperada do Dev: `feature/ISSUE-156-header-deal-detail` a partir de `desenv` atualizada (já inclui o merge do PR #157/#158).

## Dev (nodejs) — Fix pós-QA (#156, Header ausente em deal-detail) — concluído

Worktree `.worktrees/feature-ISSUE-156-header-deal-detail`, branch `feature/ISSUE-156-header-deal-detail` (base `desenv`, já inclui merge dos PRs #157/#158).

- **TDD (RED→GREEN)**: adicionado teste de regressão em `website/app/oferta/[slug]/page.test.tsx` (`CA-1/CA-8 (regressão #156): renderiza o <Header /> exatamente 1x`) — confirmado RED (0 ocorrências de `.site-header`) antes do fix.
- **Fix**: `website/app/oferta/[slug]/page.tsx` — adicionado `import Header from '@/components/Header'` e `<Header />` dentro de `<main>`, antes do script JSON-LD/`<DealDetail />`, seguindo exatamente o padrão de `app/categoria/[categoria]/page.tsx`. Nenhuma mudança de CSS/dados/rotas/API.
- **Gate obrigatório (busca de testes que referenciam o módulo)**: `Grep` por `Header`/`OfertaPage`/`DealDetail` em `*.test.*` — apenas `page.test.tsx` (atualizado) e `Header.test.tsx` (sem relação com esta página); nenhum teste com assertiva contradizendo o fix.

### Validação executada

- `npm test`: **80/80 passando** (79 pré-existentes + 1 novo), sem regressão.
- `npm run build`: sem erros de TypeScript.
- `SCREENSHOTS_DIR=documentacoes/ISSUE-154-site-sem-css/screenshots npm run test:visual` (contra stack Docker real, `STAGING_URL=http://localhost:3000`): **3/3 passando**. PNGs sobrescritos e **inspecionados visualmente**: `deal-detail.png` agora mostra o header (`O Mulet Achou` + chips de plataforma) exatamente 1x no topo, sem duplicação; `home.png`/`categoria.png` sem regressão (header 1x, mesmo layout de antes).
- Stack Docker real (`docker compose up -d --build db api website`, `.env`/`docker-compose.override.yml` locais descartáveis no worktree — não commitados) com 1 produto seedado via SQL direto (`status=Published`). `curl http://localhost:3000/oferta/{slug} | grep -c site-header` → **1** (era 0 antes do fix, confirmado por reprodução do comando do QA).
- Ambiente Docker removido ao final (`docker compose down -v` + `docker rmi` das imagens locais buildadas), `.env`/`docker-compose.override.yml` apagados — sem resíduo (`git status --short` limpo no worktree).

PR aberto: **#160** (`feature/ISSUE-156-header-deal-detail` → `desenv`), aguardando merge do Líder Técnico. Worktree removido (`git worktree remove`). `repo_path` deixado checked out em `desenv`.

## Líder Técnico — Merge PR #160 + novo PR de homologação — concluído

Diferente do ciclo anterior: o PR de homologação anterior (#158, `desenv`→`homolog`) já estava mesclado e fechado, então o fix não pôde absorver em um PR aberto — foi necessário abrir um PR de homologação novo.

- `git pull origin desenv` — já atualizado (`up to date`).
- Revisado o diff completo do PR #160 (`gh pr diff 160`): confirmado escopo estritamente pontual — apenas `import Header from '@/components/Header'` + `<Header />` em `website/app/oferta/[slug]/page.tsx` (seguindo o padrão de `app/categoria/[categoria]/page.tsx`) e o teste de regressão novo (`CA-1/CA-8`) em `page.test.tsx`. Nenhuma mudança de CSS, dados, rotas ou API.
- Merge squash do PR #160 → `desenv` (`gh pr merge 160 --squash --delete-branch`), branch remota `feature/ISSUE-156-header-deal-detail` deletada. Merge commit `fec0bb66bc4b5a309c0396ce52a18350f3f8f45c`.
- `git checkout desenv && git pull origin desenv` — fast-forward, PR #160 incorporado (HEAD `fec0bb6`).
- Sub-issue #156 fechada novamente com comentário de resumo (`gh issue close 156 --reason completed`) — já tinha sido reaberta uma vez para este fix.
- **Novo PR de homologação criado**: **#161** (`desenv` → `homolog`, merge commit — nunca squash), referenciando no corpo que corrige o achado de QA do ciclo anterior (PR #158 / `relatorio-qa.md`), já que o PR #158 anterior já estava mesclado e fechado.
- `repo_path` deixado checked out em `desenv`.

## Code Review — PR #161 (validação final)

Segunda camada de gate para o fix pontual pós-QA (achado trivial e contido — sessão principal já havia feito uma revalidação de escopo do diff, comentário [#161](https://github.com/DQM-BETA/omuletachou/pull/161#issuecomment-5294886574), sem necessidade de rodar `/code-review` completo novamente; único comentário de `/code-review` no PR foi a revalidação da própria sessão principal, sem achados bloqueantes).

**Execução realizada:**

1. `git fetch && git checkout desenv && git pull origin desenv` — já em `desenv`, `d2722ce` (up to date, inclui commit `fec0bb6` do fix `<Header />` + `1f2461b`/`d2722ce` do ledger).
2. Confirmado no código (`git show desenv:website/app/oferta/[slug]/page.tsx`): `import Header from '@/components/Header'` (linha 5) + `<Header />` renderizado dentro de `<main>` (linha 62), antes do JSON-LD/`<DealDetail />`.
3. `npm test` (Jest, `website/`): **80/80 passando** (14 test suites), sem regressão — confirma a contagem esperada (79 pré-existentes + 1 novo de regressão CA-1/CA-8).
4. `npm run build` (`website/`): `✓ Compiled successfully`, `Linting and checking validity of types...` sem erros, 5 rotas geradas (incl. `/oferta/[slug]`).
5. **Stack Docker real**: `.env` local descartável (a partir de `.env.example`, valores dummy — `DB_PASSWORD`/`JWT_SIGNING_KEY` gerados localmente, nunca reais) + `docker-compose.override.yml` local descartável (expõe 5432/8080/3000 ao host — serviços não têm `ports:` por padrão). `docker compose up -d --build db api website`: build completo (API .NET + Website Next.js standalone), todos os containers `healthy`/`Up`. Catálogo vazio no ambiente local → **2 produtos seedados via SQL direto** (`INSERT INTO products ...`, schema conferido via `\d products`; `status=2` = `Published`, `platform` 0/1 = Amazon/MercadoLivre) — "Fone Bluetooth XPTO Pro" (`slug=fone-bluetooth-xpto-pro`, com desconto/CTA habilitado) e "Mouse Gamer RGB" (`slug=mouse-gamer-rgb`). `curl http://localhost:8080/api/public/deals` confirmou os 2 produtos retornados pela API real.
6. **Reprodução exata do comando que o QA usou para reprovar**: `curl http://localhost:3000/oferta/fone-bluetooth-xpto-pro | grep -c site-header` → **1** (era 0 antes do fix, no ciclo anterior/PR #158). Confirmado também para `/oferta/mouse-gamer-rgb` → **1**, e ausência de duplicação em `/` (Home) → **1** e `/categoria/Eletronicos` → **1**. Fix comprovado ao vivo, não apenas por leitura de diff.
7. **Gate Visual**: `SCREENSHOTS_DIR=.../screenshots-cr npm run test:visual` (`STAGING_URL=http://localhost:3000`, contra o container Docker real, não o `webServer` local): **3/3 passed** (Home, Categoria, `deal-detail`). Screenshots **inspecionados visualmente**:
   - `deal-detail.png`: header ("O Mulet Achou" + chips de plataforma "Todas"/"Amazon"/"Me...") visível **exatamente 1x** no topo, sem duplicação, seguido de mídia do produto, título, categoria, preço atual/riscado, CTA "Comprar agora →" e seção "Mais ofertas" reaproveitando `.deal-card`. Layout completo e coerente com `ux-ui-spec.md`.
   - `home.png`: header 1x, sem duplicação, grid de cards com badge `%OFF`, preço, CTA "Ver oferta →", paginação "Página 1 de 1". Sem regressão.
   - `categoria.png`: header 1x, sem duplicação, mesmo grid/card da Home, título "Eletronicos". Sem regressão.
   - Nenhuma quebra em Home/Categoria — o fix é estritamente aditivo à página de detalhe.
8. Checklist de veto:
   - **Sem segredos commitados**: `.env`/`docker-compose.override.yml` locais descartáveis (gitignored/nunca adicionados ao stage), removidos ao final; `git status --short` sem resíduo relacionado. Grep no diff do PR por `password|secret|api[_-]?key|token` só retorna ocorrências em texto de documentação (`estado.md`/`relatorio-qa.md`), não em código/config.
   - **Conformidade com `repos/omuletachou/CLAUDE.md`**: branch `feature/ISSUE-156-header-deal-detail` (padrão correto `feature/ISSUE-NNN-descricao`), squash feature→desenv, merge commit desenv→homolog (nunca squash) — respeitado pelo LT.
   - **Integração real**: build+boot via Docker real (não mock) — API .NET real, Postgres real, teste de regressão em `page.test.tsx` renderiza o Server Component `OfertaPage` de verdade (`render(jsx)` do React Testing Library sobre o componente async real, não um mock de `<Header>`), reprodução do bug via `curl` contra o container real.
   - **Sem teste-lixo**: teste de regressão novo (`page.test.tsx`) faz asserts reais e específicos (`container.querySelectorAll('.site-header').length` === 1 + presença do link "O Mulet Achou"), não trivial/vazio.
   - **`.first()`/`.nth()`/`.last()`**: nenhuma ocorrência no diff do PR #161 (`gh pr diff 161` grepado) — sem veto aplicável.
   - **Diff mínimo e contido** (`gh pr diff 161`): apenas `import Header` + `<Header />` em `page.tsx` (2 linhas) + 1 teste novo em `page.test.tsx` (11 linhas) + atualização de `estado.md`. Nenhuma mudança de CSS/dados/rotas/API/config de deploy.
9. Ambiente Docker removido (`docker compose down -v`), imagens locais buildadas removidas (`docker rmi omuletachou-website omuletachou-api`), `.env`/`docker-compose.override.yml` apagados, `screenshots-cr/` (evidência temporária deste CR, não commitada) removido ao final — `git status --short` limpo (exceto artefatos pré-existentes não relacionados a este PR em `documentacoes/ISSUE-154-site-sem-css/screenshots/*.png`, já modificados no worktree antes desta validação, não tocados por este Code Review).

**Veredito: aprovado.** Merge executado: `gh pr merge 161 --repo DQM-BETA/omuletachou --merge` (merge commit `e418d089e3bde005590afd11a5c59cb84ccafab6`, `desenv` → `homolog`). `repo_path` deixado checked out em `desenv`.

Achado original do QA (`relatorio-qa.md`: CA-1/CA-8, header ausente em `/oferta/{slug}`) confirmado resolvido ao vivo — bug de duplicação/ausência estrutural não é mais reproduzível.

## QA — homolog (rodada 2, pós-fix)

**Status: APROVADO** (relatório completo, sobrescrito com o veredito final desta rodada, em `relatorio-qa.md`).

Branch sincronizada: `git fetch origin && git checkout homolog && git pull origin homolog` — fast-forward de `6e65564` para `e418d08` (PR #161). Commit `e418d089e3bde005590afd11a5c59cb84ccafab6` confirmado em `git log --oneline -5`.

- `npm test` (`website/`): **80/80 passando** (14 test suites), sem regressão — inclui o novo teste de regressão do fix (`CA-1/CA-8 (regressão #156)`).
- Código confirmado: `website/app/oferta/[slug]/page.tsx` importa `Header` (linha 5) e renderiza `<Header />` dentro de `<main>` (linha 62), antes do JSON-LD/`<DealDetail />` — igual ao padrão de `app/page.tsx`/`app/categoria/[categoria]/page.tsx`.
- **Validação integrada (d3)**: stack Docker real subida a partir de `homolog` (`docker compose up -d --build db api website`), `.env`/`docker-compose.override.yml` locais descartáveis (portas 5432/8080/3000 expostas), todos os containers `healthy`. Catálogo vazio → **5 produtos seedados via SQL direto** (2 "Eletronicos" com desconto, 1 sem desconto, 1 "Casa" sem `affiliate_link`/CTA desabilitado, 1 "Casa" com desconto — categorias de 1 palavra só, evitando o bug conhecido #159 fora de escopo). `curl http://localhost:8080/api/public/deals` confirmou os 5 produtos servidos pela API real.
- **Reprodução exata do comando que reprovou na rodada 1**: `curl http://localhost:3000/oferta/fone-bluetooth-xpto-pro | grep -c site-header` → **1** (era 0 antes do fix). Confirmado também Home (`/`) → 1 e categoria (`/categoria/Eletronicos`) → 1 — sem duplicação em nenhuma tela.
- **Gate Visual obrigatório (d2)**: `STAGING_URL=http://localhost:3000 SCREENSHOTS_DIR=documentacoes/ISSUE-154-site-sem-css/screenshots-qa-r2 npm run test:visual` → **3/3 passed** (Home, Categoria, `deal-detail`). Screenshots inspecionados visualmente:
  - **`deal-detail.png`**: header ("O Mulet Achou" + chips "Todas"/"Amazon"/"Me...") visível **exatamente 1x** no topo — achado da rodada 1 confirmado resolvido. Mídia em destaque, preço R$ 149,90/R$ 299,90 riscado, badge `-50%`, CTA "Comprar agora →" proeminente, seção "Mais ofertas" em grid reaproveitando `.deal-card` (incl. card "Indisponível" para produto sem `affiliate_link`). Layout coerente com `ux-ui-spec.md`.
  - **`home.png`**: header 1x, sem duplicação. Grid de cards estilizado (1 coluna mobile), badges `%OFF` vermelhos, preço riscado/atual, CTA vermelho "Ver oferta →", CTA desabilitado cinza "Indisponível", paginação "Página 1 de 1". Sem regressão.
  - **`categoria.png`**: header 1x, sem duplicação, mesmo grid/card da Home, título "Eletronicos". Sem regressão.
  - Estado vazio de categoria (ad-hoc, `/categoria/CategoriaInexistenteXYZ`): `curl | grep -c deals-empty` → 1, `grep -c site-header` → 1 — CA-7 confirmado, sem regressão.
- Ambiente Docker removido ao final (`docker compose down -v`), imagens locais (`omuletachou-api`, `omuletachou-website`) removidas, `.env`/`docker-compose.override.yml` apagados — sem resíduo (`git status --short` limpo, exceto o diretório novo de screenshots desta rodada).

### Revalidação completa dos 15 critérios de aceite (+ 3 transversais)

| Critério | Resultado | Evidência (rodada 2) |
|---|---|---|
| CA-1 — cor de marca consistente nas 3 telas | **PASS** (era FALHA) | `deal-detail.png` agora tem header com chip ativo vermelho; badge/CTA vermelhos nas 3 telas |
| CA-2 — paleta/tipografia alinhadas ao design system | PASS | Work Sans + tokens confirmados no CSS servido, inalterado desde rodada 1 |
| CA-3 — 100% das classes BEM com regra CSS | PASS | Sem mudança de CSS nesta correção; verificação da rodada 1 permanece válida |
| CA-4 — CSS importado/aplicado no build | PASS | `curl http://localhost:3000/` → `<link rel="stylesheet" href="/_next/static/css/...">` presente |
| CA-5 — Home estilizada | PASS | `home.png` |
| CA-6 — Categoria reaproveita estilo da Home | PASS | `categoria.png` |
| CA-7 — Estado vazio de categoria estilizado | PASS | `curl` ad-hoc categoria inexistente: `deals-empty` 1x + header 1x |
| CA-8 — `deal-detail` estilizada | **PASS** (era FALHA) | `deal-detail.png` — header presente 1x + mídia/preço/badge/CTA/relacionados OK |
| CA-9 — sem overflow horizontal (mobile 375px) | PASS | Assert automatizado em `e2e/visual.spec.ts`, 3/3 passed |
| CA-10 — grid responsivo mobile-first | PASS | 1 coluna mobile confirmada nos 3 PNGs; CSS inalterado desde rodada 1 |
| CA-11 — área de toque adequada | PASS | CTAs/chips com dimensões visíveis adequadas nos screenshots |
| CA-12 — sem conflito de cor com manifest PWA | PASS | Única cor de marca `#e63946` e derivações, nas 3 telas incl. `deal-detail` |
| CA-13 — `test:visual` existe e roda sem erro de config | PASS | `npm run test:visual` executou, 3/3 passed |
| CA-14 — cobertura mínima de 3 telas em screenshot | PASS | `home.png`, `categoria.png`, `deal-detail.png` gerados em `screenshots-qa-r2/` |
| CA-15 — Gate Visual do QA deixa de resolver N/A | PASS | Gate executou de verdade nas duas rodadas — funcionando conforme esperado |
| CA-T1 — build sem erros de TS | PASS | Build Docker: "✓ Compiled successfully", type check OK |
| CA-T2 — nenhuma mudança de dados/rotas/API | PASS | `git show fec0bb6 --stat`: apenas `page.tsx` (+3) e `page.test.tsx` (+11) |
| CA-T3 — nenhuma config de deploy alterada | PASS | Nenhum arquivo de produção alterado; `.env`/override locais, não commitados |

**Resultado: 18/18 critérios PASS (15/15 CA-1..CA-15 + 3/3 transversais T1-T3). QA APROVADO.**

**Encaminhamento**: Líder Técnico abre PR `homolog` → `main` para o Gate 2 do Gerente.

`repo_path` deixado checked out em `desenv` ao final.

## Ledger de Custo

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 25202 | 14 | 101 |
| 2 | PM Fase 1 (levantamento, Gate 1) | PM | Sonnet | 27274 | 8 | 65 |
| 3 | PM Fase 2 (PRD + critérios de aceite) | PM | Sonnet | 50220 | 19 | 197 |
| 4 | Refinamento Técnico (especificacao-tecnica.md + sub-issue #156) | Líder Técnico | Sonnet | 85826 | 39 | 430 |
| 5 | UX/UI (spec visual, tokens Figma) | UX/UI | Sonnet | 89850 | 9 | 330 |
| 6 | Dev (CSS + test:visual, sub-issue #156, PR #157) | Dev Node.js | Sonnet | 150219 | 112 | 962 |
| 7 | Merge PR #157 + PR homologação #158 + issue técnica #159 | Líder Técnico | Sonnet | 53241 | 18 | 141 |
| 8 | Code Review — validação PR #158 (build/boot/testes/visual, merge desenv→homolog) | Code Review | Sonnet | 93406 | 56 | 546 |
| 9 | QA (homolog) — reprovado, header ausente em deal-detail | QA | Sonnet | 132122 | 87 | 783 |

--- Correção pós-QA (2026-08-14) — header ausente em /oferta/[slug] ---

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 10 | LT — mapeamento da falha, reabertura #156 | Líder Técnico | Sonnet | 64083 | 15 | 190 |
| 11 | Dev — fix Header ausente em deal-detail, PR #160 | Dev Node.js | Sonnet | 82317 | 58 | 473 |
| 12 | LT — merge PR #160 + novo PR homologação #161 | Líder Técnico | Sonnet | 59026 | 11 | 170 |
| 13 | Code Review — validação final PR #161 (build/boot/testes/visual, merge desenv→homolog) | Code Review | Sonnet | 106308 | 52 | 492 |
| 14 | QA (homolog, rodada 2, pós-fix) — aprovado, 18/18 critérios | QA | Sonnet | 111515 | 39 | 483 |
| 15 | LT — PR release homolog->main (#162) | Líder Técnico | Sonnet | 46048 | 8 | 56 |
| 16 | Coordenador — Gate 2 (merge + consolidação custo) | Coordenador | Haiku | 6000 | 20 | 50 |

---

**Totais acumulados:**
- Tokens: 1.184.452
- Tools: 565
- Tempo processamento: 5.878s (97.97 min ≈ 98 min = 1h38min)
- Tempo decorrido (wall-clock): 8h52min (createdAt 13:38:17Z → closedAt 22:30:00Z)

_Mantido pelo Coordenador. Última atualização: 2026-08-14._
