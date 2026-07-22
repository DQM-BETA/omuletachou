import type { Deal } from '@/lib/types';
import { formatPriceBRL, resolveDealImageUrl } from '@/lib/format';
import DealCard from './DealCard';

interface DealDetailProps {
  deal: Deal;
  relatedDeals?: Deal[];
}

export default function DealDetail({ deal, relatedDeals = [] }: DealDetailProps) {
  const hasDiscount = deal.discountPct > 0;
  const imageUrl = resolveDealImageUrl(deal);

  return (
    <article className="deal-detail" data-testid="deal-detail">
      <div className="deal-detail__media">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={imageUrl} alt={deal.title} className="deal-detail__image" />
      </div>

      <div className="deal-detail__info">
        <h1 className="deal-detail__title">{deal.title}</h1>
        <span className="deal-detail__category" data-testid="deal-category">
          {deal.category}
        </span>

        <div className="deal-detail__price">
          <span className="deal-detail__price-current">{formatPriceBRL(deal.salePrice)}</span>
          {hasDiscount && (
            <>
              <span className="deal-detail__price-strike">{formatPriceBRL(deal.originalPrice)}</span>
              <span className="deal-detail__badge" data-testid="discount-badge">
                -{deal.discountPct}%
              </span>
            </>
          )}
        </div>

        {deal.affiliateLink ? (
          <a
            className="deal-detail__cta"
            href={deal.affiliateLink}
            target="_blank"
            rel="nofollow"
          >
            Comprar agora →
          </a>
        ) : (
          <span className="deal-detail__cta deal-detail__cta--disabled" aria-disabled="true">
            Indisponível
          </span>
        )}
      </div>

      {relatedDeals.length > 0 && (
        <section className="deal-detail__related" data-testid="related-deals">
          <h2>Mais ofertas</h2>
          <div className="deal-detail__related-grid" data-testid="related-deals-grid">
            {relatedDeals.map((related) => (
              <DealCard key={related.slug} deal={related} />
            ))}
          </div>
        </section>
      )}
    </article>
  );
}
