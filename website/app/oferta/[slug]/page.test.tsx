import { render, screen } from '@testing-library/react';
import OfertaPage, { generateMetadata } from './page';
import { fetchDeal, fetchByCategory } from '@/lib/api';
import { notFound } from 'next/navigation';
import type { Deal, PagedResult } from '@/lib/types';

jest.mock('@/lib/api', () => ({
  fetchDeal: jest.fn(),
  fetchByCategory: jest.fn(),
}));

jest.mock('next/navigation', () => ({
  notFound: jest.fn(() => {
    throw new Error('NEXT_NOT_FOUND');
  }),
}));

const fetchDealMock = fetchDeal as jest.MockedFunction<typeof fetchDeal>;
const fetchByCategoryMock = fetchByCategory as jest.MockedFunction<typeof fetchByCategory>;
const notFoundMock = notFound as jest.MockedFunction<typeof notFound>;

function buildDeal(overrides: Partial<Deal> = {}): Deal {
  return {
    title: 'Fone Bluetooth XYZ',
    salePrice: 99.9,
    originalPrice: 149.9,
    discountPct: 33,
    affiliateLink: 'https://amazon.com/xyz?tag=abc',
    mediaUrl: 'https://cdn.example.com/xyz.jpg',
    mediaLocalPath: null,
    slug: 'fone-bluetooth-xyz',
    category: 'eletronicos',
    collectedAt: '2026-07-01T12:00:00Z',
    platform: 'Amazon',
    ...overrides,
  };
}

function pagedResult(items: Deal[]): PagedResult<Deal> {
  return { items, page: 1, pageSize: items.length, totalItems: items.length, totalPages: 1 };
}

describe('OfertaPage', () => {
  beforeEach(() => {
    fetchDealMock.mockReset();
    fetchByCategoryMock.mockReset();
    notFoundMock.mockClear();
  });

  it('CA-B2: renderiza o conteúdo do produto (HTML já com conteúdo, sem depender de JS)', async () => {
    fetchDealMock.mockResolvedValueOnce(buildDeal());
    fetchByCategoryMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await OfertaPage({ params: { slug: 'fone-bluetooth-xyz' } });
    render(jsx);

    expect(screen.getByRole('heading', { level: 1, name: 'Fone Bluetooth XYZ' })).toBeInTheDocument();
  });

  it('CA-B3: slug inexistente chama notFound() do Next.js', async () => {
    fetchDealMock.mockResolvedValueOnce(null);

    await expect(OfertaPage({ params: { slug: 'slug-inexistente' } })).rejects.toThrow(
      'NEXT_NOT_FOUND'
    );
    expect(notFoundMock).toHaveBeenCalled();
  });

  it('CA-B9: injeta script JSON-LD do tipo Product', async () => {
    fetchDealMock.mockResolvedValueOnce(buildDeal());
    fetchByCategoryMock.mockResolvedValueOnce(pagedResult([]));

    const jsx = await OfertaPage({ params: { slug: 'fone-bluetooth-xyz' } });
    const { container } = render(jsx);

    const script = container.querySelector('script[type="application/ld+json"]');
    expect(script).not.toBeNull();
    const jsonLd = JSON.parse(script?.innerHTML ?? '{}');
    expect(jsonLd['@type']).toBe('Product');
    expect(jsonLd.offers['@type']).toBe('Offer');
  });

  describe('generateMetadata', () => {
    it('CA-B5: title/description dinâmicos por produto', async () => {
      fetchDealMock.mockResolvedValueOnce(buildDeal());

      const metadata = await generateMetadata({ params: { slug: 'fone-bluetooth-xyz' } });

      expect(metadata.title).toBe('Fone Bluetooth XYZ | O Mulet Achou');
      expect(metadata.description).toContain('Fone Bluetooth XYZ');
    });

    it('CA-B6: Open Graph completo (title, description, image, url)', async () => {
      fetchDealMock.mockResolvedValueOnce(buildDeal());

      const metadata = await generateMetadata({ params: { slug: 'fone-bluetooth-xyz' } });

      expect(metadata.openGraph?.title).toBe('Fone Bluetooth XYZ');
      expect(metadata.openGraph?.description).toBeTruthy();
      expect(metadata.openGraph?.url).toBe('https://omuletachou.com.br/oferta/fone-bluetooth-xyz');
      expect(JSON.stringify(metadata.openGraph?.images)).toContain('https://cdn.example.com/xyz.jpg');
      expect(metadata.alternates?.canonical).toBe('https://omuletachou.com.br/oferta/fone-bluetooth-xyz');
    });

    it('CA-B8: fallback de og:image quando produto não tem mídia', async () => {
      fetchDealMock.mockResolvedValueOnce(buildDeal({ mediaUrl: null, mediaLocalPath: null }));

      const metadata = await generateMetadata({ params: { slug: 'fone-bluetooth-xyz' } });

      expect(JSON.stringify(metadata.openGraph?.images)).toContain('/og-default.png');
    });

    it('slug inexistente: metadata de fallback sem crash', async () => {
      fetchDealMock.mockResolvedValueOnce(null);

      const metadata = await generateMetadata({ params: { slug: 'slug-inexistente' } });

      expect(metadata.title).toContain('não encontrada');
    });
  });
});
