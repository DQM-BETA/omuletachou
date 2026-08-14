/**
 * Descobre uma categoria e um slug de oferta reais a partir do sitemap.xml do site
 * (sem hardcode — catálogo vem de scraping real, sem seed fixo). As URLs do sitemap usam
 * um domínio absoluto fixo (`SITE_URL` em `app/sitemap.ts`), não `baseURL`; por isso a
 * extração é feita por regex no path, não por match de domínio.
 */
export async function getRealCategoriaAndSlug(
  baseURL: string
): Promise<{ categoria?: string; slug?: string }> {
  const res = await fetch(`${baseURL}/sitemap.xml`);
  const xml = await res.text();

  const categoria = xml.match(/\/categoria\/([^<]+)</)?.[1];
  const slug = xml.match(/\/oferta\/([^<]+)</)?.[1];

  return { categoria, slug };
}
