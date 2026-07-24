'use client';

// Client-side: nunca usar API_INTERNAL_URL aqui (server-only) — ver lib/api.ts.
const API_URL = process.env.NEXT_PUBLIC_API_URL;

export interface VapidPublicKeyResponse {
  publicKey: string | null;
}

/**
 * Converte a VAPID public key (base64url) em Uint8Array, formato exigido por
 * `PushManager.subscribe({ applicationServerKey })`. Helper padrão da spec Web Push,
 * sem dependência externa.
 */
export function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');

  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; i += 1) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

/** Detecta suporte a Service Worker + Push API + contexto seguro (HTTPS ou localhost). */
export function isPushSupported(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }
  return (
    'serviceWorker' in navigator &&
    'PushManager' in window &&
    (window.isSecureContext ?? false)
  );
}

export async function subscribeToPush(): Promise<PushSubscription | null> {
  if (!isPushSupported()) {
    return null; // fallback gracioso — sem SW/PushManager/contexto seguro
  }

  const registration = await navigator.serviceWorker.ready;

  const response = await fetch(`${API_URL}/api/public/push/vapid-public-key`);
  const { publicKey }: VapidPublicKeyResponse = await response.json();
  if (!publicKey) {
    return null; // VAPID ainda não cadastrada — não tenta subscribe
  }

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') {
    return null;
  }

  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(publicKey) as BufferSource,
  });

  const { endpoint, keys } = subscription.toJSON();
  await fetch(`${API_URL}/api/public/push/subscribe`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      endpoint,
      keys: { p256dh: keys!.p256dh, auth: keys!.auth },
    }),
  });

  return subscription;
}

export async function unsubscribeFromPush(): Promise<void> {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) {
    return;
  }
  const registration = await navigator.serviceWorker.getRegistration();
  const subscription = await registration?.pushManager.getSubscription();
  if (!subscription) {
    return;
  }

  const endpoint = subscription.endpoint;
  await subscription.unsubscribe();
  await fetch(
    `${API_URL}/api/public/push/unsubscribe?endpoint=${encodeURIComponent(endpoint)}`,
    { method: 'DELETE' }
  );
}
