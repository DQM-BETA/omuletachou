import type { Deal } from '@/lib/types';
import { formatPriceBRL, resolveDealImageUrl } from '@/lib/format';

interface DealCardProps {
  deal: Deal;
}

export default function DealCard({ deal }: DealCardProps) {
  const hasDiscount = deal.discountPct > 0;
  const imageUrl = resolveDealImageUrl(deal);

  return (
    <article className="deal-card" data-testid="deal-card">
      <div className="deal-card__media">
        {/*
          Server Component: sem onError/client handler (evita boundary de Client Component
          desnecessário — CA-A7 já é coberto pelo fallback de resolveDealImageUrl para
          MediaUrl/mediaLocalPath ausentes; uma URL presente porém quebrada em runtime
          degrada para o ícone padrão do navegador, sem crash de layout).
        */}
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={imageUrl} alt={deal.title} className="deal-card__image" loading="lazy" />
        {hasDiscount && (
          <span className="deal-card__badge" data-testid="discount-badge">
            -{deal.discountPct}%
          </span>
        )}
      </div>

      <h3 className="deal-card__title">{deal.title}</h3>

      <div className="deal-card__price">
        <span className="deal-card__price-current">{formatPriceBRL(deal.salePrice)}</span>
        {hasDiscount && (
          <span className="deal-card__price-strike">{formatPriceBRL(deal.originalPrice)}</span>
        )}
      </div>

      {deal.affiliateLink ? (
        <a
          className="deal-card__cta"
          href={deal.affiliateLink}
          target="_blank"
          rel="nofollow"
        >
          Ver oferta →
        </a>
      ) : (
        <span className="deal-card__cta deal-card__cta--disabled" aria-disabled="true">
          Indisponível
        </span>
      )}
    </article>
  );
}
