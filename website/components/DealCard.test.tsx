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
    expect(image.src).toContain('/placeholder-deal.svg');
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

  describe('tag de plataforma (Issue #229)', () => {
    it.each([
      ['Amazon', 'Amazon'],
      ['MercadoLivre', 'Mercado Livre'],
      ['Shopee', 'Shopee'],
    ])('CA 1-3, 8: exibe a tag com o texto de exibição para platform=%s', (platform, label) => {
      render(<DealCard deal={buildDeal({ platform })} />);

      const tag = screen.getByTestId('platform-tag');
      expect(tag).toHaveTextContent(label);
    });

    it('CA 4: platform ausente (null) — tag não é renderizada', () => {
      render(<DealCard deal={buildDeal({ platform: null })} />);

      expect(screen.queryByTestId('platform-tag')).not.toBeInTheDocument();
    });

    it('CA 4: platform ausente (undefined) — tag não é renderizada', () => {
      render(<DealCard deal={buildDeal({ platform: undefined })} />);

      expect(screen.queryByTestId('platform-tag')).not.toBeInTheDocument();
    });

    it('CA 5: platform com valor não mapeado — tag não é renderizada e o valor cru não vaza para a tela', () => {
      render(<DealCard deal={buildDeal({ platform: 'Aliexpress' })} />);

      expect(screen.queryByTestId('platform-tag')).not.toBeInTheDocument();
      expect(screen.queryByText('Aliexpress')).not.toBeInTheDocument();
    });

    it('CA 7: a tag não é elemento interativo (sem href/role de link/botão/onClick)', () => {
      render(<DealCard deal={buildDeal({ platform: 'Amazon' })} />);

      const tag = screen.getByTestId('platform-tag');
      expect(tag.tagName).toBe('SPAN');
      expect(tag).not.toHaveAttribute('href');
      expect(tag).not.toHaveAttribute('onclick');
      expect(tag).not.toHaveAttribute('tabindex');
      expect(screen.queryByRole('link', { name: /amazon/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /amazon/i })).not.toBeInTheDocument();
    });
  });
});
