# Dashboard

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 17.3.17.

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

Run `ng e2e` to execute the end-to-end tests via a platform of your choice. To use this command, you need to first add a package that implements end-to-end testing capabilities.

## Running visual tests (Playwright — `test:visual`)

Alimenta o Gate Visual obrigatório do QA (`.claude/agents/qa.md`, regra d2). Mesmo padrão de
`website/` (Issue #154/#156), adaptado para Angular + rotas autenticadas (Issue #155/#232).

- `npm run test:visual` — roda contra `http://localhost:4200`, subindo o dev server
  (`npm start`) automaticamente se ainda não estiver no ar (`webServer` do
  `playwright.config.ts`, `reuseExistingServer: true`).
- `STAGING_URL=<url> npm run test:visual` — roda contra um ambiente já publicado (staging/
  homolog), sem subir servidor local.
- `SCREENSHOTS_DIR=<path> npm run test:visual` — redireciona screenshots e o relatório para
  fora da raiz do repo (ex.: `documentacoes/ISSUE-NNN-titulo/screenshots` na validação do
  Dev/QA). Default: `./screenshots` (coberto por `.gitignore`, nunca commitado).
- Specs em `e2e/visual.spec.ts`, cobrindo `/login` e as rotas autenticadas (`/products`,
  `/queue`, `/facebook-manual`, `/mercadolivre-links`, `/settings`, `/jobs`, `/reports`).
- **Rotas autenticadas sem depender da API real:** `authGuard`
  (`src/app/core/auth/auth.guard.ts`) só verifica se há um token em `sessionStorage`, então os
  specs injetam um token dummy (`e2e/helpers.ts` → `injectDummyAuth`) para passar pelo guard.
  Como esse token não é um JWT válido, se a API .NET estiver de fato no ar localmente ela
  responde 401 de verdade — e `authInterceptor` (`src/app/core/auth/auth.interceptor.ts`)
  trata qualquer 401 fora de `/api/auth/login` como sessão expirada (logout + redirect para
  `/login`), o que quebraria o screenshot. Por isso os specs também bloqueiam as chamadas de
  rede a `/api/**` (`e2e/helpers.ts` → `blockApiCalls`), tornando o teste determinístico
  independente de a API estar rodando. As telas renderizam o layout/CSS real com o estado de
  erro tratado pelos próprios componentes (spinner some, snackbar/mensagem de erro) —
  suficiente para o objetivo do Gate Visual (layout/CSS, não dado).

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.
