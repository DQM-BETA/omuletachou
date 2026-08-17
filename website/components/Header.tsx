import Link from 'next/link';

// Issue #167 (CA 7.4): chips de filtro por plataforma (Amazon/MercadoLivre/Shopee) removidos —
// era aqui, não em badge de card, que a distinção de plataforma aparecia no site (achado do
// Arquiteto). O Header volta a ser só marca/logo; o filtro por categoria/subcategoria/preço/
// desconto agora vive no `FilterBar` (Home).
export default function Header() {
  return (
    <header className="site-header">
      <Link href="/" className="site-header__brand">
        O Mulet Achou
      </Link>
    </header>
  );
}
