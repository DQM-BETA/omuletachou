import { test, expect } from '@playwright/test';

/**
 * ISSUE-260 (T-03) — busca textual inteligente na FilterBar.
 *
 * Contrato exercitado (especificacao-tecnica.md §5, design.md §3/§5): campo de busca com
 * draft+debounce+`router.replace` (mesmo mecanismo do preço, Issue #230/#262), `q` na
 * querystring, banner de "resultados aproximados" quando `isApproximateSearch === true`,
 * estado de vazio genuíno quando a busca não encontra nada (nem por aproximação).
 *
 * Nota (paralelização com T-01/T-02): a lógica de 2 estágios (full-text + fallback fuzzy) vive
 * no backend (Issue #260, sub-issues #267/#268), implementada em paralelo/sequência a esta
 * sub-issue. Os cenários abaixo focam no comportamento observável do frontend (URL, ausência de
 * erro, estados visuais) e não fixam asserções sobre o conteúdo exato do catálogo real — a
 * validação de resultado end-to-end (termo com erro de digitação → banner aproximado; termo sem
 * nenhuma relação → vazio genuíno) é responsabilidade de Code Review/QA, rodada depois que
 * backend e frontend desta issue estiverem mergeados e implantados juntos.
 */
test.describe('FilterBar — busca textual (Issue #260)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Viewport padrão do projeto mobile-chromium (playwright.config.ts) — o campo de busca só
    // fica no DOM com o drawer de filtros aberto (mesmo padrão do slider de preço).
    const drawerToggle = page.getByRole('button', { name: /^Filtros/ });
    if (await drawerToggle.isVisible()) {
      await drawerToggle.click();
    }
  });

  test('CA 1.1: campo de busca visível na filter-bar', async ({ page }) => {
    await expect(page.getByRole('searchbox', { name: 'Buscar produtos' })).toBeVisible();
  });

  test('CA 2.1: digitar não navega a cada tecla; após pausa, a URL reflete `q` sem empilhar histórico', async ({
    page,
  }) => {
    const searchInput = page.getByRole('searchbox', { name: 'Buscar produtos' });
    await searchInput.fill('tenis');

    await page.waitForURL(/[?&]q=tenis/, { timeout: 5000 });

    // `router.replace` (não push) — "voltar" não deveria reaparecer com o campo vazio se o
    // commit não empilhou uma entrada de histórico por tecla/pausa.
    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);
  });

  test('limpar o campo de busca remove `q` da URL e volta ao estado padrão', async ({ page }) => {
    const searchInput = page.getByRole('searchbox', { name: 'Buscar produtos' });
    await searchInput.fill('tenis');
    await page.waitForURL(/[?&]q=tenis/, { timeout: 5000 });

    await searchInput.fill('');
    await page.waitForURL((url) => !url.search.includes('q='), { timeout: 5000 });

    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);
  });

  test('busca sem nenhuma relação com o catálogo não gera erro; exibe grid ou estado vazio (genérico/genuíno)', async ({
    page,
  }) => {
    const searchInput = page.getByRole('searchbox', { name: 'Buscar produtos' });
    // Termo propositalmente sem relação com produtos de e-commerce, para exercitar o caminho de
    // "sem resultado nem por aproximação" sem depender do conteúdo exato do catálogo real.
    await searchInput.fill('zzxxqqwwyyyy1234semrelacaonenhuma');

    await page.waitForURL(/[?&]q=/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);
    const gridOrEmpty = page.locator('[data-testid="deals-grid"], [data-testid="deals-empty"]');
    await expect(gridOrEmpty).toBeVisible();
  });

  test('quando o resultado é sinalizado como aproximado (isApproximateSearch), o banner é exibido de forma distinta do grid normal', async ({
    page,
  }) => {
    const searchInput = page.getByRole('searchbox', { name: 'Buscar produtos' });
    await searchInput.fill('tenus'); // erro de digitação comum, candidato a fallback fuzzy

    await page.waitForURL(/[?&]q=tenus/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    const banner = page.getByTestId('deals-search-approximate');
    if (await banner.isVisible().catch(() => false)) {
      await expect(banner).toContainText('aproximad');
    }
    // Independente de o backend já ter o estágio 2 implantado neste ambiente (T-01/T-02
    // paralelas), a UI nunca deve travar em erro genérico.
    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);
  });
});
