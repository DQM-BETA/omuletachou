'use client';

import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import type { CategoryTree } from '@/lib/types';

interface FilterBarProps {
  categories: CategoryTree[];
}

interface DropdownOption {
  value: string;
  label: string;
  count?: number;
}

type DropdownName = 'category' | 'subcategory' | 'sort';

const SORT_OPTIONS: DropdownOption[] = [
  { value: '', label: 'Relevância' },
  { value: 'price_asc', label: 'Menor preço' },
  { value: 'discount_desc', label: 'Maior desconto' },
  { value: 'recent', label: 'Mais recente' },
];

const DISCOUNT_OPTIONS = [10, 30, 50];

// Filtros "restritivos" — contam para o badge/estado de "Limpar filtros" e geram pílula.
// `sort` não conta (reordena, não restringe — decisão de UX registrada na spec §6.4).
const RESTRICTIVE_KEYS = ['category', 'subcategory', 'minPrice', 'maxPrice', 'minDiscount'] as const;
type RestrictiveKey = (typeof RESTRICTIVE_KEYS)[number];

// Fora de escopo visual (spec §6.2): limites reais viriam do catálogo; usados aqui como
// default sensato de UI, sem fixar contrato de dado.
const PRICE_MIN = 0;
const PRICE_MAX = 5000;

// Causa raiz do bug do slider (design.md §"Investigação do bug do item 2"): cada onChange do
// range disparava router.push() síncrono direto na URL; um arrasto rápido gera dezenas de
// chamadas/segundo, excedendo o throttle de history.pushState do Chromium (~100/10s) e lançando
// um SecurityError não tratado (sem error.tsx na árvore app/, cai no fallback genérico do
// Next.js). Correção: estado local de rascunho (min/maxDraft) desacoplado da URL durante o
// arrasto; commit (router.replace, não push) só ao soltar o gesto e/ou após este debounce —
// reduz o volume de navegações de "um por frame" para "um por gesto/pausa".
const PRICE_COMMIT_DEBOUNCE_MS = 250;

function clampToPriceRange(value: number): number {
  if (Number.isNaN(value)) {
    return PRICE_MIN;
  }
  return Math.min(PRICE_MAX, Math.max(PRICE_MIN, value));
}

// Distância de rolagem (px) a partir da qual o FAB reaparece (spec §5.2 — reabre o painel sem
// precisar rolar de volta ao topo). `.filter-bar--mobile` é `position: sticky`, então medir seu
// próprio `getBoundingClientRect()` nunca funcionaria (um elemento sticky nunca sai da viewport
// enquanto "grudado") — usamos `window.scrollY` como proxy simples e determinístico.
const FAB_SCROLL_THRESHOLD = 400;

/** Hook simples de media query — decide qual dos 2 layouts (desktop/mobile) montar no DOM,
 * evitando duplicar a marcação dos mesmos controles em 2 lugares (spec §5) e problemas de
 * papéis/nomes acessíveis duplicados em testes. Default `false` (mobile) até o mount client. */
function useIsDesktop(breakpoint = 1024): boolean {
  const [isDesktop, setIsDesktop] = useState(false);

  useEffect(() => {
    const mql = window.matchMedia(`(min-width: ${breakpoint}px)`);
    const update = () => setIsDesktop(mql.matches);
    update();
    mql.addEventListener('change', update);
    return () => mql.removeEventListener('change', update);
  }, [breakpoint]);

  return isDesktop;
}

function formatPrice(value: number): string {
  return `R$ ${value.toLocaleString('pt-BR')}`;
}

