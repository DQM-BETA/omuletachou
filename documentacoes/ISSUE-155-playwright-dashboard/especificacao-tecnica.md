# Especificação técnica — Issue #155: Playwright (`test:visual`) no `dashboard`

## Objetivo
Configurar Playwright no projeto `dashboard/` (Angular 17) para que o Gate Visual
obrigatório do QA (`.claude/agents/qa.md`, regra d2) passe a rodar de verdade — hoje
resolve `N/A` porque o script `test:visual` não existe em `dashboard/package.json`.

## Padrão de referência
Mesmo repo (`omuletachou`), já implementado e aprovado (Issue #154/#156) para `website/`
(Next.js). Reaproveitar a mesma estrutura, adaptando para Angular + rotas autenticadas:
- `website/playwright.config.ts`
- `website/e2e/visual.spec.ts` + `website/e2e/helpers.ts`
- `website/.gitignore` (`/screenshots`, `/playwright-report`)

Projeto irmão `dqm-digital-app` (Expo/RN, `repos/dqm-digital-app/playwright.config.ts` e
`tests/e2e/screenshots.spec.ts`) segue o mesmo padrão geral (config + `webServer` +
screenshots por rota) — usar só como segunda referência, a stack é diferente.

## Escopo
1. **Dependência:** `npm install -D @playwright/test@^1.62.1` (mesma major usada em
   `website/`, consistência dentro do monorepo) em `dashboard/`.
2. **Script:** adicionar `"test:visual": "playwright test"` em `dashboard/package.json`
   (`scripts`).
3. **Config** `dashboard/playwright.config.ts` (baseado em `website/playwright.config.ts`):
   - `testDir: './e2e'`
   - `outputDir`: usar `SCREENSHOTS_DIR` env var (default `./screenshots`) — mesmo padrão
     do `website`, permite redirecionar artefatos fora da raiz do repo na validação do Dev.
   - `BASE_URL`: `STAGING_URL ?? 'http://localhost:4200'` (porta padrão do `ng serve`).
   - `reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]]`
   - `use.screenshot: 'only-on-failure'`
   - `projects`: `chromium` (Desktop Chrome) — o dashboard é ferramenta interna/admin,
     sem requisito mobile-first documentado (ao contrário do `website`, que tem CA-13/CA-14
     mobile-first); usar viewport desktop padrão (`devices['Desktop Chrome']`, ~1280×720).
   - `webServer` (só quando `STAGING_URL` não definido): `command: 'npm start'`,
     `url: LOCAL_URL`, `reuseExistingServer: true`, `timeout: 60000`. **Não** subir a API
     .NET automaticamente aqui (diferente do `dqm-digital-app`) — ver nota de autenticação
     abaixo; se algum teste precisar da API real, documentar isso no próprio spec do teste.
4. **`.gitignore`:** adicionar `/screenshots` e `/playwright-report` em `dashboard/.gitignore`
   (arquivo ainda não existe — criar).
5. **Testes** `dashboard/e2e/visual.spec.ts` — screenshot por rota principal
   (`dashboard/src/app/app.routes.ts`):
   - `/login` (pública, `loginGuard`) — sempre acessível sem autenticação.
   - `/products` (rota padrão pós-login, `redirectTo` de `''`).
   - Demais rotas autenticadas (`/queue`, `/facebook-manual`, `/mercadolivre-links`,
     `/settings`, `/jobs`, `/reports`) são desejáveis mas **não bloqueantes** para o
     critério mínimo — pelo menos `/login` e `/products` cobertos é o piso aceitável do
     `test:visual` funcionar; o Dev pode estender às demais se o tempo permitir.

### Nota — rotas autenticadas sem subir a API
Todas as rotas exceto `/login` são protegidas por `authGuard`
(`dashboard/src/app/core/auth/auth.guard.ts`), que só verifica
`AuthService.isAuthenticated()` = `!!token` (não valida o JWT contra o backend de forma
síncrona no guard). O token fica em `sessionStorage` sob a chave `omuletachou_token`
(`dashboard/src/app/core/auth/auth.service.ts`).
Padrão recomendado para não depender da API/Postgres subindo no ambiente de teste
(diferente do `dqm-digital-app`, que sobe a API .NET no `webServer`): usar
`page.addInitScript` (ou navegar para `/login` uma vez e usar
`context.storageState`) para injetar um token dummy em `sessionStorage` antes de
navegar para a rota protegida. O guard libera a navegação; chamadas de API que a tela
fizer podem falhar silenciosamente (404/401) — aceitável para o objetivo de screenshot
de layout/CSS (mesmo racional do Gate Visual: pegar "classe existe mas não foi
estilizada", não validar dado). Se o Dev preferir login real via UI (chamando a API),
documentar o pré-requisito (API + Postgres rodando) no `dashboard/CLAUDE.md`.

## Critérios de aceite (Given/When/Then)
- **CA-1:** Dado `dashboard/package.json`, quando inspecionado, então existe o script
  `"test:visual": "playwright test"`.
- **CA-2:** Dado o comando `npm run test:visual` executado em `dashboard/` com o dev
  server local disponível, quando concluído, então roda sem erros de configuração e
  gera pelo menos os screenshots de `/login` e `/products` em `screenshots/`.
- **CA-3:** Dado `dashboard/playwright.config.ts`, quando comparado a
  `website/playwright.config.ts`, então segue o mesmo padrão (`SCREENSHOTS_DIR`,
  `STAGING_URL`, reporter list+html, `screenshot: 'only-on-failure'`).
- **CA-4:** Dado `dashboard/.gitignore`, então ignora `/screenshots` e
  `/playwright-report`.
- **CA-5:** Dado `dashboard/CLAUDE.md` (se existir) ou o `CLAUDE.md` do repo, quando a
  seção de testes é lida, então documenta como rodar `test:visual` no dashboard
  (dev server local + comando).

## Contexto técnico (paths)
- `repo_path`: `repos/omuletachou`
- `docs_path`: `repos/omuletachou/documentacoes/ISSUE-155-playwright-dashboard/`
- Projeto alvo: `dashboard/` (raiz do projeto Angular dentro do repo)
- Referência de config: `website/playwright.config.ts`, `website/e2e/visual.spec.ts`,
  `website/e2e/helpers.ts`, `website/.gitignore`
- Rotas: `dashboard/src/app/app.routes.ts`
- Auth: `dashboard/src/app/core/auth/auth.guard.ts`,
  `dashboard/src/app/core/auth/auth.service.ts`
- Stack: Angular 17.3, `@playwright/test` (instalar `^1.62.1`)
