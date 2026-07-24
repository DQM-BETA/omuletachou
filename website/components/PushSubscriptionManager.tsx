'use client';

import { useEffect } from 'react';
import { isPushSupported, subscribeToPush } from '@/lib/push';

/**
 * Componente montado no root (`app/layout.tsx`) que dispara a subscription de push
 * assim que o Service Worker estiver pronto. Fallback gracioso: em browsers sem
 * suporte a Service Worker/PushManager ou fora de contexto seguro, não faz nada e
 * não renderiza nenhum elemento visível — a página segue funcionando normalmente
 * (CA "sem HTTPS fora de localhost").
 */
export default function PushSubscriptionManager(): null {
  useEffect(() => {
    if (!isPushSupported()) {
      return;
    }

    subscribeToPush().catch(() => {
      // Fire-and-forget: falha na subscription não deve quebrar a navegação do usuário.
    });
  }, []);

  return null;
}
