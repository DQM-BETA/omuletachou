import { render, screen, fireEvent } from '@testing-library/react';
import ErrorBoundary from './error';

// ISSUE-262 (T-02): Error Boundary de rota — defesa em profundidade documentada em design.md
// §"Investigação do bug do item 2". `website/app/` não tinha `error.tsx`/`global-error.tsx`
// antes desta correção; qualquer exceção não tratada (ex.: o SecurityError de
// history.pushState do bug do slider) derrubava a árvore de componentes e caía no fallback
// genérico do Next.js. Este teste garante que o Error Boundary sempre renderiza uma mensagem
// amigável e um botão de reset, independente da causa do erro.
describe('app/error.tsx (Error Boundary de rota)', () => {
  const originalConsoleError = console.error;

  beforeEach(() => {
    console.error = jest.fn();
  });

  afterEach(() => {
    console.error = originalConsoleError;
  });

  it('renderiza uma mensagem amigável (não o fallback genérico do Next.js)', () => {
    const error = Object.assign(new Error('boom'), { digest: 'abc123' });
    render(<ErrorBoundary error={error} reset={jest.fn()} />);

    expect(screen.getByTestId('app-error')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Algo deu errado' })).toBeInTheDocument();
    expect(
      screen.getByText('Não foi possível carregar esta página. Tente novamente.')
    ).toBeInTheDocument();
  });

  it('botão "Tentar novamente" chama reset()', () => {
    const reset = jest.fn();
    render(<ErrorBoundary error={new Error('boom')} reset={reset} />);

    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(reset).toHaveBeenCalledTimes(1);
  });

  it('registra o erro no console para diagnóstico, sem lançar exceção', () => {
    const error = new Error('boom');

    expect(() => render(<ErrorBoundary error={error} reset={jest.fn()} />)).not.toThrow();
    expect(console.error).toHaveBeenCalledWith(error);
  });
});
