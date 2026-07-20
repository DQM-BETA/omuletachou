import type { Metadata } from 'next';
import Link from 'next/link';
import Header from '@/components/Header';
import DealCard from '@/components/DealCard';
import { fetchByCategory } from '@/lib/api';

export const revalidate = 300;

const PAGE_SIZE = 12;

interface CategoriaPageProps {
  params: { categoria: string };
  searchParams: { page?: string };
}

/**
 * Formata o segmento de rota da categoria (ex.: "casa-e-decoracao") em um rótulo legível
 * (ex.: "Casa E Decoracao"). Não restaura acentuação (fora de escopo: exigiria um dicionário
 * de categorias no backend, não previsto no contrato atual de `lib/api.ts`).
 */
function formatCategoriaLabel(categoria: string): string {
  const decoded = decodeURIComponent(categoria);
  return decoded
    .split(/[-_\s]+/)
    .filter(Boolean)
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ');
}

export async function generateMetadata({
  params,
}: Pick<CategoriaPageProps, 'params'>): Promise<Metadata> {
  const categoriaLabel = formatCategoriaLabel(params.categoria);
  return {
    title: `${categoriaLabel} | O Mulet Achou`,
  };
}

export default async function CategoriaPage({ params, searchParams }: CategoriaPageProps) {
  const page = Number(searchParams.page ?? '1') || 1;
  const categoriaLabel = formatCategoriaLabel(params.categoria);

  // CA-C4: categoria inexistente/sem ofertas NUNCA chama notFound() — renderiza estado vazio.
  const result = await fetchByCategory(params.categoria, page, PAGE_SIZE);
  const deals = result.items;

  const hasPrevious = page > 1;
  const hasNext = page < result.totalPages;

  return (
    <main>
      <Header />

      <h1>{categoriaLabel}</h1>

      {deals.length === 0 ? (
        <div className="deals-empty" data-testid="deals-empty">
          <p>Nenhuma oferta encontrada nesta categoria.</p>
          <Link href="/">Ver todas as ofertas</Link>
        </div>
      ) : (
        <>
          <section className="deals-grid" data-testid="deals-grid">
            {deals.map((deal) => (
              <DealCard key={deal.slug} deal={deal} />
            ))}
          </section>

          <nav className="deals-pagination" aria-label="Paginação de ofertas">
            {hasPrevious && (
              <Link href={`/categoria/${params.categoria}?page=${page - 1}`}>Anterior</Link>
            )}
            <span>
              Página {result.page} de {Math.max(result.totalPages, 1)}
            </span>
            {hasNext && <Link href={`/categoria/${params.categoria}?page=${page + 1}`}>Próxima</Link>}
          </nav>
        </>
      )}
    </main>
  );
}
