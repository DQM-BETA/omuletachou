'use client';

// Client-side: nunca usar API_INTERNAL_URL aqui (server-only) — ver lib/api.ts.
const API_URL = process.env.NEXT_PUBLIC_API_URL;

/**
 * Registra o clique em um produto (Issue #231, sub-issue #279). Fire-and-forget: nunca bloqueia
 * nem atrasa a navegação do usuário (CA 2.4) — usa `navigator.sendBeacon` (não aguarda resposta,
 * sobrevive à troca de página) com fallback `fetch(..., { keepalive: true })` para browsers sem
 * suporte a `sendBeacon`. Falhas são silenciadas por design: o endpoint é apenas telemetria, o
 * usuário nunca deve perceber um erro de rede aqui.
 */
export function trackProductClick(productId: string): void {
  const url = `${API_URL}/api/public/products/${productId}/click`;

  if (typeof navigator !== 'undefined' && 'sendBeacon' in navigator) {
    navigator.sendBeacon(url);
    return;
  }

  fetch(url, { method: 'POST', keepalive: true }).catch(() => {
    /* silenciado por design — CA 2.4, falha não deve ser percebida pelo usuário */
  });
}
