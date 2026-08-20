---
issue: 229
titulo: 'feat: exibir tag pequena de plataforma de origem nos cards de produto do site público'
etapa_atual: Code Review
ultimo_agente: lider-tecnico
openspec_change: repos/omuletachou/openspec/changes/issue-229-exibir-tag-plataforma
tech_stacks: [dotnet, nextjs]
repos:
  omuletachou: já existente
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma
openspec_path: repos/omuletachou/openspec/changes/issue-229-exibir-tag-plataforma
sub_issues: ['#253 (stack:dotnet, task_id:T-01)', '#254 (stack:nodejs, task_id:T-02)']
desenv_tasks_merged: ['#253', '#254']
sub_issues_frontend: {'#254': 'PR #256 (feature/ISSUE-254-tag-plataforma-dealcard -> desenv), merged'}
pr_homologacao: 257
pr_release: ~
code_review_homolog_pr: ~
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

## Notas
- **Rota:** promovida de `backlog` para `normal` pelo Gerente no Gate 1 (2026-08-20) — segue o pipeline completo a partir daqui.
- **Gate 1 respondido (2026-08-20):** confirmado sem conflito com Issue #167 (sinalização visual, não filtro); posição próxima ao preço, discreta; formato texto (não ícone); aparece em todas as telas (home/categoria/oferta). Comentário: https://github.com/DQM-BETA/omuletachou/issues/229#issuecomment-5357600715
- **PRD (Fase 2) concluído (2026-08-20):** `proposal.md` e `criterios-aceite.md` escritos cobrindo exibição em todas as telas, tratamento de produto sem plataforma identificada/valor não mapeado (tag oculta, sem erro), e legibilidade em mobile.
- **Avaliação de ambiguidade arquitetural:** sem ambiguidade — mudança de exibição em componente já existente do `website/` (Next.js), dado de plataforma já existe no domínio do produto. Única pendência técnica (se o campo já está exposto na API pública) é detalhe de implementação, não decisão de arquitetura. Segue direto para o **Líder Técnico** (com apoio do UX/UI para texto/estilo da tag).
- **Refinamento Técnico concluído (2026-08-20):** investigação confirmou que o card de produto do site público é um único componente compartilhado (`website/components/DealCard.tsx`), reutilizado nas 3 telas (home, categoria, oferta) via `app/page.tsx` e `app/categoria/[categoria]/page.tsx` — 1 sub-issue de frontend cobre as 3 telas conforme esperado pelo spawn.
- **Achado importante (diverge da hipótese do proposal.md):** o campo `Platform` **NÃO está exposto** na API pública — foi **explicitamente removido** de `PublicDealDto` na Issue #167 (CA 5.1/5.2/5.3, comentário de cabeçalho da classe e teste `GetDeals_JsonDeResposta_NuncaContemCampoPlatform`), por higiene, quando a distinção de plataforma deixou de servir como filtro/navegação. A #229 precisa **reintroduzi-lo** — reversão parcial e intencional da #167, já validada pelo Gerente no Gate 1 (sinalização visual ≠ filtro, sem conflito). Isso tornou o escopo full-stack (backend + frontend), não isolado ao `website/` como o proposal.md havia hipotetizado — nota registrada para o PM/Arquiteto revisarem a suposição em demandas futuras que dependam de campos já "higienizados" do contrato público.
- **Task breakdown: 2 sub-issues (não 1)** — por cruzar 2 stacks do monorepo (backend .NET + frontend Next.js), split em:
  - `#253` (`stack:dotnet`, T-01): reexpor `Platform` como string bruta do enum em `PublicDealDto` (`GetDeals`/`GetBySlug`), sem tradução de texto (fica a cargo do frontend); atualizar teste que assumia ausência do campo.
  - `#254` (`stack:nodejs`, T-02): `DealCard.tsx` (+ `types.ts`, `deal-card.css`, testes) renderiza a tag com mapeamento enum→texto definido pelo UX/UI; oculta se ausente/não mapeado (CA 4/5); não interativa (CA 7).
  - Dependência: integração completa (T-02 consumindo o campo real da API) depende de T-01 mergeado; desenvolvimento/testes unitários de T-02 seguem em paralelo com mocks.
- **UX/UI necessário:** demanda envolve UI (texto exato + estilo da tag, consultando o design system do Figma) — sinalizado no HANDOFF (`proximo: UX/UI`) antes dos devs, conforme instrução do Gerente.
- Docs escritas: `openspec_path/design.md` (resumido, LT), `docs_path/especificacao-tecnica.md`, `openspec_path/tasks.md` (T-01/T-02).
- **UX/UI concluído (2026-08-20):** `ux-ui-spec.md` escrito. Decisões: tag de texto neutra (sem distinção de cor por plataforma), nome completo ("Amazon", "Mercado Livre", "Shopee"), posicionada em linha própria acima do bloco de preço (evita aperto em mobile), oculta quando `platform` ausente/não mapeado, não interativa. Usa tokens já existentes em `deal-card.css`. Comentário: https://github.com/DQM-BETA/omuletachou/issues/254#issuecomment-5357731168
- **Dev #253 concluído (2026-08-20):** `PublicDealDto.Platform` (`string?`) reexposto via `product.Platform.ToString()` em `GetDeals`/`GetBySlug`. Teste `GetDeals_JsonDeResposta_NuncaContemCampoPlatform` substituído por `GetDeals_JsonDeResposta_ContemCampoPlatformComValorCorreto`. `dotnet test`: 490/490 passando. Validado contra Postgres real via `docker compose` (produto seedado com `Platform=MercadoLivre` → `GET /api/public/deals` e `/deals/{slug}` retornaram `"platform":"MercadoLivre"`). PR feature→desenv: https://github.com/DQM-BETA/omuletachou/pull/255
- **Dev #254 concluído (2026-08-20):** `Deal.platform?: string | null` reintroduzido em `types.ts`; `DealCard.tsx` com `PLATFORM_LABELS` (Amazon/Mercado Livre/Shopee) e tag `<span className="deal-card__platform">` como primeiro filho de `.deal-card__price` (linha própria acima do preço — markup de preço movido para novo `.deal-card__price-row`), oculta se `platform` ausente/`null`/não mapeado, sem `href`/`onClick`/`tabindex`. `deal-card.css`: nova classe `.deal-card__platform` só com tokens já existentes (`--color-neutral-100/600`, `--font-size-xs`, `--space-*`, `--radius-sm`; nunca `--color-primary`). Testes novos cobrindo CA 1-8 (exibição das 3 plataformas, ocultação com `null`/`undefined`/valor não mapeado, não-interatividade). `npm test`: 109/109 passando, 100% cobertura em `DealCard.tsx`. `npm run build` e `npm start` (boot) validados — servidor sobe sem erro (500 na home é fetch para hostname Docker `api`, indisponível localmente, não relacionado à mudança). Desenvolvido/testado com mocks (`buildDeal`), conforme dependência documentada; integração real com API depende do merge de #253 (já concluído). PR feature→desenv: https://github.com/DQM-BETA/omuletachou/pull/256
- **Merge LT (2026-08-20):** PR #255 (#253, backend) squash-merged para `desenv` primeiro, depois PR #256 (#254, frontend) squash-merged — respeitando a dependência (frontend integra o campo real exposto pelo backend). Ambas sub-issues fechadas. PR desenv→homolog criado via merge commit: https://github.com/DQM-BETA/omuletachou/pull/257
