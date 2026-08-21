import { render, screen } from '@testing-library/react';
import CategoriaPage, { generateMetadata } from './page';
import { fetchDeals } from '@/lib/api';
import type { Deal, PagedResult } from '@/lib/types';

jest.mock('@/lib/api', () => ({
  fetchDeals: jest.fn(),
}));

const fetchDealsMock = fetchDeals as jest.MockedFunction<typeof fetchDeals>;

function buildDeal(overrides: Partial<Deal> = {}): Deal {
  return {
    id: '11111111-1111-1111-1111-111111111111',
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

describe('CategoriaPage', () => {
  beforeEach(() => {
    fetchDealsMock.mockReset();
  });

  it('CA-C2: renderiza a grade de ofertas da categoria via fetchDeals (HTML já com conteúdo)', async () => {
    fetchDealsMock.mockResolvedValueOnce(
      pagedResult([buildDeal(), buildDeal({ slug: 'outro-produto', title: 'Outro produto' })])
    );

    const jsx = await CategoriaPage({ params: { categoria: 'eletronicos' }, searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-grid')).toBeInTheDocument();
    expect(screen.getAllByTestId('deal-card')).toHaveLength(2);
    expect(fetchDealsMock).toHaveBeenCalledWith(1, 12, { category: 'eletronicos' });
  });

  it('CA-C2 (paginação): navega para a próxima página mantendo a categoria', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal()], { page: 1, totalPages: 3 }));

    const jsx = await CategoriaPage({
      params: { categoria: 'eletronicos' },
      searchParams: { page: '1' },
    });
    render(jsx);

    const nextLink = screen.getByRole('link', { name: /próxima/i });
    expect(nextLink).toHaveAttribute('href', '/categoria/eletronicos?page=2');
    expect(screen.queryByRole('link', { name: /anterior/i })).not.toBeInTheDocument();
  });

  it('CA-C4: categoria sem ofertas exibe estado vazio, sem notFound()', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await CategoriaPage({ params: { categoria: 'brinquedos' }, searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-empty')).toBeInTheDocument();
    expect(screen.getByText(/nenhuma oferta encontrada nesta categoria/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ver todas as ofertas/i })).toHaveAttribute('href', '/');
  });

  it('CA-T2: propaga erro de fetchDeals (não engole) para o Next.js tratar via cache/ISR', async () => {
    fetchDealsMock.mockRejectedValueOnce(new Error('API indisponível'));

    await expect(
      CategoriaPage({ params: { categoria: 'eletronicos' }, searchParams: {} })
    ).rejects.toThrow('API indisponível');
  });

  describe('generateMetadata', () => {
    it('CA-C3: título segue o padrão "{Categoria} | O Mulet Achou"', async () => {
      const metadata = await generateMetadata({ params: { categoria: 'eletronicos' } });

      expect(metadata.title).toBe('Eletronicos | O Mulet Achou');
    });

    it('formata categorias com hífen/underscore como palavras capitalizadas', async () => {
      const metadata = await generateMetadata({ params: { categoria: 'casa-e-decoracao' } });

      expect(metadata.title).toBe('Casa E Decoracao | O Mulet Achou');
    });
  });
});
