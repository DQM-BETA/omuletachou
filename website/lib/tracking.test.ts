import { trackProductClick } from './tracking';

const ORIGINAL_ENV = process.env.NEXT_PUBLIC_API_URL;

describe('lib/tracking', () => {
  beforeEach(() => {
    process.env.NEXT_PUBLIC_API_URL = 'http://localhost:5000';
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_API_URL = ORIGINAL_ENV;
    jest.restoreAllMocks();
    // @ts-expect-error - limpar mock de sendBeacon entre testes
    delete (navigator as unknown as { sendBeacon?: unknown }).sendBeacon;
  });

  describe('quando sendBeacon está disponível', () => {
    it('chama navigator.sendBeacon com a URL correta do endpoint de clique', () => {
      const sendBeaconSpy = jest.fn().mockReturnValue(true);
      Object.defineProperty(navigator, 'sendBeacon', {
        value: sendBeaconSpy,
        configurable: true,
      });

      trackProductClick('11111111-1111-1111-1111-111111111111');

      expect(sendBeaconSpy).toHaveBeenCalledWith(
        expect.stringContaining(
          '/api/public/products/11111111-1111-1111-1111-111111111111/click'
        )
      );
    });

    it('não chama fetch quando sendBeacon é usado', () => {
      Object.defineProperty(navigator, 'sendBeacon', {
        value: jest.fn().mockReturnValue(true),
        configurable: true,
      });
      const fetchMock = jest.fn();
      global.fetch = fetchMock as unknown as typeof fetch;

      trackProductClick('produto-id');

      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('fallback (sem sendBeacon)', () => {
    it('usa fetch com keepalive quando navigator.sendBeacon não existe', () => {
      const fetchMock = jest.fn().mockResolvedValue({});
      global.fetch = fetchMock as unknown as typeof fetch;

      trackProductClick('produto-id');

      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining('/api/public/products/produto-id/click'),
        expect.objectContaining({ method: 'POST', keepalive: true })
      );
    });

    it('CA 2.4: falha/timeout no fetch fallback é silenciada (catch), não propaga erro', async () => {
      const fetchMock = jest.fn().mockRejectedValue(new Error('network error'));
      global.fetch = fetchMock as unknown as typeof fetch;

      expect(() => trackProductClick('produto-id')).not.toThrow();

      // Aguarda a microtask da Promise rejeitada resolver o catch silencioso.
      await Promise.resolve();
      await Promise.resolve();
    });
  });
});
