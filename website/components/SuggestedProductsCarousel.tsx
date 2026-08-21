'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import type { Deal } from '@/lib/types';
import { fetchSuggestedProducts } from '@/lib/suggested';
import DealCard from './DealCard';

interface SuggestedProductsCarouselProps {
  /** Categoria ativa no filtro da listagem principal (undefined/vazio = sem filtro). */
  category?: string;
  /** Se a listagem principal, com os filtros atuais, retornou pelo menos 1 produto. */
  hasResults: boolean;
}

// Número de cards-esqueleto exibidos durante o carregamento (ux-ui-spec.md §6) — usa a
// quantidade de itens totalmente visíveis do maior breakpoint de referência (desktop, §4), já
// que o CSS responsivo (não o JS) é quem decide quantos ficam visíveis em telas menores.
const SKELETON_ITEMS = 4;

/**
 * Faixa/carrossel de "produtos sugeridos" (Issue #231, sub-issue #280 — T-05).
 *
 * Client Component — busca em `useEffect` (especificacao-tecnica.md §4.4), isolada em
 * try/catch: falha de rede ou erro nunca propaga para o resto da página (CA 1.8), apenas omite
 * o carrossel (`return null`). O componente só consome/renderiza a lista já pronta devolvida
 * pelo backend, na ordem recebida — não decide fallback nem corte mínimo (design.md §6).
 *
 * Reaproveita `DealCard`/`DealCardLink` (Issue #279/T-04) para cada item — mesmo componente do
 * grid principal, garantindo que o clique no carrossel dispare o mesmo rastreio (CA 1.4).
 */
export default function SuggestedProductsCarousel({
  category,
  hasResults,
}: SuggestedProductsCarouselProps) {
  // `null` = ainda carregando (skeleton). `[]` = carregado, sem sugestões (nada renderiza).
  const [deals, setDeals] = useState<Deal[] | null>(null);
  const trackRef = useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setDeals(null);

    fetchSuggestedProducts(category, hasResults)
      .then((result) => {
        if (!cancelled) {
          setDeals(result);
        }
      })
      .catch(() => {
        // CA 1.8 — falha silenciosa: nenhuma sugestão disponível, sem erro visível ao usuário.
        if (!cancelled) {
          setDeals([]);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [category, hasResults]);

  // Recalcula o estado das setas (CA 1.3) a partir do scroll real do trilho — chamado no
  // `onScroll` (inclui arrasto manual/touch, não só clique nas setas, ux-ui-spec.md §5/§8.6) e
  // logo após os dados chegarem (primeiro render do trilho preenchido).
  const updateScrollState = useCallback(() => {
    const track = trackRef.current;
    if (!track) {
      return;
    }
    // Tolerância de 1px por arredondamento de subpixel (ux-ui-spec.md §5).
    setCanScrollLeft(track.scrollLeft > 1);
    setCanScrollRight(track.scrollLeft + track.clientWidth < track.scrollWidth - 1);
  }, []);

  useEffect(() => {
    if (deals && deals.length > 0) {
      updateScrollState();
    }
  }, [deals, updateScrollState]);

  const scrollByPage = (direction: 1 | -1) => {
    const track = trackRef.current;
    if (!track) {
      return;
    }
    track.scrollBy({ left: direction * track.clientWidth, behavior: 'smooth' });
  };

  // Loading — skeleton (ux-ui-spec.md §6): sem setas, mesma quantidade de itens de referência.
  if (deals === null) {
    return (
      <section
        className="suggested-carousel suggested-carousel--loading"
        data-testid="suggested-carousel-skeleton"
        aria-hidden="true"
      >
        <div className="suggested-carousel__title-skeleton" />
        <div className="suggested-carousel__wrapper">
          <div className="suggested-carousel__track">
            {Array.from({ length: SKELETON_ITEMS }).map((_, index) => (
              <div className="suggested-carousel__item-skeleton" key={index} />
            ))}
          </div>
        </div>
      </section>
    );
  }

  // Lista vazia (corte mínimo de 4 não atingido, CA 1.5) ou erro (CA 1.8) — mesmo resultado
  // visual: a faixa inteira desaparece, sem mensagem (ux-ui-spec.md §7).
  if (deals.length === 0) {
    return null;
  }

  // Título dinâmico (ux-ui-spec.md §2): só usa o nome da categoria quando o backend de fato
  // aplicou o filtro por categoria (hasResults=true e categories preenchido) — mesma condição
  // enviada ao endpoint em lib/suggested.ts.
  const title = category && hasResults ? `Em alta em ${category}` : 'Em alta na loja';

  return (
    <section className="suggested-carousel" data-testid="suggested-carousel">
      <h2 className="suggested-carousel__title">{title}</h2>
      <div className="suggested-carousel__wrapper">
        <button
          type="button"
          className="suggested-carousel__arrow suggested-carousel__arrow--left"
          aria-label="Ver produtos anteriores"
          disabled={!canScrollLeft}
          onClick={() => scrollByPage(-1)}
        >
          <span aria-hidden="true">‹</span>
        </button>

        <div
          ref={trackRef}
          className="suggested-carousel__track"
          role="region"
          aria-label="Produtos sugeridos"
          data-testid="suggested-carousel-track"
          onScroll={updateScrollState}
        >
          {deals.map((deal) => (
            <div className="suggested-carousel__item" key={deal.id}>
              <DealCard deal={deal} />
            </div>
          ))}
        </div>

        <button
          type="button"
          className="suggested-carousel__arrow suggested-carousel__arrow--right"
          aria-label="Ver mais produtos"
          disabled={!canScrollRight}
          onClick={() => scrollByPage(1)}
        >
          <span aria-hidden="true">›</span>
        </button>
      </div>
    </section>
  );
}
