import path from 'path';
import { test, expect } from '@playwright/test';
import { blockApiCalls, injectDummyAuth } from './helpers';

// Mesmo diretório usado pelo playwright.config.ts (outputDir) — mantém screenshots e
// artefatos de falha juntos, redirecionáveis via SCREENSHOTS_DIR (ver validação do PR).
const SCREENSHOTS_DIR = process.env.SCREENSHOTS_DIR ?? './screenshots';

test.describe('Visual — Dashboard (Issue #155/#232, Gate Visual do QA)', () => {
  test('Login exibe o formulário estilizado (rota pública, sem auth)', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('.login-card')).toBeVisible();
    await expect(page.getByTestId('email-input')).toBeVisible();
    await expect(page.getByTestId('password-input')).toBeVisible();
    await expect(page.getByTestId('login-submit')).toBeVisible();

    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'login.png'), fullPage: true });
  });

  test.describe('Rotas autenticadas (authGuard + token dummy, sem depender da API real)', () => {
    test.beforeEach(async ({ page }) => {
      await injectDummyAuth(page);
      await blockApiCalls(page);
    });

    test('Products (rota padrão pós-login) exibe shell + tabela estilizados', async ({ page }) => {
      await page.goto('/products');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();
      await expect(page.getByTestId('products-table')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'products.png'), fullPage: true });
    });

    test('Queue exibe shell + tabela de publicações estilizados', async ({ page }) => {
      await page.goto('/queue');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'queue.png'), fullPage: true });
    });

    test('Facebook Manual exibe shell estilizado', async ({ page }) => {
      await page.goto('/facebook-manual');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'facebook-manual.png'), fullPage: true });
    });

    test('MercadoLivre Links exibe shell estilizado', async ({ page }) => {
      await page.goto('/mercadolivre-links');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'mercadolivre-links.png'), fullPage: true });
    });

    test('Settings exibe shell estilizado', async ({ page }) => {
      await page.goto('/settings');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'settings.png'), fullPage: true });
    });

    test('Jobs exibe shell estilizado', async ({ page }) => {
      await page.goto('/jobs');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'jobs.png'), fullPage: true });
    });

    test('Reports exibe shell estilizado', async ({ page }) => {
      await page.goto('/reports');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('.shell-sidenav')).toBeVisible();

      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'reports.png'), fullPage: true });
    });
  });
});
