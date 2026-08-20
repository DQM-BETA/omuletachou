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

- **Tipo:** `string?` — nome do enum `Platform` (`Amazon` | `MercadoLivre` | `Shopee`), serializado via `product.Platform.ToString()` (mesmo padrão do DTO interno em `ProductDtos.cs`/`ProductsController.cs`). **Não traduzir para texto amigável no backend** — a tradução é do frontend.
- **Não é uma nova rota nem novo endpoint** — é a adição de 1 campo em respostas já existentes.
- **Status: CONCLUÍDO** (sub-issue #253, PR #255, mergeado em `desenv`). Nenhuma alteração adicional de backend necessária.

### Arquivos backend afetados (já implementados)
- `backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs`: `public string? Platform { get; init; }`, `Platform = product.Platform.ToString()` em `FromProduct`.
- `backend/src/AfiliadoBot.Tests/Public/PublicControllerTests.cs`: teste `GetDeals_JsonDeResposta_NuncaContemCampoPlatform` substituído por `GetDeals_JsonDeResposta_ContemCampoPlatformComValorCorreto`.

## Frontend — `website/` (Next.js)

### CORREÇÃO (pós Code Review do PR #257, 2026-08-20) — dois componentes, não um
A versão original desta spec afirmava que `DealCard.tsx` é "o único componente de card de produto, reutilizado... na página de oferta/detalhe (mesma renderização via `DealCard`)". **Isso está incorreto.** A página de oferta (`website/app/oferta/[slug]/page.tsx`) renderiza o **produto principal** via `website/components/DealDetail.tsx`, um componente separado com markup próprio de preço (`deal-detail__price*`). `DealCard` só aparece ali na seção "Mais ofertas" (produtos relacionados) — não no produto que o visitante está de fato visualizando. Isso deixou o Critério de Aceite 3 sem cobertura no PR #256/#257.

**Estado atual (após #256, já mergeado):**
- `website/lib/types.ts` → `Deal.platform?: string | null` — **concluído**, usado por qualquer componente que receba `Deal`.
- `website/components/DealCard.tsx` → renderiza `<span className="deal-card__platform">` com `PLATFORM_LABELS`, primeiro filho de `.deal-card__price` — **concluído**. Cobre home (CA1), categoria (CA2) e a grade de relacionados dentro da página de oferta.
- `website/app/styles/deal-card.css` → classe `.deal-card__platform` já existe com os tokens definidos pelo UX/UI — **concluído**.

**Pendente (escopo da correção, sub-issue #254 reaberta):**
- `website/components/DealDetail.tsx` → aplicar a mesma lógica ao produto principal da página de oferta:
  1. Calcular `label` a partir de `deal.platform` usando a mesma tabela `PLATFORM_LABELS` (mesmos 3 valores da seção 2 do `ux-ui-spec.md`; reaproveitar de `DealCard.tsx` se for prático extrair para um módulo compartilhado, ou duplicar a constante — decisão de implementação do Dev, desde que os valores sejam idênticos, para não violar CA 8).
  2. Renderizar `<span className="deal-card__platform">{label}</span>` (reaproveitar a classe já existente — ela não tem nenhuma dependência de `DealCard`) condicionalmente (`label &&`), posicionada como primeiro filho de `.deal-detail__price` (mesmo padrão de "acima da linha de preço" já usado no `DealCard`), **antes** do markup atual (`deal-detail__price-current`, `deal-detail__price-strike`, `deal-detail__badge`).
  3. Nenhum `href`/`onClick`/`role`/`tabindex` (CA 7, igual ao `DealCard`).
- **Não é necessário nenhum ajuste de dados/API/plumbing** — `DealDetail` já recebe `deal: Deal` completo (via `fetchDeal(slug)` em `page.tsx`), e `Deal.platform` já está no tipo desde o PR #256. O gap é puramente de renderização faltante no componente.
- `website/components/DealDetail.test.tsx` → novos testes espelhando os já existentes em `DealCard.test.tsx`: tag exibida com plataforma mapeada (cobrir ao menos 1 valor, ex. Amazon); tag ausente com `platform: null`/`undefined`; tag ausente com valor não mapeado; tag sem atributos interativos.

### Critérios de aceite → cobertura técnica (atualizado)
| CA | Onde é resolvido | Status |
|---|---|---|
| 1 (exibição home) | `DealCard.tsx` | Concluído (PR #256) |
| 2 (exibição categoria) | `DealCard.tsx` | Concluído (PR #256) |
| 3 (exibição oferta/detalhe) | `DealDetail.tsx` (produto principal) | **Pendente — gap desta correção** |
| 4 (sem plataforma → oculta) | condicional em `DealCard.tsx` e `DealDetail.tsx` | Concluído em `DealCard`; pendente em `DealDetail` |
| 5 (valor não mapeado → oculta) | `PLATFORM_LABELS[...]` ausente → não renderiza, em ambos componentes | Concluído em `DealCard`; pendente em `DealDetail` |
| 6 (mobile legível) | CSS com tokens já existentes, reaproveitado | Concluído em `DealCard`; validar também em `DealDetail` |
| 7 (não interativo) | `<span>` sem handler, em ambos componentes | Concluído em `DealCard`; pendente em `DealDetail` |
| 8 (texto consistente entre telas) | mesma `PLATFORM_LABELS` usada por `DealCard` e `DealDetail` | Depende da correção usar os mesmos valores |

## Dependência entre sub-issues
Ambas as sub-issues de backend (#253) e a primeira rodada de frontend (#254, `DealCard.tsx`) já estão mergeadas em `desenv`. A correção do `DealDetail.tsx` (mesma sub-issue #254, reaberta) não tem nenhuma dependência de backend adicional — o campo `platform` já está disponível na resposta de `GET /api/public/deals/{slug}`, consumida por `fetchDeal()`.
