import { render, screen, fireEvent } from '@testing-library/react';
import DealCardLink from './DealCardLink';
import * as tracking from '@/lib/tracking';

jest.mock('@/lib/tracking', () => ({
  trackProductClick: jest.fn(),
}));

describe('DealCardLink', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  it('CA 2.1: renderiza o link com href/target/rel idênticos ao CTA atual', () => {
    render(
      <DealCardLink
        productId="11111111-1111-1111-1111-111111111111"
        href="https://amazon.com/xyz?tag=abc"
        className="deal-card__cta"
      />
    );

    const cta = screen.getByRole('link', { name: /ver oferta/i });
    expect(cta).toHaveAttribute('href', 'https://amazon.com/xyz?tag=abc');
    expect(cta).toHaveAttribute('target', '_blank');
    expect(cta).toHaveAttribute('rel', 'nofollow');
    expect(cta).toHaveClass('deal-card__cta');
  });

  it('CA 2.1/2.2: chama trackProductClick com o id do produto ao clicar, sem impedir a navegação', () => {
    render(
      <DealCardLink
        productId="11111111-1111-1111-1111-111111111111"
        href="https://amazon.com/xyz?tag=abc"
        className="deal-card__cta"
      />
    );

    const cta = screen.getByRole('link', { name: /ver oferta/i });
    fireEvent.click(cta);

    expect(tracking.trackProductClick).toHaveBeenCalledWith(
      '11111111-1111-1111-1111-111111111111'
    );
    // href permanece o mesmo após o clique — nenhuma alteração de destino via JS.
    expect(cta).toHaveAttribute('href', 'https://amazon.com/xyz?tag=abc');
  });
});
