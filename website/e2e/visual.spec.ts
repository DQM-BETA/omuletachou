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

test.describe('Visual — FilterBar (Issue #167 / Sub-D #171)', () => {
  test('Mobile (<1024px): resumo compacto + abrir o drawer de filtros', async ({ page }) => {
    // Viewport já é mobile por padrão (playwright.config.ts, projeto mobile-chromium).
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const summary = page.locator('.filter-bar__summary');
    await expect(summary).toBeVisible();
    await expect(page.locator('.filter-bar__row')).toHaveCount(0);

    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'filter-bar-mobile-summary.png'),
      fullPage: true,
    });

    await page.getByRole('button', { name: /^Filtros/ }).click();
    const drawerPanel = page.locator('.filter-bar__drawer-panel');
    await expect(drawerPanel).toBeVisible();

    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth
    );
    expect(hasHorizontalOverflow).toBe(false);

    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'filter-bar-mobile-drawer.png'),
      fullPage: true,
    });
  });

  test('Desktop (>=1024px): os 5 controles em linha única, sem drawer', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const row = page.locator('.filter-bar__row');
    await expect(row).toBeVisible();
    await expect(page.locator('.filter-bar__summary')).toHaveCount(0);
    await expect(page.getByRole('combobox', { name: 'Categoria', exact: true })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Subcategoria' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Ordenar por' })).toBeVisible();
    // Filtro de desconto mínimo removido (Issue #230/#261) — o 3º controle agora é o slider de
    // preço (Issue #230/#262), não mais os botões "10%+/30%+/50%+".
    await expect(page.getByRole('slider', { name: 'Preço mínimo' })).toBeVisible();
    await expect(page.getByRole('slider', { name: 'Preço máximo' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Limpar filtros' })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'filter-bar-desktop.png'),
      fullPage: true,
    });
  });
});
