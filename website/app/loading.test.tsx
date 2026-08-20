import { render, screen } from '@testing-library/react';
import Loading from './loading';

// Issue #260 (T-03), CA 2.2 — Suspense fallback de rota (convenção de arquivo do App Router).
describe('app/loading.tsx (Suspense fallback de rota)', () => {
  it('renderiza um estado de loading anunciado a leitores de tela', () => {
    render(<Loading />);

    expect(screen.getByTestId('deals-loading')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Carregando ofertas');
  });

  it('renderiza um skeleton de grade (não a página real, não o estado vazio)', () => {
    render(<Loading />);

    expect(screen.queryByTestId('deals-grid')).not.toBeInTheDocument();
    expect(screen.queryByTestId('deals-empty')).not.toBeInTheDocument();
  });
});