export default function FilterBar({ categories }: FilterBarProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const category = searchParams.get('category') ?? '';
  const subcategory = searchParams.get('subcategory') ?? '';
  const minPriceParam = searchParams.get('minPrice');
  const maxPriceParam = searchParams.get('maxPrice');
  const minDiscount = searchParams.get('minDiscount') ?? '';
  const sort = searchParams.get('sort') ?? '';

  const minPrice = minPriceParam !== null ? Number(minPriceParam) : PRICE_MIN;
  const maxPrice = maxPriceParam !== null ? Number(maxPriceParam) : PRICE_MAX;

  const isDesktop = useIsDesktop();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [openDropdown, setOpenDropdown] = useState<DropdownName | null>(null);
  const [fabVisible, setFabVisible] = useState(false);

  useEffect(() => {
    if (isDesktop) {
      setFabVisible(false);
      return undefined;
    }

    const handleScroll = () => {
      setFabVisible(window.scrollY > FAB_SCROLL_THRESHOLD);
    };

    handleScroll();
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, [isDesktop]);

  const categoryOptions: DropdownOption[] = useMemo(
    () => categories.map((c) => ({ value: c.category, label: c.category, count: c.count })),
    [categories]
  );

  const subcategoryOptions: DropdownOption[] = useMemo(() => {
    const found = categories.find((c) => c.category === category);
    return (found?.subcategories ?? []).map((s) => ({
      value: s.subcategory,
      label: s.subcategory,
      count: s.count,
    }));
  }, [categories, category]);

  const subcategoryDisabled = !category || subcategoryOptions.length === 0;

  const updateParams = useCallback(
    (updates: Partial<Record<RestrictiveKey | 'sort', string | undefined>>) => {
      const params = new URLSearchParams(searchParams.toString());
      Object.entries(updates).forEach(([key, value]) => {
        if (value === undefined || value === '') {
          params.delete(key);
        } else {
          params.set(key, value);
        }
      });
      params.delete('page'); // qualquer mudança de filtro reseta a paginação
      router.push(`${pathname}?${params.toString()}`);
    },
    [router, pathname, searchParams]
  );

  // Commit de preço usa router.replace (não push, ver design.md) — ajustar a faixa de preço é
  // refinamento contínuo do mesmo filtro, não deve empilhar entrada de histórico por ajuste, e
  // reduz ainda mais a chance de bater no throttle de history.replaceState do browser.
  const commitPriceParams = useCallback(
    (nextMinPrice: number, nextMaxPrice: number) => {
      const params = new URLSearchParams(searchParams.toString());
      if (nextMinPrice <= PRICE_MIN) {
        params.delete('minPrice');
      } else {
        params.set('minPrice', String(nextMinPrice));
      }
      if (nextMaxPrice >= PRICE_MAX) {
        params.delete('maxPrice');
      } else {
        params.set('maxPrice', String(nextMaxPrice));
      }
      params.delete('page');
      router.replace(`${pathname}?${params.toString()}`);
    },
    [router, pathname, searchParams]
  );

  // Estado local de "rascunho" do slider/campos de preço — desacoplado da URL a cada evento
  // (causa raiz do bug, ver design.md). Inicializado a partir da URL; ressincronizado sempre que
  // o valor efetivamente commitado na URL mudar por outra via (Limpar filtros, remover pílula,
  // navegação/back-forward) — nunca durante um arrasto em andamento, já que nesse intervalo a URL
  // não muda.
  const [minDraft, setMinDraft] = useState<number>(minPrice);
  const [maxDraft, setMaxDraft] = useState<number>(maxPrice);
  const [minPriceText, setMinPriceText] = useState<string>(String(minPrice));
  const [maxPriceText, setMaxPriceText] = useState<string>(String(maxPrice));
  const [priceError, setPriceError] = useState<string | null>(null);

  // Refs para o debounce ler sempre o valor mais recente (evita closure obsoleta do setTimeout).
  const minDraftRef = useRef(minPrice);
  const maxDraftRef = useRef(maxPrice);
  const priceCommitTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setMinDraft(minPrice);
    setMinPriceText(String(minPrice));
    minDraftRef.current = minPrice;
  }, [minPrice]);

  useEffect(() => {
    setMaxDraft(maxPrice);
    setMaxPriceText(String(maxPrice));
    maxDraftRef.current = maxPrice;
  }, [maxPrice]);

  // Limpa qualquer debounce pendente ao desmontar (evita commit/navegação após unmount).
  useEffect(
    () => () => {
      if (priceCommitTimer.current) {
        clearTimeout(priceCommitTimer.current);
      }
    },
    []
  );

  const commitPrice = useCallback(
    (rawMin: number, rawMax: number) => {
      let nextMin = clampToPriceRange(rawMin);
      const nextMax = clampToPriceRange(rawMax);
      if (nextMin > nextMax) {
        // Clamp silencioso do cruzamento min/max no commit do slider (arrasto contínuo) — o
        // caminho dos campos de texto (commitMinPriceText/commitMaxPriceText) bloqueia com
        // mensagem em vez de clampar (CA 3.4).
        nextMin = nextMax;
      }
      setMinDraft(nextMin);
      setMaxDraft(nextMax);
      setMinPriceText(String(nextMin));
      setMaxPriceText(String(nextMax));
      minDraftRef.current = nextMin;
      maxDraftRef.current = nextMax;
      setPriceError(null);
      commitPriceParams(nextMin, nextMax);
    },
    [commitPriceParams]
  );

  const scheduleDebouncedPriceCommit = useCallback(() => {
    if (priceCommitTimer.current) {
      clearTimeout(priceCommitTimer.current);
    }
    priceCommitTimer.current = setTimeout(() => {
      commitPrice(minDraftRef.current, maxDraftRef.current);
    }, PRICE_COMMIT_DEBOUNCE_MS);
  }, [commitPrice]);

  const cancelDebouncedPriceCommit = useCallback(() => {
    if (priceCommitTimer.current) {
      clearTimeout(priceCommitTimer.current);
      priceCommitTimer.current = null;
    }
  }, []);

  const handleMinPriceSliderChange = (value: number) => {
    minDraftRef.current = value;
    setMinDraft(value);
    setMinPriceText(String(value));
    scheduleDebouncedPriceCommit();
  };

  const handleMaxPriceSliderChange = (value: number) => {
    maxDraftRef.current = value;
    setMaxDraft(value);
    setMaxPriceText(String(value));
    scheduleDebouncedPriceCommit();
  };

  // Ao soltar o gesto (mouse/touch/teclado) commit imediato — o debounce acima é rede de
  // segurança cross-browser caso o evento de soltura não chegue de forma confiável.
  const handlePriceSliderCommit = () => {
    cancelDebouncedPriceCommit();
    commitPrice(minDraftRef.current, maxDraftRef.current);
  };

  const handleMinPriceTextChange = (value: string) => {
    setMinPriceText(value);
  };

  const handleMaxPriceTextChange = (value: string) => {
    setMaxPriceText(value);
  };

  const commitMinPriceText = () => {
    const raw = minPriceText.trim();
    if (raw === '' || Number.isNaN(Number(raw))) {
      // CA 3.6: entrada vazia/não numérica não lança exceção nem commita filtro inválido —
      // reverte ao último valor válido.
      setMinPriceText(String(minDraft));
      return;
    }
    let value = Number(raw);
    let message: string | null = null;
    if (value < 0) {
      // CA 3.5: valor negativo não é aplicado — normalizado para 0, com feedback visível.
      value = 0;
      message = 'O valor mínimo não pode ser negativo. Ajustado para R$ 0.';
    }
    value = clampToPriceRange(value); // CA 3.7: fora dos limites do catálogo — clamp sem erro.
    if (value > maxDraft) {
      // CA 3.4: min > max bloqueado com mensagem clara — não commita.
      setMinPriceText(String(minDraft));
      setPriceError('O valor mínimo não pode ser maior que o valor máximo.');
      return;
    }
    // commitPrice limpa priceError internamente (caminho do slider não tem mensagem) — chamar
    // setPriceError(message) depois, para a mensagem de valor negativo (se houver) prevalecer.
    commitPrice(value, maxDraft);
    setPriceError(message);
  };

  const commitMaxPriceText = () => {
    const raw = maxPriceText.trim();
    if (raw === '' || Number.isNaN(Number(raw))) {
      setMaxPriceText(String(maxDraft));
      return;
    }
    let value = Number(raw);
    let message: string | null = null;
    if (value < 0) {
      value = 0;
      message = 'O valor máximo não pode ser negativo. Ajustado para R$ 0.';
    }
    value = clampToPriceRange(value);
    if (value < minDraft) {
      setMaxPriceText(String(maxDraft));
      setPriceError('O valor máximo não pode ser menor que o valor mínimo.');
      return;
    }
    commitPrice(minDraft, value);
    setPriceError(message);
  };

  const handlePriceTextKeyDown = (
    event: KeyboardEvent<HTMLInputElement>,
    commit: () => void
  ) => {
    if (event.key === 'Enter') {
      commit();
      event.currentTarget.blur();
    }
  };

  const handleCategoryChange = (value: string) => {
    updateParams({ category: value || undefined, subcategory: undefined });
    setOpenDropdown(null);
  };

  const handleSubcategoryChange = (value: string) => {
    updateParams({ subcategory: value || undefined });
    setOpenDropdown(null);
  };

  const handleSortChange = (value: string) => {
    updateParams({ sort: value || undefined });
    setOpenDropdown(null);
  };

  const handleDiscountToggle = (value: number) => {
    updateParams({ minDiscount: minDiscount === String(value) ? undefined : String(value) });
  };

  const activeRestrictiveKeys = RESTRICTIVE_KEYS.filter((key) => searchParams.get(key));
  const activeFiltersCount = activeRestrictiveKeys.length;
  const hasActiveFilters = activeFiltersCount > 0;

  const handleClear = () => {
    const params = new URLSearchParams(searchParams.toString());
    RESTRICTIVE_KEYS.forEach((key) => params.delete(key));
    params.delete('page');
    router.push(`${pathname}?${params.toString()}`);
    setDrawerOpen(false);
  };

  const removeFilter = (key: RestrictiveKey) => {
    const updates: Partial<Record<RestrictiveKey, string | undefined>> = { [key]: undefined };
    if (key === 'category') {
      updates.subcategory = undefined;
    }
    if (key === 'minPrice' || key === 'maxPrice') {
      updates.minPrice = undefined;
      updates.maxPrice = undefined;
    }
    updateParams(updates);
  };

  const toggleDropdown = (name: DropdownName) => {
    setOpenDropdown((current) => (current === name ? null : name));
  };

  function Dropdown({
    name,
    label,
    placeholder,
    disabledPlaceholder,
    value,
    options,
    disabled,
    onChange,
  }: {
    name: DropdownName;
    label: string;
    placeholder: string;
    disabledPlaceholder?: string;
    value: string;
    options: DropdownOption[];
    disabled?: boolean;
    onChange: (value: string) => void;
  }) {
    const isOpen = openDropdown === name && !disabled;
    const selected = options.find((o) => o.value === value);
    const triggerText = disabled ? disabledPlaceholder ?? placeholder : selected?.label ?? placeholder;
    const panelId = `filter-bar-dropdown-${name}-panel`;

    return (
      <div className="filter-bar__dropdown">
        <button
          type="button"
          role="combobox"
          aria-label={label}
          aria-expanded={isOpen}
          aria-haspopup="listbox"
          aria-controls={panelId}
          aria-disabled={disabled || undefined}
          disabled={disabled}
          className={[
            'filter-bar__dropdown-trigger',
            selected ? 'filter-bar__dropdown-trigger--filled' : '',
            isOpen ? 'filter-bar__dropdown-trigger--open' : '',
            disabled ? 'filter-bar__dropdown-trigger--disabled' : '',
          ]
            .filter(Boolean)
            .join(' ')}
          onClick={() => toggleDropdown(name)}
        >
          {triggerText}
        </button>
        {isOpen && (
          <ul id={panelId} className="filter-bar__dropdown-panel" role="listbox" aria-label={label}>
            {options.map((option) => (
              <li
                key={option.value}
                role="option"
                aria-selected={option.value === value}
                className={[
                  'filter-bar__dropdown-option',
                  option.value === value ? 'filter-bar__dropdown-option--selected' : '',
                ]
                  .filter(Boolean)
                  .join(' ')}
                onClick={() => onChange(option.value)}
              >
                <span>{option.label}</span>
                {option.count !== undefined && (
                  <span className="filter-bar__dropdown-option-count">({option.count})</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }

  // Renderizado como valor JSX (não como componente aninhado `function PriceGroup() {}`) —
  // mesmo padrão já usado em `groupCategory`/`groupSubcategory`/`groupSort` abaixo. Um
  // componente declarado dentro do corpo de FilterBar ganha uma nova identidade de função a
  // cada render do pai; o React trata isso como um tipo diferente e desmonta/remonta a
  // subárvore inteira a cada re-render — inclusive os `<input type="range">`. Isso quebrava o
  // próprio mecanismo desta correção: cada `onChange` do slider (que atualiza o estado local)
  // remontava o `<input>`, destruindo o node DOM em pleno arrasto/digitação e derrubando o
  // foco/pointer capture nativo do navegador (confirmado por teste: `onBlur` não disparava após
  // um `onChange` porque o node antigo já estava desmontado). `priceGroup` como valor JSX simples
  // preserva a identidade do elemento entre renders, corrigindo isso.
  const priceGroup = (
      <div className="filter-bar__group filter-bar__group--price">
        <span className="filter-bar__label" id="filter-bar-price-label">
          Preço
        </span>
        <div className="filter-bar__price">
          <div className="filter-bar__price-values">
            <span>{formatPrice(minDraft)}</span>
            <span>
              {formatPrice(maxDraft)}
              {maxDraft >= PRICE_MAX ? '+' : ''}
            </span>
          </div>
          <div className="filter-bar__price-slider">
            <div className="filter-bar__price-track" />
            <div
              className="filter-bar__price-range"
              style={{
                left: `${(minDraft / PRICE_MAX) * 100}%`,
                right: `${100 - (maxDraft / PRICE_MAX) * 100}%`,
              }}
            />
            {/* Estado local (minDraft/maxDraft) durante o arrasto — nunca mais controlado
                direto pela URL a cada evento (causa raiz do bug, ver design.md). O commit à URL
                acontece só em handlePriceSliderCommit (soltura do gesto) e/ou no debounce
                agendado por handleMin/MaxPriceSliderChange. */}
            <input
              type="range"
              aria-label="Preço mínimo"
              min={PRICE_MIN}
              max={PRICE_MAX}
              value={minDraft}
              className="filter-bar__price-input"
              onChange={(e) => handleMinPriceSliderChange(Number(e.target.value))}
              onPointerUp={handlePriceSliderCommit}
              onMouseUp={handlePriceSliderCommit}
              onTouchEnd={handlePriceSliderCommit}
              onKeyUp={handlePriceSliderCommit}
            />
            <input
              type="range"
              aria-label="Preço máximo"
              min={PRICE_MIN}
              max={PRICE_MAX}
              value={maxDraft}
              className="filter-bar__price-input"
              onChange={(e) => handleMaxPriceSliderChange(Number(e.target.value))}
              onPointerUp={handlePriceSliderCommit}
              onMouseUp={handlePriceSliderCommit}
              onTouchEnd={handlePriceSliderCommit}
              onKeyUp={handlePriceSliderCommit}
            />
          </div>
          <div className="filter-bar__price-inputs">
            <label className="filter-bar__price-input-field">
              <span className="filter-bar__price-input-label">Mín.</span>
              <input
                type="number"
                inputMode="numeric"
                aria-label="Preço mínimo (digitar)"
                min={PRICE_MIN}
                max={PRICE_MAX}
                value={minPriceText}
                onChange={(e) => handleMinPriceTextChange(e.target.value)}
                onBlur={commitMinPriceText}
                onKeyDown={(e) => handlePriceTextKeyDown(e, commitMinPriceText)}
              />
            </label>
            <label className="filter-bar__price-input-field">
              <span className="filter-bar__price-input-label">Máx.</span>
              <input
                type="number"
                inputMode="numeric"
                aria-label="Preço máximo (digitar)"
                min={PRICE_MIN}
                max={PRICE_MAX}
                value={maxPriceText}
                onChange={(e) => handleMaxPriceTextChange(e.target.value)}
                onBlur={commitMaxPriceText}
                onKeyDown={(e) => handlePriceTextKeyDown(e, commitMaxPriceText)}
              />
            </label>
          </div>
          {priceError && (
            <p className="filter-bar__price-error" role="alert">
              {priceError}
            </p>
          )}
        </div>
      </div>
  );

  function DiscountGroup() {
    return (
      <div className="filter-bar__group filter-bar__group--discount">
        <span className="filter-bar__label" id="filter-bar-discount-label">
          Desconto mínimo
        </span>
        <div className="filter-bar__discount-group" role="group" aria-labelledby="filter-bar-discount-label">
          {DISCOUNT_OPTIONS.map((value) => {
            const active = minDiscount === String(value);
            return (
              <button
                key={value}
                type="button"
                aria-pressed={active}
                className={`filter-bar__discount-btn${active ? ' filter-bar__discount-btn--active' : ''}`}
                onClick={() => handleDiscountToggle(value)}
              >
                {value}%+
              </button>
            );
          })}
        </div>
      </div>
    );
  }

  function ClearButton() {
    return (
      <button
        type="button"
        className={`filter-bar__clear${!hasActiveFilters ? ' filter-bar__clear--disabled' : ''}`}
        disabled={!hasActiveFilters}
        onClick={handleClear}
      >
        Limpar filtros
      </button>
    );
  }

  function Pills() {
    if (!hasActiveFilters) {
      return null;
    }

    return (
      <div className="filter-bar__active-pills">
        {category && (
          <span className="filter-bar__pill">
            {category}
            <button
              type="button"
              className="filter-bar__pill-remove"
              aria-label={`Remover filtro ${category}`}
              onClick={() => removeFilter('category')}
            >
              ✕
            </button>
          </span>
        )}
        {subcategory && (
          <span className="filter-bar__pill">
            {subcategory}
            <button
              type="button"
              className="filter-bar__pill-remove"
              aria-label={`Remover filtro ${subcategory}`}
              onClick={() => removeFilter('subcategory')}
            >
              ✕
            </button>
          </span>
        )}
        {(minPriceParam !== null || maxPriceParam !== null) && (
          <span className="filter-bar__pill">
            {formatPrice(minPrice)} – {formatPrice(maxPrice)}
            <button
              type="button"
              className="filter-bar__pill-remove"
              aria-label="Remover filtro de preço"
              onClick={() => removeFilter('minPrice')}
            >
              ✕
            </button>
          </span>
        )}
        {minDiscount && (
          <span className="filter-bar__pill">
            {minDiscount}% OFF+
            <button
              type="button"
              className="filter-bar__pill-remove"
              aria-label={`Remover filtro ${minDiscount}% OFF+`}
              onClick={() => removeFilter('minDiscount')}
            >
              ✕
            </button>
          </span>
        )}
      </div>
    );
  }

  const groupCategory = (
    <div className="filter-bar__group">
      <span className="filter-bar__label">Categoria</span>
      <Dropdown
        name="category"
        label="Categoria"
        placeholder="Todas as categorias"
        value={category}
        options={categoryOptions}
        onChange={handleCategoryChange}
      />
    </div>
  );

  const groupSubcategory = (
    <div className="filter-bar__group">
      <span className="filter-bar__label">Subcategoria</span>
      <Dropdown
        name="subcategory"
        label="Subcategoria"
        placeholder="Todas as subcategorias"
        disabledPlaceholder="Escolha uma categoria"
        value={subcategory}
        options={subcategoryOptions}
        disabled={subcategoryDisabled}
        onChange={handleSubcategoryChange}
      />
    </div>
  );

  const groupSort = (
    <div className="filter-bar__group">
      <span className="filter-bar__label">Ordenar por</span>
      <Dropdown
        name="sort"
        label="Ordenar por"
        placeholder="Relevância"
        value={sort}
        options={SORT_OPTIONS}
        onChange={handleSortChange}
      />
    </div>
  );

  return (
    <div
      className={`filter-bar${isDesktop ? '' : ' filter-bar--mobile'}`}
      data-testid="filter-bar"
    >
      {isDesktop ? (
        <div className="filter-bar__row">
          {groupCategory}
          {groupSubcategory}
          {priceGroup}
          <DiscountGroup />
          {groupSort}
          <ClearButton />
        </div>
      ) : (
        <>
          <div className="filter-bar__summary">
            <button
              type="button"
              className="filter-bar__toggle"
              onClick={() => setDrawerOpen(true)}
              aria-haspopup="dialog"
              aria-expanded={drawerOpen}
              // aria-label fixo: garante nome acessível determinístico ("Filtros"),
              // independente do ícone via CSS ::before ou do badge de contagem — browsers
              // reais incluem conteúdo de pseudo-elementos no cálculo do nome acessível
              // (jsdom, usado nos testes Jest, não aplica CSS e não reproduz isso).
              aria-label="Filtros"
            >
              Filtros
              {activeFiltersCount > 0 && (
                <span className="filter-bar__toggle-badge" aria-hidden="true">
                  {activeFiltersCount}
                </span>
              )}
            </button>
            <div className="filter-bar__sort">
              <Dropdown
                name="sort"
                label="Ordenar por"
                placeholder="Relevância"
                value={sort}
                options={SORT_OPTIONS}
                onChange={handleSortChange}
              />
            </div>
          </div>

          {drawerOpen && (
            <div className={`filter-bar__drawer filter-bar__drawer--open`} role="dialog" aria-modal="true" aria-label="Filtros">
              <div
                className="filter-bar__drawer-overlay"
                onClick={() => setDrawerOpen(false)}
                data-testid="filter-bar-drawer-overlay"
              />
              <div className="filter-bar__drawer-panel">
                <div className="filter-bar__drawer-header">
                  <h2>Filtros</h2>
                  <button
                    type="button"
                    aria-label="Fechar filtros"
                    onClick={() => setDrawerOpen(false)}
                  >
                    ✕
                  </button>
                </div>
                <div className="filter-bar__drawer-body">
                  {groupCategory}
                  {groupSubcategory}
                  {priceGroup}
                  <DiscountGroup />
                </div>
                <div className="filter-bar__drawer-footer">
                  <ClearButton />
                  <button
                    type="button"
                    className="filter-bar__apply"
                    onClick={() => setDrawerOpen(false)}
                  >
                    Ver resultados
                  </button>
                </div>
              </div>
            </div>
          )}

          <button
            type="button"
            className={`filter-bar__fab${fabVisible ? ' filter-bar__fab--visible' : ''}`}
            hidden={!fabVisible}
            aria-label="Reabrir filtros"
            onClick={() => setDrawerOpen(true)}
          >
            Filtros
            {activeFiltersCount > 0 && (
              <span className="filter-bar__toggle-badge">{activeFiltersCount}</span>
            )}
          </button>
        </>
      )}

      <Pills />
    </div>
  );
}
