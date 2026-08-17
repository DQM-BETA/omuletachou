import { render, screen } from '@testing-library/react';
import Header from './Header';

describe('Header', () => {
  it('renderiza o brand/logo', () => {
    render(<Header />);

    expect(screen.getByRole('link', { name: 'O Mulet Achou' })).toHaveAttribute('href', '/');
  });

  it('Issue #167 (CA 7.4): não renderiza nenhum chip/filtro de plataforma (Amazon/MercadoLivre/Shopee)', () => {
    render(<Header />);

    expect(screen.queryByText('Amazon')).not.toBeInTheDocument();
    expect(screen.queryByText('Mercado Livre')).not.toBeInTheDocument();
    expect(screen.queryByText('Shopee')).not.toBeInTheDocument();
    expect(screen.queryByText('Todas')).not.toBeInTheDocument();
  });
});
