import Link from 'next/link';

const PLATFORMS: { label: string; value: string | undefined }[] = [
  { label: 'Todas', value: undefined },
  { label: 'Amazon', value: 'Amazon' },
  { label: 'Mercado Livre', value: 'MercadoLivre' },
  { label: 'Shopee', value: 'Shopee' },
];

interface HeaderProps {
  activePlatform?: string;
}

export default function Header({ activePlatform }: HeaderProps) {
  return (
    <header className="site-header">
      <Link href="/" className="site-header__brand">
        O Mulet Achou
      </Link>

      <nav className="site-header__filters" aria-label="Filtro de plataforma">
        {PLATFORMS.map((platform) => {
          const isActive = activePlatform === platform.value;
          const href = platform.value ? `/?platform=${platform.value}` : '/';

          return (
            <Link
              key={platform.label}
              href={href}
              className={`site-header__chip${isActive ? ' site-header__chip--active' : ''}`}
              aria-current={isActive ? 'true' : undefined}
            >
              {platform.label}
            </Link>
          );
        })}
      </nav>
    </header>
  );
}
