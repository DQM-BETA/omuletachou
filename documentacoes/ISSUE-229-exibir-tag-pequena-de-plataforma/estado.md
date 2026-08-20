---
issue: 229
titulo: 'feat: exibir tag pequena de plataforma de origem nos cards de produto do site público'
etapa_atual: Em Desenvolvimento
ultimo_agente: lider-tecnico
openspec_change: repos/omuletachou/openspec/changes/issue-229-exibir-tag-plataforma
tech_stacks: [dotnet, nextjs]
repos:
  omuletachou: já existente
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma
openspec_path: repos/omuletachou/openspec/changes/issue-229-exibir-tag-plataforma
sub_issues: ['#253 (stack:dotnet, task_id:T-01)', '#254 (stack:nodejs, task_id:T-02)']
desenv_tasks_merged: ['#253']
sub_issues_frontend: {'#254': 'REABERTA — correção pendente em DealDetail.tsx (gap CA3); PR #256 anterior (DealCard.tsx) já mergeado e aprovado'}
pr_homologacao: 257
pr_release: ~
code_review_homolog_pr: REPROVADO (2026-08-20) — ver notas
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~
rota: normal

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação | Coordenador | haiku | — | — | — |
| 2 | PM Fase 1 (levantamento) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 3 | PM Fase 2 (PRD) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 4 | Refinamento Técnico (LT) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |
| 5 | Code Review PR #257 (reprovado) | Code Review | sonnet-5 | ~ | ~ | ~ |
| 6 | Mapeamento de falha (LT) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |

## Notas
- **Rota:** promovida de `backlog` para `normal` pelo Gerente no Gate 1 (2026-08-20) — segue o pipeline completo a partir daqui.
- **Gate 1 respondido (2026-08-20):** confirmado sem conflito com Issue #167 (sinalização visual, não filtro); posição próxima ao preço, discreta; formato texto (não ícone); aparece em todas as telas (home/categoria/oferta). Comentário: https://github.com/DQM-BETA/omuletachou/issues/229#issuecomment-5357600715
- **PRD (Fase 2) concluído (2026-08-20):** `proposal.md` e `criterios-aceite.md` escritos cobrindo exibição em todas as telas, tratamento de produto sem plataforma identificada/valor não mapeado (tag oculta, sem erro), e legibilidade em mobile.
- **Avaliação de ambiguidade arquitetural:** sem ambiguidade — mudança de exibição em componente já existente do `website/` (Next.js), dado de plataforma já existe no domínio do produto. Única pendência técnica (se o campo já está exposto na API pública) é detalhe de implementação, não decisão de arquitetura. Segue direto para o **Líder Técnico** (com apoio do UX/UI para texto/estilo da tag).
- **Refinamento Técnico concluído (2026-08-20):** investigação **incorretamente** concluiu que o card de produto do site público é um único componente compartilhado (`website/components/DealCard.tsx`), reutilizado nas 3 telas (home, categoria, oferta) — ver correção abaixo, esta premissa estava errada para a tela de oferta.
- **Achado importante (diverge da hipótese do proposal.md):** o campo `Platform` **NÃO estava exposto** na API pública — foi **explicitamente removido** de `PublicDealDto` na Issue #167. A #229 precisou reintroduzi-lo — reversão parcial e intencional, já validada pelo Gerente no Gate 1. Isso tornou o escopo full-stack (backend + frontend).
- **Task breakdown: 2 sub-issues** — `#253` (`stack:dotnet`, T-01, backend) e `#254` (`stack:nodejs`, T-02, frontend).
- **UX/UI concluído (2026-08-20):** `ux-ui-spec.md` escrito. Decisões: tag de texto neutra, nome completo, posicionada em linha própria acima do bloco de preço, oculta quando `platform` ausente/não mapeado, não interativa.
- **Dev #253 concluído (2026-08-20):** backend reexpõe `Platform`. `dotnet test`: 490/490 passando. PR feature→desenv: https://github.com/DQM-BETA/omuletachou/pull/255
- **Dev #254 concluído (2026-08-20, 1ª rodada):** `DealCard.tsx` com a tag, `types.ts`, `deal-card.css`. `npm test`: 109/109 passando, 100% cobertura em `DealCard.tsx`. PR feature→desenv: https://github.com/DQM-BETA/omuletachou/pull/256
- **Merge LT (2026-08-20):** PR #255 e PR #256 squash-merged para `desenv`. Ambas sub-issues fechadas. PR desenv→homolog criado (merge commit): https://github.com/DQM-BETA/omuletachou/pull/257

### Code Review reprovou PR #257 (2026-08-20) — mapeamento da falha
**Motivo:** `design.md`/`especificacao-tecnica.md` presumiam erroneamente que `DealCard.tsx` é reutilizado na página de oferta/detalhe. Na verdade `website/app/oferta/[slug]/page.tsx` renderiza o produto principal via `website/components/DealDetail.tsx` — componente **separado**, com markup próprio (`deal-detail__price*`), nunca tocado pelo PR #256. `DealCard` só é usado dentro de `DealDetail` na seção "Mais ofertas" (relacionados), não no produto sendo visualizado. Isso viola o **Critério de Aceite 3**: a tag nunca aparece na página do próprio produto.

Demais pontos do PR (backend #253, testes, build/boot via docker compose, integração real contra Postgres, checklist de veto) foram validados e aprovados pelo Code Review — não precisam ser refeitos.

**Investigação confirmou:** `DealDetail` já recebe `deal: Deal` completo (via `fetchDeal(slug)` em `page.tsx`, mesma API já corrigida por #253) — `platform` já chega até lá sem nenhum ajuste de dados/API. O gap é puramente de renderização faltante em `DealDetail.tsx`.

**Ação tomada:**
- Documentos corrigidos: `openspec_path/design.md` (seção "CORREÇÃO pós Code Review"), `docs_path/especificacao-tecnica.md` (seção "CORREÇÃO pós Code Review"), `openspec_path/tasks.md` (T-02 com subseção "CORREÇÃO PENDENTE").
- **Sub-issue #254 reaberta** (mesmo padrão usado na Issue #228 — reabrir em vez de criar nova), pois é continuação direta do mesmo escopo/task_id T-02, não um requisito novo. Comentário de correção postado: https://github.com/DQM-BETA/omuletachou/issues/254#issuecomment-5358016702
- `desenv_tasks_merged` revertido para `['#253']` (removido `#254` — precisa de novo merge após a correção).
- `code_review_homolog_pr` marcado como reprovado; PR #257 permanece aberto (LT não fecha PR reprovado — Dev deve atualizar/reabrir conforme fluxo de merge da próxima rodada; se o Dev abrir novo PR feature→desenv, o LT fará novo merge→desenv e reemitirá o PR desenv→homolog, ou reaproveitará o #257 se ainda mergeável).
- `etapa_atual` revertido para "Em Desenvolvimento".
