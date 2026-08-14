# Tasks — ISSUE-154: CSS do site público + `test:visual`

> Sub-issue única (stack: nodejs). Dev lê apenas este arquivo para critérios de aceite + contexto técnico.

## T-01 — Implementar CSS do site público + configurar `test:visual` (Playwright)

### Contexto técnico
- Repo: `omuletachou`, projeto `website/` (Next.js 14+, App Router)
- Docs: `documentacoes/ISSUE-154-site-sem-css/especificacao-tecnica.md` (abordagem técnica completa: organização de CSS, tokens, inventário de classes, config Playwright)
- Spec visual (design tokens/valores, layout detalhado): produzida pelo UX/UI a partir do Figma (https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library) — path definido pelo UX/UI, referenciado em `documentacoes/ISSUE-154-site-sem-css/`
- Openspec: `openspec/changes/issue-154-site-sem-css/proposal.md` (requisitos) e `design.md` (visão geral)
- Referência de setup Playwright (padrão a adaptar): `repos/dqm-digital-app/playwright.config.ts` e `repos/dqm-digital-app/tests/e2e/`

### O que fazer
1. Escrever CSS global cobrindo 100% das classes BEM listadas em `especificacao-tecnica.md` §1.3 (`site-header__*`, `deal-card__*`, `deal-detail__*`, `deals-grid`, `deals-empty`, `deals-pagination`), usando os tokens/valores da spec visual do UX/UI.
2. Corrigir `app/layout.tsx`: importar `./globals.css` (bug raiz).
3. Remover `app/page.module.css` (órfão, boilerplate não usado).
4. Implementar responsividade mobile-first (base mobile, breakpoints `min-width` progressivos) — sem overflow horizontal em 375px, grid de cards adaptável, área de toque adequada em CTAs/filtros/paginação.
5. Garantir consistência da cor de marca `#e63946` (CTA, badge, header) sem introduzir cor de marca concorrente — alinhado ao manifest PWA (Issue #117).
6. Instalar `@playwright/test` (devDependency) + `npx playwright install chromium`.
7. Criar `website/playwright.config.ts` (STAGING_URL first, webServer local como fallback) conforme especificação técnica §4.2.
8. Criar `website/e2e/helpers.ts` (`getRealCategoriaAndSlug` via `/sitemap.xml`) e `website/e2e/visual.spec.ts` (3 telas: Home, categoria, detalhe de oferta) conforme §4.3/4.4.
9. Adicionar script `test:visual` em `website/package.json` e entradas de `.gitignore` (`/screenshots`, `/playwright-report`, `/test-results`).
10. Rodar `npm run build`, `npm test` (cobertura ≥ 80%, sem regressão) e `npm run test:visual` localmente antes do PR.

### Critérios de aceite (Given/When/Then)
Todos os critérios em `documentacoes/ISSUE-154-site-sem-css/criterios-aceite.md` (CA-1 a CA-15, CA-T1 a CA-T3) aplicam-se a esta sub-issue. Destaques objetivos (verificáveis sem julgamento estético):
- **CA-3/CA-4**: 100% das classes BEM referenciadas no JSX têm regra CSS correspondente; `globals.css` efetivamente importado e aplicado no HTML renderizado.
- **CA-9/CA-10/CA-11**: sem overflow horizontal em 375px; grid responsivo mobile-first; áreas de toque adequadas.
- **CA-12**: nenhuma cor de marca primária diferente de `#e63946`/tons derivados.
- **CA-13/CA-14**: `npm run test:visual` existe, roda via Playwright, captura screenshot das 3 telas (Home, categoria, detalhe de oferta).
- **CA-T1/CA-T2/CA-T3**: `npm run build` sem erros; nenhuma mudança em `lib/api.ts`/rotas/fetch; nenhuma mudança em `docker-compose.yml`/variáveis de produção.

### Fora de escopo
- Alterar estrutura de componentes, nomes de classes BEM, lógica de fetch/dados ou rotas.
- Configurar `test:visual` em `dashboard` (Angular) — Issue #155, rota `backlog`, separada.
- Qualquer configuração de deploy/produção (`docker-compose.yml`, domínio, variáveis).
