import { render } from '@testing-library/react';
import PushSubscriptionManager from './PushSubscriptionManager';
import * as push from '@/lib/push';

jest.mock('@/lib/push', () => ({
  isPushSupported: jest.fn(),
  subscribeToPush: jest.fn(),
}));

const mockedIsPushSupported = push.isPushSupported as jest.Mock;
const mockedSubscribeToPush = push.subscribeToPush as jest.Mock;

describe('PushSubscriptionManager', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  it('não renderiza nenhum elemento visível', () => {
    mockedIsPushSupported.mockReturnValue(false);
    const { container } = render(<PushSubscriptionManager />);
    expect(container).toBeEmptyDOMElement();
  });

  it('não chama subscribeToPush quando o browser não suporta push (fallback gracioso)', () => {
    mockedIsPushSupported.mockReturnValue(false);

    render(<PushSubscriptionManager />);

    expect(mockedSubscribeToPush).not.toHaveBeenCalled();
  });

  it('chama subscribeToPush quando o browser suporta push', () => {
    mockedIsPushSupported.mockReturnValue(true);
    mockedSubscribeToPush.mockResolvedValue(null);

    render(<PushSubscriptionManager />);

    expect(mockedSubscribeToPush).toHaveBeenCalledTimes(1);
  });
});
