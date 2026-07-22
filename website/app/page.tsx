import Link from 'next/link';
import Header from '@/components/Header';
import DealCard from '@/components/DealCard';
import { fetchDeals } from '@/lib/api';

export const revalidate = 300;

interface HomePageProps {
  searchParams: {
    page?: string;
    platform?: string;
  };
}

const PAGE_SIZE = 12;

export default async function Home({ searchParams }: HomePageProps) {
  const page = Number(searchParams.page ?? '1') || 1;
  const platform = searchParams.platform;

  const result = await fetchDeals(page, PAGE_SIZE);
  const deals = platform ? result.items.filter((deal) => deal.platform === platform) : result.items;

  const platformQuery = platform ? `&platform=${platform}` : '';
  const hasPrevious = page > 1;
  const hasNext = page < result.totalPages;

  return (
    <main>
      <Header activePlatform={platform} />

      <h1>O Mulet Achou</h1>
      <p>As melhores ofertas do dia — selecionadas pelo Mulet!</p>

      {deals.length === 0 ? (
        <div className="deals-empty" data-testid="deals-empty">
          <p>Nenhuma oferta encontrada.</p>
        </div>
      ) : (
        <section className="deals-grid" data-testid="deals-grid">
          {deals.map((deal) => (
            <DealCard key={deal.slug} deal={deal} />
          ))}
        </section>
      )}

      <nav className="deals-pagination" aria-label="Paginação de ofertas">
        {hasPrevious && <Link href={`/?page=${page - 1}${platformQuery}`}>Anterior</Link>}
        <span>
          Página {result.page} de {Math.max(result.totalPages, 1)}
        </span>
        {hasNext && <Link href={`/?page=${page + 1}${platformQuery}`}>Próxima</Link>}
      </nav>
    </main>
  );
}
