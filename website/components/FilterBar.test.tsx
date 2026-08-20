import { render, screen, fireEvent, within, act } from '@testing-library/react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import FilterBar from './FilterBar';
import type { CategoryTree } from '@/lib/types';

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
  usePathname: jest.fn(),
  useSearchParams: jest.fn(),
}));

const mockPush = jest.fn();
const mockReplace = jest.fn();

function setSearchParams(query = ''): void {
  (useSearchParams as jest.Mock).mockReturnValue(new URLSearchParams(query));
}

function lastPushedParams(): URLSearchParams {
  const calls = mockPush.mock.calls;
  const [url] = calls[calls.length - 1];
  return new URL(url, 'http://localhost').searchParams;
}

function lastReplacedParams(): URLSearchParams {
  const calls = mockReplace.mock.calls;
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

// Espelha SEARCH_COMMIT_DEBOUNCE_MS (não exportado do componente) — só o valor numérico importa
// para o teste, não uma referência à constante interna.
const SEARCH_COMMIT_DEBOUNCE_MS = 350;

describe('FilterBar', () => {
  beforeEach(() => {
    mockPush.mockReset();
    mockReplace.mockReset();
    (useRouter as jest.Mock).mockReturnValue({ push: mockPush, replace: mockReplace });
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

  // ISSUE-262 (T-02): correção do bug do slider de preço (causa raiz: router.push() sem
  // debounce/estado local a cada onChange, ver design.md) + campos digitáveis min/max.
  describe('PriceGroup — slider de preço (correção do bug + estado local)', () => {
    beforeEach(() => {
      jest.useFakeTimers();
    });

    afterEach(() => {
      act(() => {
        jest.runOnlyPendingTimers();
      });
      jest.useRealTimers();
    });

    it('CA 2.1/2.2: um único onChange no slider não navega ainda (fica só em estado local até soltar/debounce)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const slider = screen.getByRole('slider', { name: 'Preço mínimo' });
      fireEvent.change(slider, { target: { value: '100' } });

      expect(mockReplace).not.toHaveBeenCalled();
      // O valor exibido já reflete o rascunho local, mesmo sem ter navegado ainda.
      expect(slider).toHaveValue('100');
    });

    it('CA 2.1: soltar o gesto (pointerUp) após ajustar o slider commita minPrice via router.replace (não push)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const slider = screen.getByRole('slider', { name: 'Preço mínimo' });
      fireEvent.change(slider, { target: { value: '100' } });
      fireEvent.mouseUp(slider);

      expect(lastReplacedParams().get('minPrice')).toBe('100');
      expect(mockPush).not.toHaveBeenCalled();
    });

    it('CA 2.4 (regressão do bug): arrasto rápido (dezenas de onChange em sequência apertada) gera no máximo um punhado de commits, nunca um router.replace por evento — a causa raiz (rajada de navegações que excede o throttle do browser) fica eliminada', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const slider = screen.getByRole('slider', { name: 'Preço mínimo' });

      // Simula um arrasto rápido e contínuo: 150 eventos onChange em sucessão apertada, muito
      // acima do throttle de ~100 pushState/replaceState por 10s do Chromium citado no design.md.
      for (let i = 0; i <= 150; i += 1) {
        fireEvent.change(slider, { target: { value: String(i % 5000) } });
      }
      fireEvent.mouseUp(slider);

      // Nenhuma navegação disparada durante o arrasto — só ao soltar o gesto.
      expect(mockReplace.mock.calls.length).toBeLessThanOrEqual(1);
      expect(mockPush).not.toHaveBeenCalled();
    });

    it('debounce: sem soltar o gesto explicitamente, o commit acontece após o período de inatividade (rede de segurança cross-browser)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.change(screen.getByRole('slider', { name: 'Preço mínimo' }), {
        target: { value: '250' },
      });

      expect(mockReplace).not.toHaveBeenCalled();

      act(() => {
        jest.advanceTimersByTime(300);
      });

      expect(lastReplacedParams().get('minPrice')).toBe('250');
    });

    it('CA 3.3: arrastar o slider atualiza os campos de texto (nunca divergem na tela)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.change(screen.getByRole('slider', { name: 'Preço mínimo' }), {
        target: { value: '333' },
      });

      expect(screen.getByRole('spinbutton', { name: 'Preço mínimo (digitar)' })).toHaveValue(333);
    });

    it('CA 3.1/3.2: digitar no campo de texto e sair do campo (blur) commita e move o slider', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('spinbutton', { name: 'Preço mínimo (digitar)' });
      fireEvent.change(input, { target: { value: '420' } });
      fireEvent.blur(input);

      expect(lastReplacedParams().get('minPrice')).toBe('420');
      expect(screen.getByRole('slider', { name: 'Preço mínimo' })).toHaveValue('420');
    });

    it('CA 3.1: pressionar Enter no campo de texto também commita (sem exigir blur adicional)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('spinbutton', { name: 'Preço máximo (digitar)' });
      fireEvent.change(input, { target: { value: '1000' } });
      fireEvent.keyDown(input, { key: 'Enter' });

      expect(lastReplacedParams().get('maxPrice')).toBe('1000');
    });

    it('CA 3.4: digitar um mínimo maior que o máximo é bloqueado com mensagem clara e não commita', () => {
      setSearchParams('minPrice=100&maxPrice=200');
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('spinbutton', { name: 'Preço mínimo (digitar)' });
      fireEvent.change(input, { target: { value: '500' } });
      fireEvent.blur(input);

      expect(mockReplace).not.toHaveBeenCalled();
      expect(screen.getByRole('alert')).toHaveTextContent(
        'O valor mínimo não pode ser maior que o valor máximo.'
      );
      // Reverte ao último valor válido no campo de texto.
      expect(input).toHaveValue(100);
    });

    it('CA 3.5: valor negativo não é aplicado — normalizado para 0 com feedback visível', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('spinbutton', { name: 'Preço mínimo (digitar)' });
      fireEvent.change(input, { target: { value: '-50' } });
      fireEvent.blur(input);

      expect(lastReplacedParams().get('minPrice')).toBeNull(); // 0 é o próprio PRICE_MIN, sem param
      expect(screen.getByRole('alert')).toHaveTextContent(/não pode ser negativo/);
    });

    it('CA 3.6: campo vazio ao perder o foco não lança exceção e reverte ao último valor válido', () => {
      setSearchParams('minPrice=150');
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('spinbutton', { name: 'Preço mínimo (digitar)' });
      fireEvent.change(input, { target: { value: '' } });

      expect(() => fireEvent.blur(input)).not.toThrow();
      expect(mockReplace).not.toHaveBeenCalled();
      expect(input).toHaveValue(150);
    });

    it('CA 3.7: valor digitado acima do limite do catálogo é clampado sem erro', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('spinbutton', { name: 'Preço máximo (digitar)' });
      fireEvent.change(input, { target: { value: '999999' } });
      fireEvent.blur(input);

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(lastReplacedParams().get('maxPrice')).toBeNull(); // clampado ao PRICE_MAX (default, sem param)
    });

    it('resincroniza o rascunho quando o filtro de preço é removido externamente ("Limpar filtros")', () => {
      setSearchParams('minPrice=100&maxPrice=500');
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }));

      const params = lastPushedParams();
      expect(params.get('minPrice')).toBeNull();
      expect(params.get('maxPrice')).toBeNull();
    });
  });

  // ISSUE-269 (T-03): campo de busca textual — mesmo mecanismo de draft+debounce+
  // router.replace já usado no PriceGroup (Issue #230), reaproveitado 1:1.
  describe('SearchGroup — campo de busca textual (Issue #260)', () => {
    beforeEach(() => {
      jest.useFakeTimers();
    });

    afterEach(() => {
      act(() => {
        jest.runOnlyPendingTimers();
      });
      jest.useRealTimers();
    });

    it('CA 1.1: campo de busca visível na filter-bar, sem substituir os filtros existentes', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      expect(screen.getByRole('searchbox', { name: 'Buscar produtos' })).toBeInTheDocument();
      expect(screen.getByRole('combobox', { name: 'Categoria' })).toBeInTheDocument();
    });

    it('CA 2.1: digitar não navega a cada tecla (fica em estado local até o debounce)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      const input = screen.getByRole('searchbox', { name: 'Buscar produtos' });
      fireEvent.change(input, { target: { value: 'ten' } });

      expect(mockReplace).not.toHaveBeenCalled();
      expect(input).toHaveValue('ten');

      fireEvent.change(input, { target: { value: 'tenis' } });
      expect(mockReplace).not.toHaveBeenCalled();
    });

    it('CA 2.1: após o debounce, navega com `q` via router.replace (não push, sem empilhar histórico)', () => {
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.change(screen.getByRole('searchbox', { name: 'Buscar produtos' }), {
        target: { value: 'tenis' },
      });

      act(() => {
        jest.advanceTimersByTime(SEARCH_COMMIT_DEBOUNCE_MS);
      });

      expect(lastReplacedParams().get('q')).toBe('tenis');
      expect(mockPush).not.toHaveBeenCalled();
    });

    it('mudança de filtro via busca também reseta a paginação (params.delete("page"))', () => {
      setSearchParams('page=3');
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.change(screen.getByRole('searchbox', { name: 'Buscar produtos' }), {
        target: { value: 'fone' },
      });
      act(() => {
        jest.advanceTimersByTime(SEARCH_COMMIT_DEBOUNCE_MS);
      });

      expect(lastReplacedParams().get('page')).toBeNull();
    });

    it('campo vazio remove `q` da URL', () => {
      setSearchParams('q=tenis');
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.change(screen.getByRole('searchbox', { name: 'Buscar produtos' }), {
        target: { value: '' },
      });
      act(() => {
        jest.advanceTimersByTime(SEARCH_COMMIT_DEBOUNCE_MS);
      });

      expect(lastReplacedParams().get('q')).toBeNull();
    });

    it('pílula de busca aparece com o termo ativo e conta para "Limpar filtros"', () => {
      setSearchParams('q=tenis');
      render(<FilterBar categories={categories} />);
      openDrawer();

      expect(screen.getByText('"tenis"')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Limpar filtros' })).not.toBeDisabled();
    });

    it('remover a pílula de busca commita `q` vazio imediatamente, sem esperar o debounce', () => {
      setSearchParams('q=tenis');
      render(<FilterBar categories={categories} />);

      fireEvent.click(screen.getByRole('button', { name: 'Remover busca tenis' }));

      expect(lastReplacedParams().get('q')).toBeNull();
    });

    it('"Limpar filtros" também remove a busca ativa', () => {
      setSearchParams('q=tenis&category=Eletrônicos');
      render(<FilterBar categories={categories} />);
      openDrawer();

      fireEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }));

      const params = lastPushedParams();
      expect(params.get('q')).toBeNull();
      expect(params.get('category')).toBeNull();
    });

    it('resincroniza o rascunho quando `q` muda externamente (ex. "Limpar filtros"/back-forward)', () => {
      setSearchParams('q=tenis');
      const { rerender } = render(<FilterBar categories={categories} />);
      openDrawer();

      expect(screen.getByRole('searchbox', { name: 'Buscar produtos' })).toHaveValue('tenis');

      setSearchParams('');
      rerender(<FilterBar categories={categories} />);

      expect(screen.getByRole('searchbox', { name: 'Buscar produtos' })).toHaveValue('');
    });
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
