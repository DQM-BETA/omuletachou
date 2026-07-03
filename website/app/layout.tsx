import type { Metadata } from 'next';

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
      <body>{children}</body>
    </html>
  );
}
