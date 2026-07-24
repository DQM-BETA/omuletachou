import {
  isPushSupported,
  subscribeToPush,
  unsubscribeFromPush,
  urlBase64ToUint8Array,
} from './push';

const ORIGINAL_ENV = process.env.NEXT_PUBLIC_API_URL;

function mockSecureContext(secure: boolean) {
  Object.defineProperty(window, 'isSecureContext', {
    value: secure,
    configurable: true,
  });
}

describe('lib/push', () => {
  beforeEach(() => {
    process.env.NEXT_PUBLIC_API_URL = 'http://localhost:5000';
    mockSecureContext(true);
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_API_URL = ORIGINAL_ENV;
    jest.restoreAllMocks();
    // @ts-expect-error - limpar mocks de navigator entre testes
    delete (navigator as unknown as { serviceWorker?: unknown }).serviceWorker;
    // @ts-expect-error - limpar mocks de window entre testes
    delete (window as unknown as { PushManager?: unknown }).PushManager;
  });

  describe('urlBase64ToUint8Array', () => {
    it('converte uma string base64url em Uint8Array', () => {
      const result = urlBase64ToUint8Array('AAECAw');
      expect(result).toBeInstanceOf(Uint8Array);
      expect(Array.from(result)).toEqual([0, 1, 2, 3]);
    });
  });

  describe('isPushSupported / fallbacks', () => {
    it('retorna false quando serviceWorker não existe em navigator', () => {
      expect(isPushSupported()).toBe(false);
    });

    it('retorna false quando PushManager não existe em window', () => {
      Object.defineProperty(navigator, 'serviceWorker', {
        value: {},
        configurable: true,
      });
      expect(isPushSupported()).toBe(false);
    });

    it('retorna false fora de contexto seguro (sem HTTPS/localhost)', () => {
      Object.defineProperty(navigator, 'serviceWorker', {
        value: {},
        configurable: true,
      });
      // @ts-expect-error - mock mínimo para o teste
      window.PushManager = {};
      mockSecureContext(false);
      expect(isPushSupported()).toBe(false);
    });

    it('retorna true quando serviceWorker, PushManager e contexto seguro existem', () => {
      Object.defineProperty(navigator, 'serviceWorker', {
        value: {},
        configurable: true,
      });
      // @ts-expect-error - mock mínimo para o teste
      window.PushManager = {};
      mockSecureContext(true);
      expect(isPushSupported()).toBe(true);
    });

    it('subscribeToPush retorna null sem quebrar quando não suportado', async () => {
      const result = await subscribeToPush();
      expect(result).toBeNull();
    });

    it('unsubscribeFromPush não faz nada quando serviceWorker não existe', async () => {
      const fetchMock = jest.fn();
      global.fetch = fetchMock as unknown as typeof fetch;
      await expect(unsubscribeFromPush()).resolves.toBeUndefined();
      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('subscribeToPush — fluxo feliz e permissão negada', () => {
    function setupSupportedBrowser(registration: unknown) {
      Object.defineProperty(navigator, 'serviceWorker', {
        value: {
          ready: Promise.resolve(registration),
        },
        configurable: true,
      });
      // @ts-expect-error - mock mínimo para o teste
      window.PushManager = {};
      mockSecureContext(true);
    }

    it('não chama pushManager.subscribe quando o usuário nega a permissão', async () => {
      const subscribeSpy = jest.fn();
      setupSupportedBrowser({ pushManager: { subscribe: subscribeSpy } });

      Object.defineProperty(window, 'Notification', {
        value: { requestPermission: jest.fn().mockResolvedValue('denied') },
        configurable: true,
      });

      global.fetch = jest.fn().mockResolvedValue({
        json: () => Promise.resolve({ publicKey: 'chave-vapid-valida' }),
      }) as unknown as typeof fetch;

      const result = await subscribeToPush();

      expect(result).toBeNull();
      expect(subscribeSpy).not.toHaveBeenCalled();
    });

    it('não tenta subscribe quando a VAPID public key ainda não foi cadastrada (null)', async () => {
      const subscribeSpy = jest.fn();
      setupSupportedBrowser({ pushManager: { subscribe: subscribeSpy } });

      global.fetch = jest.fn().mockResolvedValue({
        json: () => Promise.resolve({ publicKey: null }),
      }) as unknown as typeof fetch;

      const result = await subscribeToPush();

      expect(result).toBeNull();
      expect(subscribeSpy).not.toHaveBeenCalled();
    });

    it('fluxo feliz: busca chave, pede permissão, assina e envia subscribe', async () => {
      const fakeSubscription = {
        toJSON: () => ({
          endpoint: 'https://push.example.com/xyz',
          keys: { p256dh: 'p256dh-value', auth: 'auth-value' },
        }),
      };
      const subscribeSpy = jest.fn().mockResolvedValue(fakeSubscription);
      setupSupportedBrowser({ pushManager: { subscribe: subscribeSpy } });

      Object.defineProperty(window, 'Notification', {
        value: { requestPermission: jest.fn().mockResolvedValue('granted') },
        configurable: true,
      });

      const fetchMock = jest.fn().mockImplementation((url: string) => {
        if (String(url).includes('vapid-public-key')) {
          return Promise.resolve({
            json: () => Promise.resolve({ publicKey: 'AAECAw' }),
          });
        }
        return Promise.resolve({ json: () => Promise.resolve({ id: 1 }) });
      });
      global.fetch = fetchMock as unknown as typeof fetch;

      const result = await subscribeToPush();

      expect(subscribeSpy).toHaveBeenCalledWith(
        expect.objectContaining({ userVisibleOnly: true })
      );
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining('/api/public/push/subscribe'),
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            endpoint: 'https://push.example.com/xyz',
            keys: { p256dh: 'p256dh-value', auth: 'auth-value' },
          }),
        })
      );
      expect(result).toBe(fakeSubscription);
    });
  });

  describe('unsubscribeFromPush', () => {
    it('remove a subscription local e chama DELETE no backend', async () => {
      const unsubscribeSpy = jest.fn().mockResolvedValue(true);
      const fakeSubscription = {
        endpoint: 'https://push.example.com/xyz',
        unsubscribe: unsubscribeSpy,
      };

      Object.defineProperty(navigator, 'serviceWorker', {
        value: {
          getRegistration: jest.fn().mockResolvedValue({
            pushManager: {
              getSubscription: jest.fn().mockResolvedValue(fakeSubscription),
            },
          }),
        },
        configurable: true,
      });

      const fetchMock = jest.fn().mockResolvedValue({});
      global.fetch = fetchMock as unknown as typeof fetch;

      await unsubscribeFromPush();

      expect(unsubscribeSpy).toHaveBeenCalled();
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining('/api/public/push/unsubscribe?endpoint='),
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('não chama DELETE quando não há subscription ativa', async () => {
      Object.defineProperty(navigator, 'serviceWorker', {
        value: {
          getRegistration: jest.fn().mockResolvedValue({
            pushManager: { getSubscription: jest.fn().mockResolvedValue(null) },
          }),
        },
        configurable: true,
      });

      const fetchMock = jest.fn();
      global.fetch = fetchMock as unknown as typeof fetch;

      await unsubscribeFromPush();

      expect(fetchMock).not.toHaveBeenCalled();
    });
  });
});
