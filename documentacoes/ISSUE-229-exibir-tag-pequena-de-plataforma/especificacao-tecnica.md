# Especificação Técnica — ISSUE-229: Tag de plataforma nos cards de produto

## Contrato de API — `PublicDealDto` (alteração)

`GET /api/public/deals` (paginado) e `GET /api/public/deals/{slug}` passam a retornar um novo campo `platform`:

```json
{
  "title": "Fone Bluetooth XYZ",
  "salePrice": 99.9,
  "originalPrice": 149.9,
  "discountPct": 33,
  "affiliateLink": "https://...",
  "mediaUrl": "https://...",
  "mediaLocalPath": null,
  "slug": "fone-bluetooth-xyz",
  "category": "eletronicos",
  "subcategory": null,
  "collectedAt": "2026-07-01T12:00:00Z",
  "platform": "MercadoLivre"
}
```

- **Tipo:** `string?` — nome do enum `Platform` (`Amazon` | `MercadoLivre` | `Shopee`), serializado via `product.Platform.ToString()` (mesmo padrão do DTO interno em `ProductDtos.cs`/`ProductsController.cs`). **Não traduzir para texto amigável no backend** — a tradução é do frontend (UX/UI define o texto de exibição sem exigir deploy de backend).
- Se o produto não tiver plataforma identificável (hoje `Platform` é enum não-nullable na entidade — na prática todo produto tem um valor; o "sem plataforma identificada" do PRD cobre principalmente o caso de **valor não mapeado no frontend**, não ausência real no backend). Ainda assim, o campo é `string?` no DTO por segurança de contrato (não quebra se o domínio mudar no futuro).
- **Não é uma nova rota nem novo endpoint** — é a adição de 1 campo em respostas já existentes.

### Arquivos backend afetados
- `backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs`: adicionar `public string? Platform { get; init; }` e em `FromProduct`: `Platform = product.Platform.ToString()`. Atualizar o comentário de cabeçalho da classe (hoje documenta explicitamente a ausência do campo — precisa refletir a reversão parcial da #167, citando o Gate 1 da #229).
- `backend/src/AfiliadoBot.Tests/Public/PublicControllerTests.cs`: o teste `GetDeals_JsonDeResposta_NuncaContemCampoPlatform` (~linha 121-130) **assume a ausência do campo** e vai quebrar — deve ser removido ou reescrito para validar o novo comportamento (ex.: `json.Should().Contain("platform")` + valor correto). Isto é esperado, não uma regressão a investigar.
- Nenhuma migration necessária — `Product.Platform` já existe na entidade/banco (nunca foi removido, só a exposição pública).

## Frontend — `website/` (Next.js)

### Componente único confirmado
`website/components/DealCard.tsx` é o **único** componente de card de produto, reutilizado em:
- `website/app/page.tsx` (home)
- `website/app/categoria/[categoria]/page.tsx` (categoria)
- página de oferta/detalhe (mesma renderização via `DealCard`)

→ **1 sub-issue de frontend cobre as 3 telas** (mudança centralizada no componente).

### Alterações
1. `website/lib/types.ts` — adicionar ao `interface Deal`: `platform?: string | null;` (documentar que é o valor bruto do enum vindo do backend, reintroduzido pela #229 — ver comentário existente sobre a remoção na #167, deve ser atualizado/removido).
2. `website/components/DealCard.tsx` — renderizar a tag próxima ao bloco `.deal-card__price` (ex.: dentro ou logo após a `<div className="deal-card__price">`), condicionada a `deal.platform` estar presente **e** mapeado numa tabela de labels conhecida. Se `deal.platform` for `null`/`undefined`/valor fora da tabela → não renderizar nada (sem placeholder, sem espaço reservado).
3. Tabela de mapeamento enum → texto de exibição (`PLATFORM_LABELS` ou nome equivalente) — **texto exato e estilo (cor/tipografia/badge vs. texto solto) a definir pelo UX/UI** consultando o Figma. Sugestão de estrutura (o UX/UI decide os valores, não o LT):
   ```ts
   const PLATFORM_LABELS: Record<string, string> = {
     Amazon: 'Amazon',
     MercadoLivre: 'Mercado Livre',
     Shopee: 'Shopee',
   };
   ```
4. `website/app/styles/deal-card.css` — nova classe (ex. `.deal-card__platform`) usando os design tokens já em uso no arquivo (`--color-neutral-*`, `--font-size-xs`, `--space-*`, `--radius-sm`) — seguir o padrão discreto (não usar `--color-primary` que já é usado no preço/CTA, para não competir visualmente). Testar em viewport mobile (cards compactos) — não pode cortar/sobrepor.
5. `website/components/DealCard.test.tsx` — novos testes cobrindo os critérios de aceite 1, 4, 5 e 7 (exibe tag com plataforma mapeada; oculta com `platform: null`; oculta com valor não mapeado ex. `platform: 'Aliexpress'`; tag não é link/botão, não possui `href`/`onClick`).

### Critérios de aceite → cobertura técnica
| CA | Onde é resolvido |
|---|---|
| 1, 2, 3 (exibição home/categoria/oferta) | `DealCard.tsx` único, reutilizado nas 3 rotas |
| 4 (sem plataforma → oculta, sem quebra de layout) | condicional em `DealCard.tsx`; sem elemento placeholder |
| 5 (valor não mapeado → oculta, sem vazar enum cru) | `PLATFORM_LABELS[deal.platform]` ausente → não renderiza |
| 6 (mobile legível) | CSS com tokens responsivos existentes; validar em viewport estreito |
| 7 (não interativo/não filtro) | tag é `<span>` sem handler de clique/navegação |
| 8 (texto consistente entre telas) | fonte única (`DealCard.tsx` + `PLATFORM_LABELS`), sem duplicação de lógica |

## Dependência entre sub-issues
A sub-issue de frontend depende do campo `platform` existir na resposta da API para teste de integração/e2e completo, mas o desenvolvimento pode ocorrer em paralelo (frontend usa mock/fixture de `Deal` com `platform` nos testes unitários, igual ao padrão já usado em `DealCard.test.tsx::buildDeal`). A integração real (API retornando o campo) só é validada quando ambos os PRs estiverem mergeados em `desenv`.
