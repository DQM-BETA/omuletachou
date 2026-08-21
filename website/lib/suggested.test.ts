import { fetchSuggestedProducts } from './suggested';
import type { Deal } from './types';

const ORIGINAL_ENV = process.env.NEXT_PUBLIC_API_URL;

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

describe('lib/suggested', () => {
  afterEach(() => {
    process.env.NEXT_PUBLIC_API_URL = ORIGINAL_ENV;
    jest.restoreAllMocks();
  });

  // NEXT_PUBLIC_API_URL é lido em `const API_URL` no topo do módulo (mesmo padrão de
  // lib/tracking.ts/lib/push.ts) — capturado uma única vez na importação, então setar
  // `process.env` em `beforeEach` não alteraria o valor já fechado no módulo (mesma limitação
  // já presente em lib/push.test.ts). As asserções abaixo checam o path/querystring via
  // `stringContaining`, sem fixar o host, seguindo o padrão já usado em lib/push.test.ts.

  it('CA 1.1: envia a categoria ativa e hasResults=true, retorna a lista', async () => {
    const deals = [buildDeal()];
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(deals),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    const result = await fetchSuggestedProducts('Eletrônicos', true);

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/api/public/products/suggested?hasResults=true&categories=Eletr%C3%B4nicos'
      )
    );
    expect(result).toEqual(deals);
  });

  it('CA 1.2: categoria ausente — não envia `categories`, só hasResults', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve([]),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    await fetchSuggestedProducts(undefined, true);

    const calledUrl = fetchMock.mock.calls[0][0] as string;
    expect(calledUrl).toContain('/api/public/products/suggested?hasResults=true');
    expect(calledUrl).not.toContain('categories=');
  });

  it('CA 1.2: hasResults=false (filtro sem resultado) é repassado como está, sem decidir fallback aqui', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve([]),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    await fetchSuggestedProducts('Eletrônicos', false);

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/api/public/products/suggested?hasResults=false&categories=Eletr%C3%B4nicos'
      )
    );
  });

  it('CA 1.8: resposta não-OK lança erro (tratado pelo chamador)', async () => {
    const fetchMock = jest.fn().mockResolvedValue({ ok: false, status: 500 });
    global.fetch = fetchMock as unknown as typeof fetch;

    await expect(fetchSuggestedProducts('Eletrônicos', true)).rejects.toThrow('HTTP 500');
  });
});
