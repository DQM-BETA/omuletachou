---
issue: 154
titulo: "bug: Site público (website) sem nenhum estilo CSS implementado — apenas HTML puro"
etapa_atual: Refinamento Técnico — concluído, aguardando UX/UI
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
sub_issues: ["#156 (stack:nodejs, task_id:T-01)"]
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
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
5. UX/UI: spec visual a partir do design system do Figma — **próximo**
6. Dev: implementação (CSS + `test:visual`) na sub-issue #156
7. Code Review + QA: validação visual (novo checkpoint — Gate Visual passa a funcionar de fato)
8. Gate 2 (Gerente) → merge main

## Ledger de Custo

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparação | Coordenador | Haiku | — | — | — |
| 2 | PM Fase 1 + Fase 2 | PM | Sonnet | (preencher pelo orquestrador via `<usage>`) | — | — |
| 3 | Refinamento Técnico | Líder Técnico | Sonnet | (preencher pelo orquestrador via `<usage>`) | — | — |

---

_Mantido pelo Líder Técnico. Última atualização: 2026-08-14._
