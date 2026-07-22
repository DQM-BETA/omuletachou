import { render, screen } from '@testing-library/react';
import DealCard from './DealCard';
import type { Deal } from '@/lib/types';

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

describe('DealCard', () => {
  it('CA-A3: exibe imagem, título, preço riscado, preço atual, badge de desconto e CTA', () => {
    render(<DealCard deal={buildDeal()} />);

    expect(screen.getByAltText('Fone Bluetooth XYZ')).toBeInTheDocument();
    expect(screen.getByText('Fone Bluetooth XYZ')).toBeInTheDocument();
    expect(screen.getByText('R$ 99,90')).toBeInTheDocument();
    expect(screen.getByText('R$ 149,90')).toBeInTheDocument();
    expect(screen.getByTestId('discount-badge')).toHaveTextContent('-33%');
    expect(screen.getByRole('link', { name: /ver oferta/i })).toBeInTheDocument();
  });

  it('CA-A4: CTA usa target=_blank e rel=nofollow apontando para o link de afiliado', () => {
    render(<DealCard deal={buildDeal()} />);

    const cta = screen.getByRole('link', { name: /ver oferta/i });
    expect(cta).toHaveAttribute('target', '_blank');
    expect(cta).toHaveAttribute('rel', 'nofollow');
    expect(cta).toHaveAttribute('href', 'https://amazon.com/xyz?tag=abc');
  });

  it('CA-A7: sem mediaUrl/mediaLocalPath, usa placeholder sem quebrar o layout', () => {
    render(<DealCard deal={buildDeal({ mediaUrl: null, mediaLocalPath: null })} />);

    const image = screen.getByAltText('Fone Bluetooth XYZ') as HTMLImageElement;
    expect(image.src).toContain('/placeholder-deal.png');
  });

  it('usa mediaLocalPath como fallback quando mediaUrl é nulo', () => {
    render(
      <DealCard
        deal={buildDeal({ mediaUrl: null, mediaLocalPath: 'http://api:8080/media/xyz.jpg' })}
      />
    );

    const image = screen.getByAltText('Fone Bluetooth XYZ') as HTMLImageElement;
    expect(image.src).toBe('http://api:8080/media/xyz.jpg');
  });

  it('desconto zero: não renderiza badge nem preço riscado', () => {
    render(<DealCard deal={buildDeal({ discountPct: 0, originalPrice: 99.9 })} />);

    expect(screen.queryByTestId('discount-badge')).not.toBeInTheDocument();
    expect(screen.queryByText('R$ 149,90')).not.toBeInTheDocument();
  });

  it('sem affiliateLink: não renderiza CTA clicável', () => {
    render(<DealCard deal={buildDeal({ affiliateLink: null })} />);

    expect(screen.queryByRole('link', { name: /ver oferta/i })).not.toBeInTheDocument();
    expect(screen.getByText(/indisponível/i)).toBeInTheDocument();
  });
});
