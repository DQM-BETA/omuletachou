import { render, screen, fireEvent, within } from '@testing-library/react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import FilterBar from './FilterBar';
import type { CategoryTree } from '@/lib/types';

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
  usePathname: jest.fn(),
  useSearchParams: jest.fn(),
}));

const mockPush = jest.fn();

function setSearchParams(query = ''): void {
  (useSearchParams as jest.Mock).mockReturnValue(new URLSearchParams(query));
}

function lastPushedParams(): URLSearchParams {
  const calls = mockPush.mock.calls;
  const [url] = calls[calls.length - 1];
  return new URL(url, 'http://localhost').searchParams;
}

const categories: CategoryTree[] = [
  {
    category: 'Eletrônicos',
    count: 10,
    subcategories: [
      { subcategory: 'Celulares', count: 6 },
      { subcategory: 'Fones', count: 4 },
    ],
  },
  {
    category: 'Geral',
    count: 2,
    subcategories: [],
  },
];

// Regex (não string exata) porque o botão "Filtros" ganha um badge numérico concatenado ao
// texto quando há filtro ativo (ex. "Filtros1") — ver `.filter-bar__toggle-badge`.
function openDrawer(): void {
  fireEvent.click(screen.getByRole('button', { name: /^Filtros/ }));
}

