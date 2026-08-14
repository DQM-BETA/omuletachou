---
issue: 154
titulo: "bug: Site público (website) sem nenhum estilo CSS implementado — apenas HTML puro"
etapa_atual: "Correção pós-QA — sub-issue #156 reaberta (fix: <Header /> ausente em /oferta/[slug], CA-1/CA-8) — aguardando Dev"
ultimo_agente: lt
rota: normal
openspec_change: repos/omuletachou/openspec/changes/issue-154-site-sem-css
tech_stacks: [nodejs]
repos:
  omuletachou: main
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-154-site-sem-css
openspec_path: repos/omuletachou/openspec/changes/issue-154-site-sem-css
status_comment_id: "5293952020"
sub_issues: ["#156 (stack:nodejs, task_id:T-01) — reaberta: fix header ausente em deal-detail (QA reprovou PR #158)"]
desenv_tasks_merged: []
pr_feature: "#157 (fix/156-css-website -> desenv, squash merged) — histórico; nova PR será aberta pelo Dev para o fix"
sub_issues_frontend: {}
pr_homologacao: "#158 (desenv -> homolog, merge commit 6e65564d8e4172c5d437af2bb99e00245ee26424)"
pr_release: ~
code_review_homolog_pr: 158
qa_status: reprovado
figma_url: https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library
blockers: nenhum
createdAt: "2026-08-14T00:00:00Z"
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
10. Dev: fix pontual (`<Header />` em `app/oferta/[slug]/page.tsx`) — **pendente**
11. Líder Técnico: novo ciclo merge → Code Review → QA → Gate 2 (Gerente) → merge main

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

## QA — homolog

**Status: REPROVADO** (relatório completo em `relatorio-qa.md`).

Branch sincronizada (`git fetch && git checkout homolog && git pull origin homolog`), commit `6e65564d8e4172c5d437af2bb99e00245ee26424` (PR #158) confirmado em `git log`.

Nota de sincronismo: o `estado.md` encontrado em `homolog` no momento da checagem ainda dizia "aguardando /code-review" (desatualizado) — o `estado.md` de `desenv` já tinha a seção "Code Review — PR #158" (aprovado, merge feito). Validação prosseguiu normalmente pois o código/commit já estava confirmado em `homolog` (`git log`); a defasagem era só de bookkeeping do `estado.md` entre branches, não do código.

- `npm test` (website/): **79/79 passando**, sem regressão.
- `npm run build` (via docker build): sem erros de TypeScript — CA-T1 OK.
- **Validação integrada (d3)**: stack Docker real subida a partir de `homolog` (`db`+`api`+`website`), `.env`/`docker-compose.override.yml` locais descartáveis (portas 8080/3000 expostas para teste), catálogo vazio seedado via SQL direto (5 produtos, casos com/sem desconto, com/sem `affiliate_link`, categorias distintas). Confirmado via `curl` que o HTML carrega com `<link rel="stylesheet">` e o CSS compilado contém os tokens da marca (`--color-primary:#e63946`).
- **Gate Visual obrigatório (d2) — primeira execução real para `website`**: `STAGING_URL=http://localhost:3000 SCREENSHOTS_DIR=documentacoes/ISSUE-154-site-sem-css/screenshots-qa npm run test:visual` → **3/3 passed**. Screenshots inspecionados visualmente (Home, Categoria, `deal-detail` + 1 ad-hoc de categoria vazia para CA-7).
  - Home e Categoria: header (`site-header`) visível exatamente 1x, sem duplicação, layout condizente com `ux-ui-spec.md`. **OK.**
  - **`deal-detail`: header COMPLETAMENTE AUSENTE (0x)** — confirmado por inspeção visual do PNG e por `curl .../oferta/{slug} | grep -c site-header` → 0 ocorrências. Causa raiz: `website/app/oferta/[slug]/page.tsx` nunca importou/renderizou `<Header />` (diferente de `app/page.tsx` e `app/categoria/[categoria]/page.tsx`) — bug **pré-existente desde a Issue #95** (`git log` confirma o arquivo não foi tocado pelo PR #157/#158 desta issue), só agora capturado porque é a primeira vez que o Gate Visual do QA de fato dispara (CA-15). O Code Review anterior também não pegou esse achado (sua inspeção visual não cobriu ausência total de elemento).
  - Resto da tela `deal-detail` (mídia, preço, badge, CTA, "Mais ofertas") está corretamente estilizado.
- **Critérios reprovados: CA-1 e CA-8** (mesma causa raiz — header ausente em `deal-detail`). Demais 13 CAs + 3 transversais (T1-T3): PASS. Ver tabela completa em `relatorio-qa.md`.
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

## Ledger de Custo

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 25202 | 14 | 101s |
| 2 | PM Fase 1 (levantamento, Gate 1) | PM | Sonnet | 27274 | 8 | 65s |
| 3 | PM Fase 2 (PRD + critérios de aceite) | PM | Sonnet | 50220 | 19 | 197s |
| 4 | Refinamento Técnico (especificacao-tecnica.md + sub-issue #156) | Líder Técnico | Sonnet | 85826 | 39 | 430s |
| 5 | UX/UI (spec visual, tokens Figma) | UX/UI | Sonnet | 89850 | 9 | 330s |
| 6 | Dev (CSS + test:visual, sub-issue #156, PR #157) | Dev Node.js | Sonnet | 150219 | 112 | 962s |
| 7 | Merge PR #157 + PR homologação #158 + issue técnica #159 | Líder Técnico | Sonnet | 53241 | 18 | 141s |
| 8 | Code Review — validação PR #158 (build/boot/testes/visual, merge desenv→homolog) | Code Review | Sonnet | 93406 | 56 | 546s |
| 9 | QA (homolog) — reprovado, header ausente em deal-detail | QA | Sonnet | 132122 | 87 | 783s |

--- Correção pós-QA (2026-08-14) — header ausente em /oferta/[slug] ---

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 10 | LT — mapeamento da falha, reabertura #156 | Líder Técnico | Sonnet | 64083 | 15 | 190s |
| 11 | Dev — fix Header ausente em deal-detail, PR #160 | Dev Node.js | Sonnet | 82317 | 58 | 473s |

---

_Mantido pela sessão principal. Última atualização: 2026-08-14._
