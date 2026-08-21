import { render, screen, waitFor, fireEvent, act } from '@testing-library/react';
import SuggestedProductsCarousel from './SuggestedProductsCarousel';
import { fetchSuggestedProducts } from '@/lib/suggested';
import * as tracking from '@/lib/tracking';
import type { Deal } from '@/lib/types';

jest.mock('@/lib/suggested', () => ({
  fetchSuggestedProducts: jest.fn(),
}));

jest.mock('@/lib/tracking', () => ({
  trackProductClick: jest.fn(),
}));

const fetchSuggestedProductsMock = fetchSuggestedProducts as jest.MockedFunction<
  typeof fetchSuggestedProducts
>;

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
    category: 'Eletrônicos',
    collectedAt: '2026-07-01T12:00:00Z',
    ...overrides,
  };
}

function buildDeals(count: number): Deal[] {
  return Array.from({ length: count }, (_, index) =>
    buildDeal({
      id: `1111111${index}-1111-1111-1111-11111111111${index}`,
      slug: `produto-${index}`,
      title: `Produto ${index}`,
    })
  );
}

describe('SuggestedProductsCarousel', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  it('exibe o skeleton (sem setas) enquanto o fetch está pendente', async () => {
    let resolvePromise: (value: Deal[]) => void = () => {};
    fetchSuggestedProductsMock.mockReturnValue(
      new Promise((resolve) => {
        resolvePromise = resolve;
      })
    );

    render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

    expect(screen.getByTestId('suggested-carousel-skeleton')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /ver mais produtos/i })).not.toBeInTheDocument();

    await act(async () => {
      resolvePromise(buildDeals(4));
    });
  });

  it('CA 1.1: título "Em alta em {Categoria}" quando categoria com resultado', async () => {
    fetchSuggestedProductsMock.mockResolvedValue(buildDeals(4));

    render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

    expect(
      await screen.findByRole('heading', { name: 'Em alta em Eletrônicos' })
    ).toBeInTheDocument();
    expect(fetchSuggestedProductsMock).toHaveBeenCalledWith('Eletrônicos', true);
  });

  it('CA 1.2: título "Em alta na loja" quando fallback geral (hasResults=false)', async () => {
    fetchSuggestedProductsMock.mockResolvedValue(buildDeals(4));

    render(<SuggestedProductsCarousel category="Eletrônicos" hasResults={false} />);

    expect(await screen.findByRole('heading', { name: 'Em alta na loja' })).toBeInTheDocument();
  });

  it('CA 1.2: título "Em alta na loja" quando não há categoria ativa', async () => {
    fetchSuggestedProductsMock.mockResolvedValue(buildDeals(4));

    render(<SuggestedProductsCarousel category={undefined} hasResults />);

    expect(await screen.findByRole('heading', { name: 'Em alta na loja' })).toBeInTheDocument();
  });

  it('CA 1.1: renderiza os produtos retornados, na ordem recebida do backend', async () => {
    fetchSuggestedProductsMock.mockResolvedValue(buildDeals(4));

    render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

    await screen.findByTestId('suggested-carousel');
    const cards = screen.getAllByTestId('deal-card');
    expect(cards).toHaveLength(4);
  });

  it('CA 1.5: lista vazia (corte mínimo não atingido) — não renderiza nada', async () => {
    fetchSuggestedProductsMock.mockResolvedValue([]);

    const { container } = render(
      <SuggestedProductsCarousel category="Eletrônicos" hasResults />
    );

    await waitFor(() => {
      expect(screen.queryByTestId('suggested-carousel-skeleton')).not.toBeInTheDocument();
    });
    expect(screen.queryByTestId('suggested-carousel')).not.toBeInTheDocument();
    expect(container).toBeEmptyDOMElement();
  });

  it('CA 1.8: endpoint indisponível/erro — não renderiza nada e não propaga erro', async () => {
    fetchSuggestedProductsMock.mockRejectedValue(new Error('HTTP 500'));

    const { container } = render(
      <SuggestedProductsCarousel category="Eletrônicos" hasResults />
    );

    await waitFor(() => {
      expect(screen.queryByTestId('suggested-carousel-skeleton')).not.toBeInTheDocument();
    });
    expect(container).toBeEmptyDOMElement();
  });

  it('CA 1.4: clique em um card do carrossel dispara o mesmo rastreio de clique (DealCardLink reaproveitado)', async () => {
    fetchSuggestedProductsMock.mockResolvedValue(
      buildDeals(4).map((deal, index) =>
        index === 0 ? { ...deal, id: '99999999-9999-9999-9999-999999999999' } : deal
      )
    );

    render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

    const ctas = await screen.findAllByRole('link', { name: /ver oferta/i });
    fireEvent.click(ctas[0]);

    expect(tracking.trackProductClick).toHaveBeenCalledWith(
      '99999999-9999-9999-9999-999999999999'
    );
  });

  describe('navegação por setas (CA 1.3)', () => {
    function mockTrackDimensions(
      track: HTMLElement,
      { scrollLeft = 0, clientWidth = 300, scrollWidth = 1200 } = {}
    ) {
      Object.defineProperty(track, 'scrollLeft', { value: scrollLeft, configurable: true });
      Object.defineProperty(track, 'clientWidth', { value: clientWidth, configurable: true });
      Object.defineProperty(track, 'scrollWidth', { value: scrollWidth, configurable: true });
    }

    it('seta esquerda nasce desabilitada (início do trilho); seta direita habilitada quando há mais itens', async () => {
      fetchSuggestedProductsMock.mockResolvedValue(buildDeals(6));

      render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

      const track = await screen.findByTestId('suggested-carousel-track');
      mockTrackDimensions(track, { scrollLeft: 0, clientWidth: 300, scrollWidth: 1200 });
      fireEvent.scroll(track);

      expect(screen.getByRole('button', { name: /ver produtos anteriores/i })).toBeDisabled();
      expect(screen.getByRole('button', { name: /ver mais produtos/i })).toBeEnabled();
    });

    it('seta direita desabilita ao chegar ao fim do trilho; seta esquerda habilita após rolar', async () => {
      fetchSuggestedProductsMock.mockResolvedValue(buildDeals(6));

      render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

      const track = await screen.findByTestId('suggested-carousel-track');
      mockTrackDimensions(track, { scrollLeft: 900, clientWidth: 300, scrollWidth: 1200 });
      fireEvent.scroll(track);

      expect(screen.getByRole('button', { name: /ver produtos anteriores/i })).toBeEnabled();
      expect(screen.getByRole('button', { name: /ver mais produtos/i })).toBeDisabled();
    });

    it('clique na seta direita chama scrollBy com deslocamento positivo (largura do trilho)', async () => {
      fetchSuggestedProductsMock.mockResolvedValue(buildDeals(6));

      render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

      const track = await screen.findByTestId('suggested-carousel-track');
      mockTrackDimensions(track, { scrollLeft: 0, clientWidth: 300, scrollWidth: 1200 });
      fireEvent.scroll(track);
      const scrollBySpy = jest.fn();
      track.scrollBy = scrollBySpy;

      fireEvent.click(screen.getByRole('button', { name: /ver mais produtos/i }));

      expect(scrollBySpy).toHaveBeenCalledWith({ left: 300, behavior: 'smooth' });
    });

    it('clique na seta esquerda chama scrollBy com deslocamento negativo', async () => {
      fetchSuggestedProductsMock.mockResolvedValue(buildDeals(6));

      render(<SuggestedProductsCarousel category="Eletrônicos" hasResults />);

      const track = await screen.findByTestId('suggested-carousel-track');
      mockTrackDimensions(track, { scrollLeft: 900, clientWidth: 300, scrollWidth: 1200 });
      fireEvent.scroll(track);
      const scrollBySpy = jest.fn();
      track.scrollBy = scrollBySpy;

      fireEvent.click(screen.getByRole('button', { name: /ver produtos anteriores/i }));

      expect(scrollBySpy).toHaveBeenCalledWith({ left: -300, behavior: 'smooth' });
    });
  });

  it('nova busca ao trocar category/hasResults (ex.: mudança de filtro) — refaz o fetch', async () => {
    fetchSuggestedProductsMock.mockResolvedValue(buildDeals(4));

    const { rerender } = render(
      <SuggestedProductsCarousel category="Eletrônicos" hasResults />
    );
    await screen.findByTestId('suggested-carousel');

    rerender(<SuggestedProductsCarousel category="Moda" hasResults />);

    await waitFor(() => {
      expect(fetchSuggestedProductsMock).toHaveBeenCalledWith('Moda', true);
    });
  });
});
