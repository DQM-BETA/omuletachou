# Relatório QA — ISSUE-229: Tag pequena de plataforma de origem nos cards de produto

**Status: APROVADO**

## Sincronização e escopo validado
- `git fetch origin` + `git checkout homolog` + `git pull origin homolog` — branch local sincronizada.
- Confirmado commit `89beab1a` (merge do PR #257, `desenv→homolog`) no topo de `git log --oneline` de `homolog`.
- PR #258 (correção `DealDetail.tsx`, CA3) já absorvido — commit `b7d8a08c` presente no histórico.

## Testes automatizados
| Suíte | Resultado |
|---|---|
| `dotnet test` (backend) | 490/490 passando |
| `npm test` (website, jest) | 117/117 passando (15 suítes) |
| `npx tsc --noEmit` (website) | Gap pré-existente (`toBeInTheDocument`/`toHaveAttribute` não tipados — `@testing-library/jest-dom` ausente de `tsconfig.json` `types`) afeta arquivos não relacionados a esta issue (`FilterBar.test.tsx`, `Header.test.tsx`, `PushSubscriptionManager.test.tsx`, `lib/push.test.ts`) — confirmado não ser regressão desta mudança. `npx next build` (usado como evidência de type-check real de produção) compila e type-checa sem erros. |
| `npm run test:visual` (Playwright, mobile-chromium) | 5/5 passando |

## Gate visual (screenshots arquivadas em `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/screenshots/`)
Inspeção visual de `home.png`, `categoria.png`, `deal-detail.png`:
- Header ("O Mulet Achou") aparece exatamente 1x em cada tela — sem duplicação.
- Sem componente de Footer no projeto (confirmado: não existe `Footer`/`site-footer` em `website/components/` nem `app/`) — condição pré-existente do site, não uma regressão desta issue.
- Tag "Mercado Livre" visível, discreta (chip cinza claro, texto neutro), posicionada em linha própria acima do preço, em todos os cards de todas as telas — condiz com `ux-ui-spec.md`.
- Em `deal-detail.png`: o produto principal ("Liquidificador Turbo Full Mondial 900W") exibe sua própria tag "Mercado Livre" acima do preço `R$ 109,00`; a seção "Mais ofertas" abaixo mostra 4 produtos relacionados, cada um com sua própria tag "Mercado Livre" — visualmente distintas (chips separados por card), sem confusão entre a tag do produto principal e as dos relacionados.
- Sem cores/tokens hardcoded aparentes (chip usa tom neutro cinza claro consistente com o resto do design system do card).

## Validação integrada (aplicação real, ponta a ponta)
- `docker compose build --no-cache api website` (branch `homolog`) — build limpo, sem cache antigo. `next build`: "Compiled successfully", type-check de produção OK.
- `docker compose up -d db api website` — 3 containers saudáveis (`afiliado_db`, `afiliado_api` healthy, `afiliado_website` up). `/health` → 200. Home (`/`) → 200.
- Postgres real consultado diretamente (`docker exec afiliado_db psql`): 105 produtos `status=Pending/Queued`(2), **12 produtos `status=Published`** — todos com `platform=1` (MercadoLivre), dado real de produção replicado em homolog.
- Requisições HTTP reais (curl) contra o `website` (porta 3000) e a `api` (porta 8080), servindo HTML SSR real (não mock/unit):
  - `GET /` (home): 12 `deal-card` renderizados, **12** `data-testid="platform-tag"` — 1:1, todos "Mercado Livre".
  - `GET /categoria/Geral`: 4 `deal-card` renderizados, **4** tags de plataforma — 1:1.
  - `GET /oferta/microfone-hollyland-lark-m2-lark-m2-combo-branco-branco-mlb51892201`: confirmado no HTML SSR (antes de qualquer hidratação JS) que `.deal-detail__price` tem `<span class="deal-card__platform" data-testid="platform-tag">Mercado Livre</span>` como primeiro filho, antes de `.deal-detail__price-current` — **CA3 confirmado no ambiente real**, exatamente o gap que reprovou a 1ª rodada de Code Review, agora corrigido em `DealDetail.tsx`.
  - Na mesma página de oferta, seção "Mais ofertas" (relacionados) usa `article.deal-card` com suas próprias tags — DOM nodes distintos do produto principal, sem confusão (confirmado via grep estrutural do HTML e reforçado pelo teste `DealDetail.test.tsx` "CA 3: a tag do produto principal é distinta da tag dos relacionados").
  - Nenhuma alteração residual no Postgres (contagem de status idêntica antes/depois: 105/12/94) — nenhuma mutação de dado foi aplicada (tentativa de setar produto para Amazon/Shopee via SQL foi bloqueada pelo classificador de permissão do ambiente; validação seguiu com dado real já publicado em MercadoLivre, suficiente para confirmar renderização ponta a ponta, complementada pelos testes unitários que já cobrem Amazon/Shopee/null/não-mapeado com 100% de cobertura declarada).
  - Stack derrubada ao final (`docker compose down`) — sem alteração residual.

## Critérios de aceite — tabela de validação
| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | Tag na home, próxima ao preço | `home.png` + HTML SSR: 12/12 cards com tag acima do preço | OK |
| 2 | Tag na categoria, mesmo padrão visual | `categoria.png` + HTML SSR: 4/4 cards, mesma classe `.deal-card__platform` | OK |
| 3 | Tag na página de oferta/detalhe (produto principal) | `deal-detail.png` + HTML SSR bruto: tag como primeiro filho de `.deal-detail__price`, distinta das tags dos relacionados | OK |
| 4 | Produto sem plataforma → tag oculta, sem erro | `DealDetail.test.tsx`/`DealCard.test.tsx` (`platform: null`/`undefined`) — 100% cobertura declarada; não reproduzível em dado real (coluna `platform` é `NOT NULL` no schema atual) | OK (via teste unitário) |
| 5 | Valor não mapeado → tag oculta, sem vazar valor cru | `DealDetail.test.tsx`/`DealCard.test.tsx` (`platform: 'Aliexpress'`) — tag ausente, texto cru não aparece | OK (via teste unitário) |
| 6 | Legível em mobile | Viewport 375x812 (Playwright mobile-chromium) — `deal-detail.png`/`home.png`/`categoria.png` mostram tag legível, não cortada, `white-space: nowrap` + `text-overflow: ellipsis` de segurança no CSS | OK |
| 7 | Tag não interativa/não filtro | HTML real: `<span class="deal-card__platform" data-testid="platform-tag">` sem `href`/`onclick`/`tabindex`; CSS `cursor: default`, sem estados hover/focus; `FilterBar.tsx` sem nenhuma menção a "platform" (0 ocorrências) — sem regressão da decisão da Issue #167 | OK |
| 8 | Consistência de texto entre telas | Mesmo produto MercadoLivre exibe "Mercado Livre" idêntico em home/categoria/oferta; `PLATFORM_LABELS` centralizado em `website/lib/platform.ts`, reaproveitado por `DealCard.tsx` e `DealDetail.tsx` | OK |

## Conclusão
Todos os 8 critérios de aceite validados — 6 via HTTP real contra `homolog` com dado real do Postgres (SSR), 2 via teste unitário com 100% de cobertura declarada (cenários que a base de dados publicada atual não reproduz naturalmente, pois `platform` é `NOT NULL`). Gate visual sem achados. Sem regressão de filtro/navegação por plataforma (Issue #167). QA **aprovado**.
