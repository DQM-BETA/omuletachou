import { defineConfig, devices } from '@playwright/test';

const STAGING_URL = process.env.STAGING_URL;
const LOCAL_URL = 'http://localhost:4200';
const BASE_URL = STAGING_URL ?? LOCAL_URL;

// Redireciona os artefatos (screenshots/relatório) para fora da raiz do repo quando
// SCREENSHOTS_DIR é definido (ex. {docs_path}/screenshots na validação do Dev) — default
// mantém tudo em ./screenshots (já coberto por .gitignore). Mesmo padrão de website/playwright.config.ts.
const SCREENSHOTS_DIR = process.env.SCREENSHOTS_DIR ?? './screenshots';

export default defineConfig({
  testDir: './e2e',
  outputDir: SCREENSHOTS_DIR,
  timeout: 60000,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: {
    baseURL: BASE_URL,
    screenshot: 'only-on-failure',
    // Dashboard é ferramenta interna/admin, sem requisito mobile-first documentado
    // (ao contrário do website) — viewport desktop padrão.
    viewport: { width: 1280, height: 720 },
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: STAGING_URL
    ? undefined
    : {
        command: 'npm start',
        url: LOCAL_URL,
        reuseExistingServer: true,
        timeout: 60000,
      },
});
