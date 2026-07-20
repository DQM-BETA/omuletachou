import { render, screen } from '@testing-library/react';
import Home from './page';
import { fetchDeals } from '@/lib/api';
import type { Deal, PagedResult } from '@/lib/types';

jest.mock('@/lib/api', () => ({
  fetchDeals: jest.fn(),
}));

const fetchDealsMock = fetchDeals as jest.MockedFunction<typeof fetchDeals>;

function buildDeal(overrides: Partial<Deal> = {}): Deal {
  return {
    title: 'Fone Bluetooth XYZ',
    salePrice: 99.9,
    originalPrice: 149.9,
    discountPct: 33,
    affiliateLink: 'https://amazon.com/xyz',
    mediaUrl: 'https://cdn.example.com/xyz.jpg',
    mediaLocalPath: null,
    slug: 'fone-bluetooth-xyz',
    category: 'eletronicos',
    collectedAt: '2026-07-01T12:00:00Z',
    platform: 'Amazon',
    ...overrides,
  };
}

function pagedResult(items: Deal[], overrides: Partial<PagedResult<Deal>> = {}): PagedResult<Deal> {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalItems: items.length,
    totalPages: 1,
    ...overrides,
  };
}

describe('Home page', () => {
  beforeEach(() => {
    fetchDealsMock.mockReset();
  });

  it('CA-A2: renderiza a grade de ofertas retornada por fetchDeals (HTML já com conteúdo)', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal(), buildDeal({ slug: 'outro-produto', title: 'Outro produto' })]));

    const jsx = await Home({ searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-grid')).toBeInTheDocument();
    expect(screen.getAllByTestId('deal-card')).toHaveLength(2);
  });

  it('CA-A6: pagina para a próxima página mantendo o filtro de plataforma', async () => {
    fetchDealsMock.mockResolvedValueOnce(
      pagedResult([buildDeal()], { page: 1, totalPages: 3 })
    );

    const jsx = await Home({ searchParams: { page: '1', platform: 'Amazon' } });
    render(jsx);

    const nextLink = screen.getByRole('link', { name: /próxima/i });
    expect(nextLink).toHaveAttribute('href', '/?page=2&platform=Amazon');
    expect(screen.queryByRole('link', { name: /anterior/i })).not.toBeInTheDocument();
  });

  it('CA-A5: filtra a grade por plataforma selecionada', async () => {
    fetchDealsMock.mockResolvedValueOnce(
      pagedResult([
        buildDeal({ slug: 'a', platform: 'Amazon' }),
        buildDeal({ slug: 'b', platform: 'Shopee' }),
      ])
    );

    const jsx = await Home({ searchParams: { platform: 'Amazon' } });
    render(jsx);

    expect(screen.getAllByTestId('deal-card')).toHaveLength(1);
  });

  it('exibe estado vazio quando não há ofertas', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await Home({ searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-empty')).toBeInTheDocument();
  });

  it('CA-T2: propaga erro de fetchDeals (não engole) para o Next.js tratar via cache/ISR', async () => {
    fetchDealsMock.mockRejectedValueOnce(new Error('API indisponível'));

    await expect(Home({ searchParams: {} })).rejects.toThrow('API indisponível');
  });
});
