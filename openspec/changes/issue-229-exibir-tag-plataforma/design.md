# Design (resumido) — ISSUE-229: Tag de plataforma nos cards de produto

> PM roteou sem Arquiteto (sem ambiguidade arquitetural). Design resumido pelo LT.

## Visão geral da solução
Mudança em 2 pontas do monorepo `omuletachou`, coordenadas mas tecnicamente independentes:

1. **Backend (`AfiliadoBot.Api`, stack:dotnet)** — o campo `Platform` foi **removido do contrato público** (`PublicDealDto`) na Issue #167 (CA 5.1/5.2/5.3), por higiene, quando a distinção de plataforma deixou de ser usada como filtro/navegação. A Issue #229 precisa reintroduzi-lo, agora **apenas como dado de exibição** (texto informativo, não filtro) — o Gerente já confirmou no Gate 1 que isso não conflita com a decisão da #167 (sinalização visual ≠ filtro/navegação).
2. **Frontend (`website/`, Next.js, stack:nodejs)** — o componente `DealCard.tsx` (compartilhado por home, categoria e oferta — confirmado: é o único componente de card usado nas 3 telas via `app/page.tsx` e `app/categoria/[categoria]/page.tsx`) passa a renderizar a tag de texto da plataforma, próxima ao preço, com o mapeamento enum→texto e o estilo (cor/tipografia/badge) definidos pelo UX/UI a partir do design system do Figma.

## Componentes/telas envolvidos
- `website/components/DealCard.tsx` — componente único reutilizado em `app/page.tsx` (home), `app/categoria/[categoria]/page.tsx` (categoria) e na página de oferta/detalhe (mesma renderização de card). Não há componente de card duplicado — 1 sub-issue de frontend cobre as 3 telas.
- `website/lib/types.ts` — `Deal` precisa de um novo campo opcional `platform` (ou nome equivalente já alinhado ao JSON do backend).
- `website/app/styles/deal-card.css` — novo bloco de classe para a tag, usando as CSS vars de design token já existentes no arquivo (`--color-*`, `--font-size-*`, `--space-*`, `--radius-*`), consistente com `.deal-card__badge`.
- `backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs` — reintroduzir `Platform` (como string) e o mapeamento em `FromProduct`.
- `backend/src/AfiliadoBot.Tests/Public/PublicControllerTests.cs` — o teste `GetDeals_JsonDeResposta_NuncaContemCampoPlatform` (linha ~121) fica desatualizado com a mudança e precisa ser removido/reescrito para refletir a nova decisão (documentar isso explicitamente na sub-issue de backend para o dev não interpretar como regressão acidental).

## Stack
- Backend: ASP.NET Core 8.0 / C# (`stack:dotnet`)
- Frontend: Next.js 14+ SSR (`stack:nodejs`)

## Fluxo de dados
1. `Product.Platform` (enum `Amazon | MercadoLivre | Shopee`, já existe na entidade — nunca foi removido do domínio, só do DTO público) é serializado em `PublicDealDto.Platform` como **string do nome do enum** (`"Amazon"`, `"MercadoLivre"`, `"Shopee"`), igual ao padrão já usado no DTO interno (`ProductsController`: `product.Platform.ToString()`). Backend não traduz para texto de exibição — mantém o contrato como dado bruto/estável, a tradução para texto amigável é responsabilidade do frontend (evita redeploy de backend só para ajustar copy).
2. `website/lib/types.ts` → `Deal.platform?: string | null`.
3. `DealCard.tsx` mapeia o valor via uma tabela `PLATFORM_LABELS` (a ser definida com o UX/UI: ex. `{ Amazon: 'Amazon', MercadoLivre: 'Mercado Livre', Shopee: 'Shopee' }` ou abreviações) — se `platform` for `null`/`undefined`/ausente do mapeamento, a tag não é renderizada (CA 4 e CA 5 dos critérios de aceite).
4. Nenhuma nova rota de API, nenhum novo endpoint — mudança de contrato (campo a mais) num endpoint já existente (`GET /api/public/deals` e `GET /api/public/deals/{slug}`).

## Decisão explícita: reversão parcial da Issue #167
Documentar no PR/commit que isto reintroduz `Platform` no contrato público — decisão já validada pelo Gerente no Gate 1 da #229 (ver comentário https://github.com/DQM-BETA/omuletachou/issues/229#issuecomment-5357600715). Não é regressão: a #167 removeu a plataforma como **mecanismo de filtro/navegação**; a #229 a reintroduz **apenas como texto informativo não interativo** (CA 7).
