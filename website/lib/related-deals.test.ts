import { getRelatedDeals } from './related-deals';
import { fetchDeals } from './api';
import type { Deal, PagedResult } from './types';

jest.mock('./api', () => ({
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
    affiliateLink: 'https://amazon.com/xyz?tag=abc',
    mediaUrl: 'https://cdn.example.com/xyz.jpg',
    mediaLocalPath: null,
    slug: 'fone-bluetooth-xyz',
    category: 'eletronicos',
    collectedAt: '2026-07-01T12:00:00Z',
    ...overrides,
  };
}

function pagedResult(items: Deal[]): PagedResult<Deal> {
  return { items, page: 1, pageSize: items.length, totalItems: items.length, totalPages: 1 };
}

describe('lib/related-deals', () => {
  beforeEach(() => {
    fetchDealsMock.mockReset();
  });

  it('CA-B4: busca até 4 relacionados da mesma categoria, excluindo o produto atual', async () => {
    fetchDealsMock.mockResolvedValueOnce(
      pagedResult([
        buildDeal({ slug: 'fone-bluetooth-xyz' }),
        buildDeal({ slug: 'related-1' }),
        buildDeal({ slug: 'related-2' }),
        buildDeal({ slug: 'related-3' }),
        buildDeal({ slug: 'related-4' }),
      ])
    );

    const related = await getRelatedDeals(buildDeal());

    expect(related).toHaveLength(4);
    expect(related.every((item) => item.slug !== 'fone-bluetooth-xyz')).toBe(true);
    expect(fetchDealsMock).toHaveBeenCalledWith(1, 5, { category: 'eletronicos' });
  });

  it('retorna lista vazia quando não há relacionados suficientes', async () => {
    fetchDealsMock.mockResolvedValueOnce(pagedResult([buildDeal()]));

    const related = await getRelatedDeals(buildDeal());

    expect(related).toEqual([]);
  });
});
