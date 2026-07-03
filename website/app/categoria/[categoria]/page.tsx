interface Props {
  params: { categoria: string };
}

export default function CategoriaPage({ params }: Props) {
  return (
    <main>
      <h1>Categoria: {params.categoria}</h1>
      <p>Ofertas desta categoria em breve.</p>
    </main>
  );
}
