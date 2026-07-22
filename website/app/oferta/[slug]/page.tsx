import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { fetchDeal } from '@/lib/api';
import { getRelatedDeals } from '@/lib/related-deals';
import DealDetail from '@/components/DealDetail';
import {
  buildDealCanonicalUrl,
  buildDealDescription,
  buildDealJsonLd,
  buildDealTitle,
  resolveDealOgImage,
  safeJsonLdStringify,
} from '@/lib/seo';

export const revalidate = 300;

interface OfertaPageProps {
  params: { slug: string };
}

export async function generateMetadata({ params }: OfertaPageProps): Promise<Metadata> {
  const deal = await fetchDeal(params.slug);

  if (!deal) {
    return {
      title: 'Oferta não encontrada | O Mulet Achou',
    };
  }

  const description = buildDealDescription(deal);
  const url = buildDealCanonicalUrl(deal);
  const image = resolveDealOgImage(deal);

  return {
    title: buildDealTitle(deal),
    description,
    alternates: {
      canonical: url,
    },
    openGraph: {
      title: deal.title,
      description,
      url,
      images: [{ url: image }],
    },
  };
}

export default async function OfertaPage({ params }: OfertaPageProps) {
  const deal = await fetchDeal(params.slug);

  if (!deal) {
    notFound();
  }

  const jsonLd = buildDealJsonLd(deal);
  const relatedDeals = await getRelatedDeals(deal);

  return (
    <main>
      <script
        type="application/ld+json"
        // eslint-disable-next-line react/no-danger
        dangerouslySetInnerHTML={{ __html: safeJsonLdStringify(jsonLd) }}
      />
      <DealDetail deal={deal} relatedDeals={relatedDeals} />
    </main>
  );
}
