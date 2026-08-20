# Tasks — ISSUE-229: Tag de plataforma nos cards de produto

## T-01 (sub-issue backend, stack:dotnet) — Reexpor `Platform` no contrato público
**Status: CONCLUÍDO** (sub-issue #253, PR #255, mergeado em `desenv`). Nenhuma ação pendente.

---

## T-02 (sub-issue frontend, stack:nodejs) — Exibir tag de plataforma no `DealCard`
**Status: PARCIALMENTE CONCLUÍDO** (sub-issue #254, PR #256, mergeado em `desenv` — porém Code Review do PR #257 reprovou por gap de escopo, ver correção abaixo). Sub-issue #254 **reaberta** para a correção.

### Parte já concluída (não refazer)
- `website/lib/types.ts`: `platform?: string | null;` adicionado ao `interface Deal`.
- `website/components/DealCard.tsx`: tag renderizada com `PLATFORM_LABELS`, primeiro filho de `.deal-card__price`, condicional (`label &&`), sem `href`/`onClick`.
- `website/app/styles/deal-card.css`: classe `.deal-card__platform` criada com os tokens do `ux-ui-spec.md`.
- `website/components/DealCard.test.tsx`: testes cobrindo exibição/ocultação/não-interatividade — 100% cobertura.

### CORREÇÃO PENDENTE — `website/components/DealDetail.tsx` (Critério de Aceite 3 não coberto)
**O que fazer:**
- `website/components/DealDetail.tsx`: aplicar a mesma lógica de tag ao produto principal da página de oferta/detalhe (`app/oferta/[slug]/page.tsx`):
  - Calcular `label` a partir de `deal.platform` usando a **mesma** tabela `PLATFORM_LABELS` já usada em `DealCard.tsx` (valores idênticos — reaproveitar/extrair se prático, ou duplicar a constante local, desde que os 3 valores continuem exatamente `Amazon: 'Amazon'`, `MercadoLivre: 'Mercado Livre'`, `Shopee: 'Shopee'`, para não violar CA 8).
  - Renderizar `<span className="deal-card__platform">{label}</span>` (reaproveitar a classe CSS já existente — não tem nenhuma dependência de `DealCard`) como primeiro filho de `.deal-detail__price`, antes do markup atual (`deal-detail__price-current`, `deal-detail__price-strike`, `deal-detail__badge`), condicional a `label` truthy.
  - Sem `href`/`onClick`/`role`/`tabindex` (CA 7).
- `website/components/DealDetail.test.tsx`: novos testes espelhando `DealCard.test.tsx` — tag exibida com plataforma mapeada (ex. Amazon); tag ausente com `platform: null`/`undefined`; tag ausente com valor não mapeado (ex. `'Aliexpress'`); tag sem atributos interativos (`href`/`onClick`/`tabindex`).
- Rodar `npm test` (suíte completa) e validar manualmente (ou via teste) que a página `/oferta/[slug]` real exibe a tag no produto principal, não só nos relacionados.
- **Não é necessário nenhum ajuste de API/plumbing** — `DealDetail` já recebe `deal: Deal` completo (via `fetchDeal(slug)`), e `Deal.platform` já está tipado desde o PR #256.

**Critérios de aceite (Given/When/Then):**
- Ver `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/criterios-aceite.md`, critério 3 (principal gap), e reconfirmar 4, 5, 7, 8 também no contexto de `DealDetail.tsx`.

**Contexto técnico:**
- docs: `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/especificacao-tecnica.md` (seção "CORREÇÃO pós Code Review")
- design: `openspec/changes/issue-229-exibir-tag-plataforma/design.md` (seção "CORREÇÃO pós Code Review")
- ux-ui: `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/ux-ui-spec.md` (seção 2 — mapeamento; seção 3 — estilo, mesma classe `.deal-card__platform`)
- stack: Next.js 14+ SSR
- repo: DQM-BETA/omuletachou (branch base: `desenv`)
- Motivo da reprovação original: `gh pr view 257 --repo DQM-BETA/omuletachou --json comments` (comentário de Code Review, 2026-08-20)
- Componente a alterar: `website/components/DealDetail.tsx` (NÃO `DealCard.tsx`, que já está correto)
