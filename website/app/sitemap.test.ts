import sitemap from './sitemap';
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
    pageSize: 100,
    totalItems: items.length,
    totalPages: 1,
    ...overrides,
  };
}

describe('app/sitemap', () => {
  beforeEach(() => {
    fetchDealsMock.mockReset();
  });

  it('CA-C5: lista Home, categorias distintas e cada oferta ativa com lastModified', async () => {
    fetchDealsMock.mockResolvedValueOnce(
      pagedResult([
        buildDeal({ slug: 'a', category: 'eletronicos', collectedAt: '2026-07-01T12:00:00Z' }),
        buildDeal({ slug: 'b', category: 'casa', collectedAt: '2026-07-02T12:00:00Z' }),
      ])
    );

    const entries = await sitemap();

    const urls = entries.map((entry) => entry.url);
    expect(urls).toContain('https://omuletachou.com.br');
    expect(urls).toContain('https://omuletachou.com.br/categoria/eletronicos');
    expect(urls).toContain('https://omuletachou.com.br/categoria/casa');
    expect(urls).toContain('https://omuletachou.com.br/oferta/a');
    expect(urls).toContain('https://omuletachou.com.br/oferta/b');

    const dealEntry = entries.find((entry) => entry.url === 'https://omuletachou.com.br/oferta/a');
    expect(dealEntry?.lastModified).toEqual(new Date('2026-07-01T12:00:00Z'));
  });

  it('pagina fetchDeals até esgotar totalPages para cobrir todas as ofertas', async () => {
    fetchDealsMock
      .mockResolvedValueOnce(pagedResult([buildDeal({ slug: 'p1' })], { page: 1, totalPages: 2 }))
      .mockResolvedValueOnce(pagedResult([buildDeal({ slug: 'p2' })], { page: 2, totalPages: 2 }));

    const entries = await sitemap();

    expect(fetchDealsMock).toHaveBeenCalledTimes(2);
    const urls = entries.map((entry) => entry.url);
    expect(urls).toContain('https://omuletachou.com.br/oferta/p1');
    expect(urls).toContain('https://omuletachou.com.br/oferta/p2');
  });

  it('não duplica categorias repetidas entre ofertas', async () => {
    fetchDealsMock.mockResolvedValueOnce(
      pagedResult([
        buildDeal({ slug: 'a', category: 'eletronicos' }),
        buildDeal({ slug: 'b', category: 'eletronicos' }),
      ])
    );

    const entries = await sitemap();

    const categoriaEntries = entries.filter((entry) =>
      entry.url.startsWith('https://omuletachou.com.br/categoria/')
    );
    expect(categoriaEntries).toHaveLength(1);
  });
});
