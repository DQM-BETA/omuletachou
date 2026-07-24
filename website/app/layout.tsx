import type { Metadata } from 'next';
import PushSubscriptionManager from '@/components/PushSubscriptionManager';

export const metadata: Metadata = {
  title: 'O Mulet Achou',
  description: 'As melhores ofertas do dia, selecionadas pelo Mulet',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="pt-BR">
      <head>
        <link rel="manifest" href="/manifest.json" />
        <meta name="theme-color" content="#e63946" />
      </head>
      <body>
        {children}
        <PushSubscriptionManager />
      </body>
    </html>
  );
}
