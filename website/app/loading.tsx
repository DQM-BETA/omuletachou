/**
 * Suspense fallback da rota `app/` (convenção de arquivo do App Router — sem plumbing manual de
 * estado). Issue #260 (T-03), design.md §5.5/especificacao-tecnica.md §5.5: cobre CA 2.2
 * (loading visível se a resposta ultrapassar tempo perceptível) para a busca textual e, de forma
 * incidental, para os demais filtros já existentes — gap que este arquivo fecha para toda a tela
 * `app/page.tsx` (hoje não há nenhum loading state). Conteúdo mínimo (skeleton simples,
 * reaproveitando a grade de `.deals-grid`); visual não é crítico — a issue não passou pelo
 * UX/UI (aditivo, refinável depois se o Gerente pedir).
 */
const SKELETON_CARDS = Array.from({ length: 8 }, (_, index) => index);

export default function Loading() {
  return (
    <main>
      <div className="deals-loading" data-testid="deals-loading" role="status" aria-live="polite">
        <span className="sr-only">Carregando ofertas…</span>
        <section className="deals-grid" aria-hidden="true">
          {SKELETON_CARDS.map((index) => (
            <div key={index} className="deal-card-skeleton" />
          ))}
        </section>
      </div>
    </main>
  );
}
