import path from 'path';
import { test, expect } from '@playwright/test';
import { getRealCategoriaAndSlug } from './helpers';

// Mesmo diretório usado pelo playwright.config.ts (outputDir) — mantém screenshots e
// artefatos de falha juntos, redirecionáveis via SCREENSHOTS_DIR (ver validação do PR).
const SCREENSHOTS_DIR = process.env.SCREENSHOTS_DIR ?? './screenshots';

test.describe('Visual — Site público (CA-13/CA-14, mobile-first)', () => {
  test('Home exibe grid de ofertas estilizado', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // CA-9: sem overflow horizontal em viewport mobile (375px).
    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth
    );
    expect(hasHorizontalOverflow).toBe(false);

    await expect(page.locator('.site-header')).toBeVisible();

    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, 'home.png'), fullPage: true });
  });

  test('Categoria exibe grid ou estado vazio estilizado', async ({ page, baseURL }) => {
    const { categoria } = await getRealCategoriaAndSlug(baseURL!);
    const targetCategoria = categoria ?? 'categoria-teste-e2e-vazia';

    await page.goto(`/categoria/${targetCategoria}`);
    await page.waitForLoadState('networkidle');

    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth
    );
    expect(hasHorizontalOverflow).toBe(false);

    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'categoria.png'),
      fullPage: true,
    });
  });

  test('Detalhe de oferta exibe mídia, preço e CTA estilizados', async ({ page, baseURL }) => {
    const { slug } = await getRealCategoriaAndSlug(baseURL!);
    test.skip(!slug, 'Nenhuma oferta ativa no catálogo — não há /oferta/{slug} navegável.');

    await page.goto(`/oferta/${slug}`);
    await page.waitForLoadState('networkidle');

    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth
    );
    expect(hasHorizontalOverflow).toBe(false);

    await expect(page.locator('.deal-detail')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'deal-detail.png'),
      fullPage: true,
    });
  });
});
