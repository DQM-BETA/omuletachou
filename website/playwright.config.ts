import { defineConfig, devices } from '@playwright/test';

const STAGING_URL = process.env.STAGING_URL;
const LOCAL_URL = 'http://localhost:3000';
const BASE_URL = STAGING_URL ?? LOCAL_URL;

// Redireciona os artefatos (screenshots/relatório) para fora da raiz do repo quando
// SCREENSHOTS_DIR é definido (ex. {docs_path}/screenshots na validação do Dev) — default
// mantém tudo em ./screenshots (já coberto por .gitignore).
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
    viewport: { width: 375, height: 812 }, // mobile-first (CA-14 pede viewport mobile)
  },
  projects: [
    { name: 'mobile-chromium', use: { ...devices['Pixel 7'] } },
  ],
  webServer: STAGING_URL
    ? undefined
    : {
        command: 'npm run dev',
        url: LOCAL_URL,
        reuseExistingServer: true,
        timeout: 60000,
      },
});
