import { test, expect } from '@playwright/test';
import { getRealCategoriaAndSlug } from './helpers';

/**
 * ISSUE-231 (sub-issue #280, T-05) — faixa/carrossel de produtos sugeridos na listagem pública.
 *
 * Como o catálogo real vem de scraping (sem seed fixo), estes cenários não fixam asserções
 * sobre quais produtos específicos aparecem na faixa nem sobre o número exato de itens — o
 * corte mínimo de 4 e o critério de fallback são resolvidos pelo backend (design.md §6). Foco
 * no comportamento observável do frontend: a faixa aparece (ou some sem erro) tanto no caso de
 * filtro com resultado quanto no de fallback, e clicar num card do carrossel não quebra a
 * navegação — mesmo contrato de `DealCardLink` já validado em #279/T-04.
 */
test.describe('Faixa de produtos sugeridos (Issue #231, T-05)', () => {
  test('categoria com resultado: se a faixa aparece, mostra título "Em alta em {Categoria}" e não quebra a página', async ({
    page,
    baseURL,
  }) => {
    const { categoria } = await getRealCategoriaAndSlug(baseURL ?? 'http://localhost:3000');
    test.skip(!categoria, 'Catálogo real sem categoria disponível para exercitar o cenário.');

    await page.goto(`/?category=${encodeURIComponent(categoria!)}`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);

    const carousel = page.getByTestId('suggested-carousel');
    if (await carousel.isVisible().catch(() => false)) {
      await expect(carousel.getByRole('heading', { level: 2 })).toContainText('Em alta em');
    }
  });

  test('filtro sem resultado (fallback): a faixa, se aparecer, mostra "Em alta na loja" sem quebrar o grid vazio', async ({
    page,
  }) => {
    // Faixa de preço propositalmente inatingível — força `hasResults=false` na listagem
    // principal, cenário de fallback (CA 1.2), sem depender do conteúdo exato do catálogo.
    await page.goto('/?minPrice=999999');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);
    await expect(page.getByTestId('deals-empty')).toBeVisible();

    const carousel = page.getByTestId('suggested-carousel');
    if (await carousel.isVisible().catch(() => false)) {
      await expect(carousel.getByRole('heading', { level: 2 })).toHaveText('Em alta na loja');
    }
  });

  test('clique em um card do carrossel abre o destino do link de afiliado sem erro na página de origem', async ({
    page,
    context,
  }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const carousel = page.getByTestId('suggested-carousel');
    test.skip(
      !(await carousel.isVisible().catch(() => false)),
      'Catálogo real sem produtos suficientes para exibir a faixa (corte mínimo de 4).'
    );

    const firstCta = carousel.getByRole('link', { name: /ver oferta/i }).first();
    const [popup] = await Promise.all([
      context.waitForEvent('page'),
      firstCta.click(),
    ]);
    await popup.waitForLoadState('domcontentloaded').catch(() => {
      // Destino é um site de afiliado externo — não precisa carregar por completo para o
      // teste confirmar que a navegação abriu em nova aba sem travar a página de origem.
    });

    expect(popup).toBeTruthy();
    await expect(page.locator('[data-testid="app-error"]')).toHaveCount(0);

    await popup.close();
  });
});
