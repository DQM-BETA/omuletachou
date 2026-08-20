import { render, screen } from '@testing-library/react';
import DealDetail from './DealDetail';
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

describe('DealDetail', () => {
  it('CA-B4: exibe mídia, título, preço grande, badge de desconto e CTA principal', () => {
    render(<DealDetail deal={buildDeal()} />);

    expect(screen.getByAltText('Fone Bluetooth XYZ')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Fone Bluetooth XYZ' })).toBeInTheDocument();
    expect(screen.getByText('R$ 99,90')).toBeInTheDocument();
    expect(screen.getByText('R$ 149,90')).toBeInTheDocument();
    expect(screen.getByTestId('discount-badge')).toHaveTextContent('-33%');
    expect(screen.getByRole('link', { name: /comprar agora/i })).toHaveAttribute(
      'href',
      'https://amazon.com/xyz?tag=abc'
    );
  });

  it('CTA principal usa target=_blank e rel=nofollow', () => {
    render(<DealDetail deal={buildDeal()} />);

    const cta = screen.getByRole('link', { name: /comprar agora/i });
    expect(cta).toHaveAttribute('target', '_blank');
    expect(cta).toHaveAttribute('rel', 'nofollow');
  });

  it('CA-B4: seção "Mais ofertas" exibe os produtos relacionados recebidos via prop', () => {
    render(
      <DealDetail
        deal={buildDeal()}
        relatedDeals={[
          buildDeal({ slug: 'related-1', title: 'Related 1' }),
          buildDeal({ slug: 'related-2', title: 'Related 2' }),
          buildDeal({ slug: 'related-3', title: 'Related 3' }),
          buildDeal({ slug: 'related-4', title: 'Related 4' }),
        ]}
      />
    );

    const relatedSection = screen.getByTestId('related-deals');
    expect(relatedSection).toBeInTheDocument();
    expect(screen.getAllByTestId('deal-card')).toHaveLength(4);
  });

  it('sem relacionados: seção "Mais ofertas" não é renderizada', () => {
    render(<DealDetail deal={buildDeal()} relatedDeals={[]} />);

    expect(screen.queryByTestId('related-deals')).not.toBeInTheDocument();
  });

  it('sem prop relatedDeals: seção "Mais ofertas" não é renderizada (default)', () => {
    render(<DealDetail deal={buildDeal()} />);

    expect(screen.queryByTestId('related-deals')).not.toBeInTheDocument();
  });

  it('desconto zero: não renderiza badge nem preço riscado', () => {
    render(<DealDetail deal={buildDeal({ discountPct: 0, originalPrice: 99.9 })} />);

    expect(screen.queryByTestId('discount-badge')).not.toBeInTheDocument();
  });

  it('sem affiliateLink: CTA principal fica indisponível', () => {
    render(<DealDetail deal={buildDeal({ affiliateLink: null })} />);

    expect(screen.queryByRole('link', { name: /comprar agora/i })).not.toBeInTheDocument();
    expect(screen.getByText(/indisponível/i)).toBeInTheDocument();
  });

  describe('tag de plataforma no produto principal (Issue #229, correção pós Code Review PR #257)', () => {
    it.each([
      ['Amazon', 'Amazon'],
      ['MercadoLivre', 'Mercado Livre'],
      ['Shopee', 'Shopee'],
    ])('CA 3, 8: exibe a tag com o texto de exibição para platform=%s', (platform, label) => {
      render(<DealDetail deal={buildDeal({ platform })} />);

      const tag = screen.getByTestId('platform-tag');
      expect(tag).toHaveTextContent(label);
    });

    it('CA 4: platform ausente (null) — tag não é renderizada', () => {
      render(<DealDetail deal={buildDeal({ platform: null })} />);

      expect(screen.queryByTestId('platform-tag')).not.toBeInTheDocument();
    });

    it('CA 4: platform ausente (undefined) — tag não é renderizada', () => {
      render(<DealDetail deal={buildDeal({ platform: undefined })} />);

      expect(screen.queryByTestId('platform-tag')).not.toBeInTheDocument();
    });

    it('CA 5: platform com valor não mapeado — tag não é renderizada e o valor cru não vaza para a tela', () => {
      render(<DealDetail deal={buildDeal({ platform: 'Aliexpress' })} />);

      expect(screen.queryByTestId('platform-tag')).not.toBeInTheDocument();
      expect(screen.queryByText('Aliexpress')).not.toBeInTheDocument();
    });

    it('CA 7: a tag não é elemento interativo (sem href/role de link/botão/onClick)', () => {
      render(<DealDetail deal={buildDeal({ platform: 'Amazon' })} />);

      const tag = screen.getByTestId('platform-tag');
      expect(tag.tagName).toBe('SPAN');
      expect(tag).not.toHaveAttribute('href');
      expect(tag).not.toHaveAttribute('onclick');
      expect(tag).not.toHaveAttribute('tabindex');
      expect(screen.queryByRole('link', { name: /amazon/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /amazon/i })).not.toBeInTheDocument();
    });

    it('CA 3: a tag do produto principal é distinta da tag dos relacionados (não depende de DealCard)', () => {
      render(
        <DealDetail
          deal={buildDeal({ platform: 'Amazon' })}
          relatedDeals={[
            buildDeal({ slug: 'related-1', title: 'Related 1', platform: 'Shopee', salePrice: 49.9 }),
          ]}
        />
      );

      const tags = screen.getAllByTestId('platform-tag');
      expect(tags).toHaveLength(2);

      const mainPrice = screen.getByText('R$ 99,90').closest('.deal-detail__price');
      expect(mainPrice).not.toBeNull();
      expect(mainPrice?.querySelector('[data-testid="platform-tag"]')).toHaveTextContent('Amazon');
    });
  });
});
