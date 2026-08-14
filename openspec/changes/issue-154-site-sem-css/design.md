# Design — ISSUE-154: CSS do site público + validação visual (resumido)

> Sem ambiguidade arquitetural (avaliação do PM, Fase 2) — design resumido, sem decisões de arquitetura de sistema.

## Visão geral da solução
`website/` (Next.js 14+, App Router) renderiza HTML sem estilo porque `app/globals.css` nunca foi customizado (boilerplate) e sequer é importado em `app/layout.tsx`. A solução é puramente de camada de apresentação:
1. Implementar CSS global (sem CSS Modules, sem dependência nova) cobrindo as classes BEM já existentes nos componentes/páginas.
2. Corrigir o import ausente em `app/layout.tsx`.
3. Configurar Playwright (`test:visual`) para validar visualmente as 3 telas, destravando o Gate Visual do QA (hoje sempre `N/A` para este projeto).

## Componentes envolvidos
- `app/globals.css` (+ partials opcionais em `app/styles/`)
- `app/layout.tsx` (fix do import)
- `app/page.tsx`, `app/categoria/[categoria]/page.tsx`, `app/oferta/[slug]/page.tsx` (consumidores das classes, sem alteração estrutural)
- `components/Header.tsx`, `components/DealCard.tsx`, `components/DealDetail.tsx` (idem — só consumidores)
- Novo: `website/playwright.config.ts`, `website/e2e/visual.spec.ts`, `website/e2e/helpers.ts`

## Stack
Sem mudança de stack: Next.js 14+, CSS puro/global (nativo), Playwright (novo devDependency, mesmo padrão já usado em `dqm-digital-app`).

## Fluxo direto
Build/deploy já existentes (Docker, Issues #12/#94/#95/#96/#117) não mudam. O único fluxo novo é o de teste: `npm run test:visual` sobe o Next.js (local) ou aponta para `STAGING_URL` (CI/homolog) e tira screenshot das 3 telas.

Detalhes técnicos completos (organização de arquivos CSS, tokens, inventário de classes, config do Playwright) em `especificacao-tecnica.md`.
