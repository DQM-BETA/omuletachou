'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
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

// Filtros "restritivos" — contam para o badge/estado de "Limpar filtros" e geram pílula.
// `sort` não conta (reordena, não restringe — decisão de UX registrada na spec §6.4).
const RESTRICTIVE_KEYS = ['category', 'subcategory', 'minPrice', 'maxPrice'] as const;
type RestrictiveKey = (typeof RESTRICTIVE_KEYS)[number];

// Fora de escopo visual (spec §6.2): limites reais viriam do catálogo; usados aqui como
// default sensato de UI, sem fixar contrato de dado.
const PRICE_MIN = 0;
const PRICE_MAX = 5000;

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

  const handleMinPriceChange = (value: string) => {
    updateParams({ minPrice: value });
  };

  const handleMaxPriceChange = (value: string) => {
    updateParams({ maxPrice: value });
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

  function PriceGroup() {
    return (
      <div className="filter-bar__group filter-bar__group--price">
        <span className="filter-bar__label" id="filter-bar-price-label">
          Preço
        </span>
        <div className="filter-bar__price">
          <div className="filter-bar__price-values">
            <span>{formatPrice(minPrice)}</span>
            <span>
              {formatPrice(maxPrice)}
              {maxPrice >= PRICE_MAX ? '+' : ''}
            </span>
          </div>
          <div className="filter-bar__price-slider">
            <div className="filter-bar__price-track" />
            <div
              className="filter-bar__price-range"
              style={{
                left: `${(minPrice / PRICE_MAX) * 100}%`,
                right: `${100 - (maxPrice / PRICE_MAX) * 100}%`,
              }}
            />
            <input
              type="range"
              aria-label="Preço mínimo"
              min={PRICE_MIN}
              max={PRICE_MAX}
              value={minPrice}
              className="filter-bar__price-input"
              onChange={(e) => handleMinPriceChange(e.target.value)}
            />
            <input
              type="range"
              aria-label="Preço máximo"
              min={PRICE_MIN}
              max={PRICE_MAX}
              value={maxPrice}
              className="filter-bar__price-input"
              onChange={(e) => handleMaxPriceChange(e.target.value)}
            />
          </div>
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
          <PriceGroup />
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
                  <PriceGroup />
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
