---
issue: 154
titulo: "bug: Site público (website) sem nenhum estilo CSS implementado — apenas HTML puro"
etapa_atual: "PR #158 desenv→homolog aberto — aguardando /code-review (sessão principal) + Code Review"
ultimo_agente: lider-tecnico
rota: normal
openspec_change: repos/omuletachou/openspec/changes/issue-154-site-sem-css
tech_stacks: [nodejs]
repos:
  omuletachou: main
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-154-site-sem-css
openspec_path: repos/omuletachou/openspec/changes/issue-154-site-sem-css
status_comment_id: "5293952020"
sub_issues: ["#156 (stack:nodejs, task_id:T-01) — merged via PR #157"]
desenv_tasks_merged: ["#156"]
pr_feature: "#157 (fix/156-css-website -> desenv, squash merged)"
sub_issues_frontend: {}
pr_homologacao: "#158 (desenv -> homolog)"
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
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
8. Code Review + QA: validação visual (novo checkpoint — Gate Visual passa a funcionar de fato) — **próximo**
9. Gate 2 (Gerente) → merge main

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

---

_Mantido pela sessão principal. Última atualização: 2026-08-14._