describe('FilterBar', () => {
  beforeEach(() => {
    mockPush.mockReset();
    (useRouter as jest.Mock).mockReturnValue({ push: mockPush });
    (usePathname as jest.Mock).mockReturnValue('/');
    setSearchParams('');
  });

  it('renderiza o resumo mobile (botão Filtros + ordenação) sem o drawer aberto', () => {
    render(<FilterBar categories={categories} />);

    expect(screen.getByRole('button', { name: 'Filtros' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Ordenar por' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Filtros' })).not.toBeInTheDocument();
  });

  it('CA 1.1/1.2: não exibe o seletor de desconto mínimo no drawer mobile nem gera pílula/estado para ele', () => {
    setSearchParams('minDiscount=30');
    render(<FilterBar categories={categories} />);
    openDrawer();

    expect(screen.queryByText('Desconto mínimo')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^\d+%\+$/ })).not.toBeInTheDocument();
    // `minDiscount` na URL não gera pílula nem conta como filtro ativo (código órfão removido).
    expect(screen.queryByText(/OFF\+/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeDisabled();
  });

  it('CA 7.1: dropdown de subcategoria fica desabilitado até escolher uma categoria', () => {
    render(<FilterBar categories={categories} />);
    openDrawer();

    const subcategoryTrigger = screen.getByRole('combobox', { name: 'Subcategoria' });
    expect(subcategoryTrigger).toBeDisabled();
    expect(subcategoryTrigger).toHaveTextContent('Escolha uma categoria');
  });

  it('CA 7.1: escolher a categoria "Geral" (sem subcategorias) mantém o dropdown desabilitado', () => {
    render(<FilterBar categories={categories} />);
    openDrawer();

    fireEvent.click(screen.getByRole('combobox', { name: 'Categoria' }));
    fireEvent.click(screen.getByRole('option', { name: /^Geral/ }));

    expect(lastPushedParams().get('category')).toBe('Geral');
  });

  it('seleciona categoria e reflete na querystring, limpando subcategoria anterior', () => {
    setSearchParams('category=Casa&subcategory=Panelas');
    render(<FilterBar categories={categories} />);
    openDrawer();

    fireEvent.click(screen.getByRole('combobox', { name: 'Categoria' }));
    fireEvent.click(screen.getByRole('option', { name: /Eletrônicos/ }));

    const params = lastPushedParams();
    expect(params.get('category')).toBe('Eletrônicos');
    expect(params.get('subcategory')).toBeNull();
  });

  it('combinação de filtros: subcategoria habilitada após categoria, gera category+subcategory juntos', () => {
    setSearchParams('category=Eletr%C3%B4nicos');
    render(<FilterBar categories={categories} />);
    openDrawer();

    const subcategoryTrigger = screen.getByRole('combobox', { name: 'Subcategoria' });
    expect(subcategoryTrigger).not.toBeDisabled();

    fireEvent.click(subcategoryTrigger);
    fireEvent.click(screen.getByRole('option', { name: /Celulares/ }));

    const params = lastPushedParams();
    expect(params.get('category')).toBe('Eletrônicos');
    expect(params.get('subcategory')).toBe('Celulares');
  });

  it('trocar a ordenação atualiza o parâmetro sort sem exigir o drawer aberto', () => {
    render(<FilterBar categories={categories} />);

    fireEvent.click(screen.getByRole('combobox', { name: 'Ordenar por' }));
    fireEvent.click(screen.getByRole('option', { name: 'Maior desconto' }));

    expect(lastPushedParams().get('sort')).toBe('discount_desc');
  });

  it('ajustar o slider de preço reflete minPrice/maxPrice na querystring', () => {
    render(<FilterBar categories={categories} />);
    openDrawer();

    fireEvent.change(screen.getByRole('slider', { name: 'Preço mínimo' }), {
      target: { value: '100' },
    });

    expect(lastPushedParams().get('minPrice')).toBe('100');
  });

  it('"Limpar filtros" fica desabilitado quando não há filtro ativo', () => {
    render(<FilterBar categories={categories} />);
    openDrawer();

    expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeDisabled();
  });

  it('"Limpar filtros" reseta category/subcategory/preço mas preserva sort', () => {
    setSearchParams('category=Eletr%C3%B4nicos&subcategory=Celulares&minPrice=100&maxPrice=500&sort=price_asc');
    render(<FilterBar categories={categories} />);
    openDrawer();

    fireEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }));

    const params = lastPushedParams();
    expect(params.get('category')).toBeNull();
    expect(params.get('subcategory')).toBeNull();
    expect(params.get('minPrice')).toBeNull();
    expect(params.get('maxPrice')).toBeNull();
    expect(params.get('sort')).toBe('price_asc');
  });

  it('exibe pílulas dos filtros ativos e remove individualmente', () => {
    setSearchParams('category=Eletr%C3%B4nicos&minPrice=100&maxPrice=500');
    render(<FilterBar categories={categories} />);

    const pills = screen.getByText('Eletrônicos').closest('.filter-bar__pill');
    expect(pills).toBeInTheDocument();
    expect(screen.getByText('R$ 100 – R$ 500')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Remover filtro Eletrônicos' }));

    const params = lastPushedParams();
    expect(params.get('category')).toBeNull();
    // minPrice/maxPrice não devem ser afetados ao remover apenas a pílula de categoria.
    expect(params.get('minPrice')).toBe('100');
    expect(params.get('maxPrice')).toBe('500');
  });

  it('fecha o drawer ao clicar em "Ver resultados"', () => {
    render(<FilterBar categories={categories} />);
    openDrawer();

    expect(screen.getByRole('dialog', { name: 'Filtros' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Ver resultados' }));

    expect(screen.queryByRole('dialog', { name: 'Filtros' })).not.toBeInTheDocument();
  });

  it('não renderiza nenhuma referência textual a plataforma (Amazon/MercadoLivre/Shopee) no DOM', () => {
    setSearchParams('category=Eletr%C3%B4nicos&subcategory=Celulares');
    render(<FilterBar categories={categories} />);
    openDrawer();

    const html = document.body.innerHTML;
    expect(html).not.toMatch(/amazon/i);
    expect(html).not.toMatch(/mercadolivre|mercado livre/i);
    expect(html).not.toMatch(/shopee/i);
    expect(html).not.toMatch(/platform/i);
  });

  it('FAB de reabertura fica oculto (hidden) antes de rolar além do threshold', () => {
    render(<FilterBar categories={categories} />);

    expect(screen.queryByRole('button', { name: 'Reabrir filtros' })).not.toBeInTheDocument();
  });

  it('FAB de reabertura aparece após rolar a página além do threshold (window.scrollY)', () => {
    render(<FilterBar categories={categories} />);

    Object.defineProperty(window, 'scrollY', { value: 500, configurable: true });
    fireEvent.scroll(window);

    expect(screen.getByRole('button', { name: 'Reabrir filtros' })).toBeInTheDocument();

    // Volta a rolar para o topo — FAB some de novo (não fica "grudado" incorretamente).
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
    fireEvent.scroll(window);

    expect(screen.queryByRole('button', { name: 'Reabrir filtros' })).not.toBeInTheDocument();
  });

  describe('layout desktop (>=1024px)', () => {
    const originalMatchMedia = window.matchMedia;

    beforeEach(() => {
      window.matchMedia = jest.fn().mockImplementation((query: string) => ({
        matches: true,
        media: query,
        onchange: null,
        addEventListener: () => {},
        removeEventListener: () => {},
        addListener: () => {},
        removeListener: () => {},
        dispatchEvent: () => false,
      }));
    });

    afterEach(() => {
      window.matchMedia = originalMatchMedia;
    });

    it('renderiza os 4 controles em linha única, sem o botão "Filtros" do resumo mobile e sem o filtro de desconto mínimo', () => {
      render(<FilterBar categories={categories} />);

      expect(screen.queryByRole('button', { name: 'Filtros' })).not.toBeInTheDocument();
      expect(screen.getByRole('combobox', { name: 'Categoria' })).toBeInTheDocument();
      expect(screen.getByRole('combobox', { name: 'Subcategoria' })).toBeInTheDocument();
      expect(screen.getByRole('combobox', { name: 'Ordenar por' })).toBeInTheDocument();
      expect(screen.getByRole('slider', { name: 'Preço mínimo' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeInTheDocument();
      expect(within(screen.getByTestId('filter-bar')).queryByText('Desconto mínimo')).not.toBeInTheDocument();
      expect(within(screen.getByTestId('filter-bar')).queryByRole('button', { name: /^\d+%\+$/ })).not.toBeInTheDocument();
    });
  });
});
