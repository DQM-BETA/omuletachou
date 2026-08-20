# Design (resumido) — ISSUE-229: Tag de plataforma nos cards de produto

> PM roteou sem Arquiteto (sem ambiguidade arquitetural). Design resumido pelo LT.

## Visão geral da solução
Mudança em 2 pontas do monorepo `omuletachou`, coordenadas mas tecnicamente independentes:

1. **Backend (`AfiliadoBot.Api`, stack:dotnet)** — o campo `Platform` foi **removido do contrato público** (`PublicDealDto`) na Issue #167 (CA 5.1/5.2/5.3), por higiene, quando a distinção de plataforma deixou de ser usada como filtro/navegação. A Issue #229 precisa reintroduzi-lo, agora **apenas como dado de exibição** (texto informativo, não filtro) — o Gerente já confirmou no Gate 1 que isso não conflita com a decisão da #167 (sinalização visual ≠ filtro/navegação).
2. **Frontend (`website/`, Next.js, stack:nodejs)** — a tag de texto da plataforma, próxima ao preço, com o mapeamento enum→texto e o estilo definidos pelo UX/UI, precisa ser renderizada em **dois componentes distintos** (ver correção abaixo), não apenas um.

## CORREÇÃO (pós Code Review do PR #257, 2026-08-20) — premissa de componente único estava errada
A afirmação original deste documento — de que `DealCard.tsx` é "reutilizado... na página de oferta/detalhe (mesma renderização via `DealCard`)" — **está incorreta** e foi a causa da reprovação do Code Review no PR #257 (CA 3 não implementado).

**Como é de fato:**
- `website/app/oferta/[slug]/page.tsx` (página de oferta/detalhe) renderiza o produto principal via `website/components/DealDetail.tsx` — um componente **separado**, com markup próprio de preço (`deal-detail__price`, `deal-detail__price-current`, `deal-detail__price-strike`, `deal-detail__badge`).
- `DealCard.tsx` só é usado **dentro** de `DealDetail.tsx`, na seção "Mais ofertas" (`relatedDeals`) — ou seja, para produtos relacionados, não para o produto que a página está exibindo.
- `DealDetail` já recebe o objeto `deal: Deal` completo como prop (vindo de `fetchDeal(slug)` em `page.tsx`, que consome a mesma API pública já corrigida pela sub-issue #253) — **o campo `platform` já chega até `DealDetail` sem nenhum ajuste de plumbing/API necessário.** O único gap é que `DealDetail.tsx` nunca foi tocado para renderizar a tag.

**Correção de escopo:** a sub-issue #254 (T-02) precisa aplicar a mesma lógica de tag (mesmo `PLATFORM_LABELS`, mesma regra de ocultação, não interativa) também em `DealDetail.tsx`, posicionada de forma equivalente ao `DealCard` (próxima ao bloco `.deal-detail__price`, ex. como primeiro filho, antes do markup de preço atual). Reaproveitar a mesma tabela de mapeamento (extrair para local compartilhado se fizer sentido, ou duplicar constante pequena — decisão de implementação do Dev) e, se prático, a mesma classe CSS `.deal-card__platform` (ela já não depende de nada específico do `DealCard`) para manter CA 8 (consistência de texto/estilo entre telas).

## Componentes/telas envolvidos (atualizado)
- `website/components/DealCard.tsx` — componente de card usado em `app/page.tsx` (home), `app/categoria/[categoria]/page.tsx` (categoria) e, dentro de `DealDetail.tsx`, na grade de produtos relacionados da página de oferta. **Já corrigido no PR #256** (aprovado pelo Code Review, não precisa de nova alteração).
- `website/components/DealDetail.tsx` — componente que renderiza o produto principal da página de oferta/detalhe (`app/oferta/[slug]/page.tsx`). **Ainda não recebeu a tag de plataforma — é o gap a corrigir.**
- `website/lib/types.ts` — `Deal.platform?: string | null` (já adicionado no PR #256, usado por ambos os componentes).
- `website/app/styles/deal-card.css` — classe `.deal-card__platform` já existe (PR #256); reaproveitar ou criar equivalente `deal-detail__platform` com os mesmos tokens, a critério do Dev.
- `backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs` — já reintroduzido `Platform` (PR #255, sub-issue #253, mergeado). Nenhuma alteração adicional necessária aqui.

## Stack
- Backend: ASP.NET Core 8.0 / C# (`stack:dotnet`) — concluído, sub-issue #253.
- Frontend: Next.js 14+ SSR (`stack:nodejs`) — sub-issue #254 reaberta para cobrir `DealDetail.tsx`.

## Fluxo de dados
1. `Product.Platform` (enum `Amazon | MercadoLivre | Shopee`) é serializado em `PublicDealDto.Platform` como string do nome do enum. **Concluído** (PR #255).
2. `website/lib/types.ts` → `Deal.platform?: string | null`. **Concluído** (PR #256).
3. `DealCard.tsx` mapeia o valor via `PLATFORM_LABELS` — **concluído** (PR #256), cobre home, categoria e a grade de relacionados dentro da página de oferta.
4. `DealDetail.tsx` **precisa do mesmo mapeamento/renderização condicional**, aplicado ao produto principal exibido na página de oferta — **pendente**, escopo da correção desta rodada.
5. Nenhuma nova rota de API, nenhum novo endpoint.

## Decisão explícita: reversão parcial da Issue #167
Documentar no PR/commit que isto reintroduz `Platform` no contrato público — decisão já validada pelo Gerente no Gate 1 da #229 (ver comentário https://github.com/DQM-BETA/omuletachou/issues/229#issuecomment-5357600715). Não é regressão: a #167 removeu a plataforma como **mecanismo de filtro/navegação**; a #229 a reintroduz **apenas como texto informativo não interativo** (CA 7).
