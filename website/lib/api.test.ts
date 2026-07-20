import { fetchDeals, fetchDeal, fetchByCategory } from './api';
import type { Deal, PagedResult } from './types';

const mockDeal: Deal = {
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
};

function pagedResult(items: Deal[]): PagedResult<Deal> {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalItems: items.length,
    totalPages: 1,
  };
}

describe('lib/api', () => {
  const originalFetch = global.fetch;
  let fetchMock: jest.Mock;
  let consoleErrorSpy: jest.SpyInstance;

  beforeEach(() => {
    fetchMock = jest.fn();
    global.fetch = fetchMock as unknown as typeof fetch;
    consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    global.fetch = originalFetch;
    consoleErrorSpy.mockRestore();
    jest.clearAllMocks();
  });

  describe('fetchDeals', () => {
    it('CA-A1: retorna a lista paginada de ofertas chamando o endpoint público', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => pagedResult([mockDeal]),
      });

      const result = await fetchDeals(1, 12);

      expect(result.items).toHaveLength(1);
      expect(result.items[0].slug).toBe('fone-bluetooth-xyz');
      expect(fetchMock).toHaveBeenCalledTimes(1);
      const [calledUrl, calledOptions] = fetchMock.mock.calls[0];
      expect(calledUrl).toContain('/api/public/deals?');
      expect(calledUrl).toContain('page=1');
      expect(calledUrl).toContain('pageSize=12');
      expect(calledOptions).toEqual({ next: { revalidate: 300 } });
    });

    it('nunca expõe a URL interna da API ao chamar (usa API_INTERNAL_URL, não NEXT_PUBLIC_*)', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => pagedResult([]),
      });

      await fetchDeals();

      const [calledUrl] = fetchMock.mock.calls[0];
      // A URL usada server-side não deve derivar de NEXT_PUBLIC_API_URL.
      expect(calledUrl.startsWith('http://api:8080')).toBe(true);
    });

    it('aplica filtro de category quando informado', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => pagedResult([mockDeal]),
      });

      await fetchDeals(2, 24, 'eletronicos');

      const [calledUrl] = fetchMock.mock.calls[0];
      expect(calledUrl).toContain('page=2');
      expect(calledUrl).toContain('pageSize=24');
      expect(calledUrl).toContain('category=eletronicos');
    });

    it('CA-T2: propaga erro 5xx (não engole) para o Next.js servir cache antigo', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        json: async () => ({}),
      });

      await expect(fetchDeals()).rejects.toThrow(/500/);
      expect(consoleErrorSpy).toHaveBeenCalled();
    });

    it('propaga falha de rede (fetch rejeitado)', async () => {
      fetchMock.mockRejectedValueOnce(new Error('network error'));

      await expect(fetchDeals()).rejects.toThrow('network error');
    });
  });

  describe('fetchDeal', () => {
    it('CA-B1: retorna os dados completos da oferta pelo slug', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockDeal,
      });

      const result = await fetchDeal('fone-bluetooth-xyz');

      expect(result).toEqual(mockDeal);
      const [calledUrl] = fetchMock.mock.calls[0];
      expect(calledUrl).toContain('/api/public/deals/fone-bluetooth-xyz');
    });

    it('CA-B3: mapeia 404 para null (não lança exceção)', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        json: async () => ({}),
      });

      const result = await fetchDeal('slug-inexistente');

      expect(result).toBeNull();
      expect(consoleErrorSpy).not.toHaveBeenCalled();
    });

    it('diferencia erro de rede/5xx de um 404 real — propaga o erro', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: false,
        status: 503,
        statusText: 'Service Unavailable',
        json: async () => ({}),
      });

      await expect(fetchDeal('qualquer-slug')).rejects.toThrow(/503/);
    });
  });

  describe('fetchByCategory', () => {
    it('CA-C1: retorna as ofertas da categoria, paginadas', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => pagedResult([mockDeal]),
      });

      const result = await fetchByCategory('eletronicos', 1, 12);

      expect(result.items).toHaveLength(1);
      const [calledUrl] = fetchMock.mock.calls[0];
      expect(calledUrl).toContain('/api/public/deals/category/eletronicos?');
    });
  });
});
