import { render, screen } from '@testing-library/react';
import CategoriaPage, { generateMetadata } from './page';
import { fetchByCategory } from '@/lib/api';
import type { Deal, PagedResult } from '@/lib/types';

jest.mock('@/lib/api', () => ({
  fetchByCategory: jest.fn(),
}));

const fetchByCategoryMock = fetchByCategory as jest.MockedFunction<typeof fetchByCategory>;

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

describe('CategoriaPage', () => {
  beforeEach(() => {
    fetchByCategoryMock.mockReset();
  });

  it('CA-C2: renderiza a grade de ofertas da categoria via fetchByCategory (HTML já com conteúdo)', async () => {
    fetchByCategoryMock.mockResolvedValueOnce(
      pagedResult([buildDeal(), buildDeal({ slug: 'outro-produto', title: 'Outro produto' })])
    );

    const jsx = await CategoriaPage({ params: { categoria: 'eletronicos' }, searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-grid')).toBeInTheDocument();
    expect(screen.getAllByTestId('deal-card')).toHaveLength(2);
    expect(fetchByCategoryMock).toHaveBeenCalledWith('eletronicos', 1, 12);
  });

  it('CA-C2 (paginação): navega para a próxima página mantendo a categoria', async () => {
    fetchByCategoryMock.mockResolvedValueOnce(pagedResult([buildDeal()], { page: 1, totalPages: 3 }));

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
    fetchByCategoryMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await CategoriaPage({ params: { categoria: 'brinquedos' }, searchParams: {} });
    render(jsx);

    expect(screen.getByTestId('deals-empty')).toBeInTheDocument();
    expect(screen.getByText(/nenhuma oferta encontrada nesta categoria/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ver todas as ofertas/i })).toHaveAttribute('href', '/');
  });

  it('CA-T2: propaga erro de fetchByCategory (não engole) para o Next.js tratar via cache/ISR', async () => {
    fetchByCategoryMock.mockRejectedValueOnce(new Error('API indisponível'));

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
