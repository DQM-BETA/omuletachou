# Tasks — ISSUE-229: Tag de plataforma nos cards de produto

## T-01 (sub-issue backend, stack:dotnet) — Reexpor `Platform` no contrato público
**O que fazer:**
- `backend/src/AfiliadoBot.Api/Public/PublicDealDto.cs`: adicionar `public string? Platform { get; init; }`; em `FromProduct`, setar `Platform = product.Platform.ToString()`; atualizar o comentário de cabeçalho da classe (hoje afirma que `Platform` foi removido — precisa refletir a reversão parcial autorizada pelo Gate 1 da #229).
- `backend/src/AfiliadoBot.Tests/Public/PublicControllerTests.cs`: remover/reescrever o teste `GetDeals_JsonDeResposta_NuncaContemCampoPlatform` (assume ausência do campo — comportamento esperado muda). Adicionar teste cobrindo que o JSON de resposta **contém** `platform` com o valor correto do enum.
- Rodar toda a suíte de testes do backend após a mudança (`dotnet test`) — garantir que nenhum outro teste dependia da ausência do campo.

**Critérios de aceite (Given/When/Then):**
- Given um produto com `Platform = MercadoLivre` no banco, When `GET /api/public/deals` ou `GET /api/public/deals/{slug}` é chamado, Then a resposta JSON contém `"platform": "MercadoLivre"`.
- Given a suíte de testes do backend, When executada após a mudança, Then nenhum teste antigo que assumia a ausência do campo permanece quebrado/inconsistente (removido ou atualizado deliberadamente).
- Given o DTO interno usado pelo dashboard (`ProductDtos.cs`), When este DTO é revisado, Then permanece inalterado (fora de escopo).

**Contexto técnico:**
- docs: `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/especificacao-tecnica.md`
- design: `openspec/changes/issue-229-exibir-tag-plataforma/design.md`
- stack: ASP.NET Core 8.0 / C#
- repo: DQM-BETA/omuletachou (branch base: `desenv`)
- Enum de referência: `backend/src/AfiliadoBot.Domain/Enums/Platform.cs` (`Amazon | MercadoLivre | Shopee`)

---

## T-02 (sub-issue frontend, stack:nodejs) — Exibir tag de plataforma no `DealCard`
**O que fazer:**
- `website/lib/types.ts`: adicionar `platform?: string | null;` ao `interface Deal` (atualizar/remover o comentário que documenta a remoção pela #167).
- `website/components/DealCard.tsx`: renderizar a tag de texto da plataforma próxima ao bloco de preço (`.deal-card__price`), usando uma tabela de mapeamento enum→texto de exibição (coordenar texto exato/estilo com o UX/UI — consultar Figma). Se `deal.platform` ausente/`null`/valor não mapeado → não renderizar nada (sem placeholder).
- `website/app/styles/deal-card.css`: nova classe para a tag usando os design tokens já existentes no arquivo (não competir visualmente com preço/CTA — evitar `--color-primary`). Validar legibilidade em viewport mobile.
- `website/components/DealCard.test.tsx`: novos testes — tag exibida com plataforma mapeada; tag ausente com `platform: null`; tag ausente com valor não mapeado (ex. string desconhecida); tag não é elemento clicável/interativo (sem `href`/`role=link`/`onClick`).

**Critérios de aceite (Given/When/Then):** ver `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/criterios-aceite.md` critérios 1-8 (cobertura completa: home/categoria/oferta reutilizam o mesmo `DealCard`, então os critérios 1-3 são resolvidos pela mesma implementação).

**Contexto técnico:**
- docs: `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/especificacao-tecnica.md`
- design: `openspec/changes/issue-229-exibir-tag-plataforma/design.md`
- Texto exato/estilo da tag: aguardar output do UX/UI (Figma design system) antes de finalizar a UI — consumir o resultado do UX/UI como input desta sub-issue.
- stack: Next.js 14+ SSR
- repo: DQM-BETA/omuletachou (branch base: `desenv`)
- Componente único confirmado usado em `app/page.tsx`, `app/categoria/[categoria]/page.tsx` e página de oferta — sem duplicação de card.

**Dependência:** integração completa (campo `platform` de fato vindo da API) depende de T-01 mergeado; desenvolvimento/testes unitários podem seguir em paralelo com mocks (`buildDeal` em `DealCard.test.tsx`).
