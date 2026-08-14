import { render, screen } from '@testing-library/react';
import Home from './page';
import { fetchDeals, fetchCategories } from '@/lib/api';
import type { CategoryTree, Deal, PagedResult } from '@/lib/types';

jest.mock('@/lib/api', () => ({
  fetchDeals: jest.fn(),
  fetchCategories: jest.fn(),
}));

jest.mock('@/components/FilterBar', () => {
  return function MockFilterBar() {
    return <div data-testid="filter-bar-mock" />;
  };
});

const fetchDealsMock = fetchDeals as jest.MockedFunction<typeof fetchDeals>;
const fetchCategoriesMock = fetchCategories as jest.MockedFunction<typeof fetchCategories>;

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
    fetchCategoriesMock.mockReset();
    fetchCategoriesMock.mockResolvedValue([] as CategoryTree[]);
  });

  it('CA-A2: renderiza a grade de ofertas retornada por fetchDeals (HTML já com conteúdo)', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal(), buildDeal({ slug: 'outro-produto', title: 'Outro produto' })]));

    const jsx = await Home({ searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-grid')).toBeInTheDocument();
    expect(screen.getAllByTestId('deal-card')).toHaveLength(2);
  });

  it('renderiza o FilterBar acima do grid', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal()]));

    const jsx = await Home({ searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('filter-bar-mock')).toBeInTheDocument();
  });

  it('CA 7.1/7.2/7.3: repassa os filtros da URL (category/subcategory/preço/desconto/sort) para fetchDeals', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal()]));

    await Home({
      searchParams: {
        category: 'Eletrônicos',
        subcategory: 'Celulares',
        minPrice: '100',
        maxPrice: '500',
        minDiscount: '30',
        sort: 'price_asc',
      },
    });

    expect(fetchDealsMock).toHaveBeenCalledWith(1, 12, {
      category: 'Eletrônicos',
      subcategory: 'Celulares',
      minPrice: 100,
      maxPrice: 500,
      minDiscount: 30,
      sort: 'price_asc',
    });
  });

  it('CA-A6: pagina mantendo os filtros ativos na querystring', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal()], { page: 1, totalPages: 3 }));

    const jsx = await Home({ searchParams: { page: '1', category: 'Eletrônicos' } });
    render(jsx);

    const nextLink = screen.getByRole('link', { name: /próxima/i });
    expect(nextLink).toHaveAttribute('href', '?category=Eletr%C3%B4nicos&page=2');
    expect(screen.queryByRole('link', { name: /anterior/i })).not.toBeInTheDocument();
  });

  it('exibe estado vazio genérico quando não há ofertas e nenhum filtro ativo', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await Home({ searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-empty')).toBeInTheDocument();
    expect(screen.getByText('Nenhuma oferta encontrada.')).toBeInTheDocument();
  });

  it('CA 7.5: exibe estado vazio orientado a filtro, com CTA "Ver todas as ofertas", quando há filtro ativo sem resultado', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await Home({ searchParams: { minDiscount: '90' } });
    render(jsx);

    expect(screen.getByTestId('deals-empty')).toBeInTheDocument();
    expect(screen.getByText(/nenhuma oferta encontrada com esses filtros/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ver todas as ofertas/i })).toHaveAttribute('href', '/');
  });

  it('CA-T2: propaga erro de fetchDeals (não engole) para o Next.js tratar via cache/ISR', async () => {
    fetchDealsMock.mockRejectedValueOnce(new Error('API indisponível'));

    await expect(Home({ searchParams: {} })).rejects.toThrow('API indisponível');
  });
});
