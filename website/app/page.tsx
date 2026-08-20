import Link from 'next/link';
import Header from '@/components/Header';
import DealCard from '@/components/DealCard';
import FilterBar from '@/components/FilterBar';
import { fetchCategories, fetchDeals, type DealFilters } from '@/lib/api';

export const revalidate = 300;

interface HomePageProps {
  searchParams: {
    page?: string;
    category?: string;
    subcategory?: string;
    minPrice?: string;
    maxPrice?: string;
    sort?: string;
  };
}

const PAGE_SIZE = 12;

function buildFilters(searchParams: HomePageProps['searchParams']): DealFilters {
  return {
    category: searchParams.category,
    subcategory: searchParams.subcategory,
    minPrice: searchParams.minPrice !== undefined ? Number(searchParams.minPrice) : undefined,
    maxPrice: searchParams.maxPrice !== undefined ? Number(searchParams.maxPrice) : undefined,
    sort: searchParams.sort,
  };
}

/** Preserva os filtros ativos (exceto `page`) ao montar os links de paginação. */
function buildPaginationQuery(searchParams: HomePageProps['searchParams'], page: number): string {
  const params = new URLSearchParams();
  if (searchParams.category) params.set('category', searchParams.category);
  if (searchParams.subcategory) params.set('subcategory', searchParams.subcategory);
  if (searchParams.minPrice) params.set('minPrice', searchParams.minPrice);
  if (searchParams.maxPrice) params.set('maxPrice', searchParams.maxPrice);
  if (searchParams.sort) params.set('sort', searchParams.sort);
  params.set('page', String(page));
  return `?${params.toString()}`;
}

export default async function Home({ searchParams }: HomePageProps) {
  const page = Number(searchParams.page ?? '1') || 1;
  const filters = buildFilters(searchParams);
  const hasActiveFilters = Boolean(
    filters.category || filters.subcategory || filters.minPrice || filters.maxPrice
  );

  const [result, categories] = await Promise.all([
    fetchDeals(page, PAGE_SIZE, filters),
    fetchCategories(),
  ]);
  const deals = result.items;

  const hasPrevious = page > 1;
  const hasNext = page < result.totalPages;

  return (
    <main>
      <Header />

      <h1>O Mulet Achou</h1>
      <p>As melhores ofertas do dia — selecionadas pelo Mulet!</p>

      <FilterBar categories={categories} />

      {deals.length === 0 ? (
        <div className="deals-empty" data-testid="deals-empty">
          {hasActiveFilters ? (
            <>
              <p>Nenhuma oferta encontrada com esses filtros.</p>
              <p>Tente ajustar a faixa de preço ou os demais filtros.</p>
              <Link href="/" className="deal-card__cta">
                Ver todas as ofertas
              </Link>
            </>
          ) : (
            <p>Nenhuma oferta encontrada.</p>
          )}
        </div>
      ) : (
        <section className="deals-grid" data-testid="deals-grid">
          {deals.map((deal) => (
            <DealCard key={deal.slug} deal={deal} />
          ))}
        </section>
      )}

      <nav className="deals-pagination" aria-label="Paginação de ofertas">
        {hasPrevious && (
          <Link href={buildPaginationQuery(searchParams, page - 1)}>Anterior</Link>
        )}
        <span>
          Página {result.page} de {Math.max(result.totalPages, 1)}
        </span>
        {hasNext && <Link href={buildPaginationQuery(searchParams, page + 1)}>Próxima</Link>}
      </nav>
    </main>
  );
}
