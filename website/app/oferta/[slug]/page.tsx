interface Props {
  params: { slug: string };
}

export default function OfertaPage({ params }: Props) {
  return (
    <main>
      <h1>Oferta: {params.slug}</h1>
      <p>Detalhes do produto em breve.</p>
    </main>
  );
}
