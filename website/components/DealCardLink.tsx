'use client';

import { trackProductClick } from '@/lib/tracking';

interface DealCardLinkProps {
  productId: string;
  href: string;
  className: string;
}

/**
 * CTA do `DealCard` extraído como Client Component (Issue #231, sub-issue #279) — necessário
 * porque `sendBeacon`/`onClick` exigem boundary de client, mas `DealCard` continua Server
 * Component. Registra o clique via `trackProductClick` sem alterar destino/atributos do link
 * (mesmo `href`/`target`/`rel` de sempre) — a navegação nunca é bloqueada ou atrasada pelo
 * tracking (CA 2.1/2.2/2.4).
 */
export default function DealCardLink({ productId, href, className }: DealCardLinkProps) {
  return (
    <a
      className={className}
      href={href}
      target="_blank"
      rel="nofollow"
      onClick={() => trackProductClick(productId)}
    >
      Ver oferta →
    </a>
  );
}
