import { render, screen } from '@testing-library/react';
import Header from './Header';

describe('Header', () => {
  it('renderiza o brand e os filtros de plataforma', () => {
    render(<Header />);

    expect(screen.getByText('O Mulet Achou')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Todas' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('link', { name: 'Amazon' })).toHaveAttribute(
      'href',
      '/?platform=Amazon'
    );
    expect(screen.getByRole('link', { name: 'Mercado Livre' })).toHaveAttribute(
      'href',
      '/?platform=MercadoLivre'
    );
    expect(screen.getByRole('link', { name: 'Shopee' })).toHaveAttribute(
      'href',
      '/?platform=Shopee'
    );
  });

  it('CA-A5: marca a plataforma ativa via aria-current', () => {
    render(<Header activePlatform="Amazon" />);

    expect(screen.getByRole('link', { name: 'Amazon' })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('link', { name: 'Todas' })).not.toHaveAttribute('aria-current');
  });
});
