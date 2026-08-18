# Relatório QA — Issue #210 (fix: tooltip de motivo do erro aparece no AI Score, não no Status)

**Status: ✅ APROVADO**

## Contexto validado
`homolog` @ commit `adfcfea5ae7202f20553782968218d37d4d10cfd` (merge PR #213, contém PR #211 — fix #210).
`git fetch origin && git checkout homolog && git pull origin homolog` confirmou fast-forward `c95a0ce..adfcfea`; commit alvo presente em `git log --oneline -5`.

## Ambiente
Containers Docker (`afiliado_db`, `afiliado_api`, `afiliado_dashboard`) reaproveitados do Code Review, com verificação de que a imagem refletia o código atual: `docker compose build dashboard api` retornou 100% em cache (hash de contexto idêntico ao commit atual) — descartada a hipótese de imagem desatualizada. `afiliado_api` healthy, `afiliado_db` healthy. Produtos ML reais com `status=Error` confirmados via API (`GET /api/products?status=Error`) — ex.: "Jogo Da Forca Interativo..." (MLB65764419), `ai_reason: "Nenhuma rede social habilitada com credenciais validas para publicar este produto."`.

## Testes automatizados
| Suíte | Resultado |
|---|---|
| Backend (`dotnet test`) | **441/441** aprovados |
| Dashboard (`ng test --watch=false --browsers=ChromeHeadless`) | **134/134** aprovados |
| `tsc --noEmit` (dashboard) | sem erros |
| `products.component.spec.ts` — CA-B6/CA-B7 | `CA-B6 — tooltip com o motivo do erro aparece no badge de Status (não no de AI Score) quando Status = Error` e `CA-B7 — tooltip de justificativa do AI Score permanece no badge de AI Score quando Status != Error`, ambos passando |

## Gate visual (screenshots arquivados em `documentacoes/ISSUE-210-tooltip-erro-ai-score/screenshots/`)
Dashboard não define `test:visual` no `package.json` (só existe em `website/`) — sem pipeline Playwright automatizado neste projeto. Evidência coletada manualmente com Playwright (script ad-hoc fora do repo) contra os containers Docker reais em `http://localhost:8081`, com **fluxo integrado real**: login via UI (`POST /api/auth/login` real) → rota protegida `/products` → filtro `Status=Error` → hover nos badges de um produto ML real com erro.

- `01-products-filtered-error.png` — tabela filtrada por `Status=Error`, badges "AI Score" e "Status" visíveis lado a lado na primeira linha (produto ML real).
- `02-hover-ai-score-no-tooltip.png` — mouse sobre o badge "AI Score" (nota 9) do produto em erro: **nenhum tooltip aparece** (`matTooltipDisabled` quando `status==='Error'`) — confirmado programaticamente (contagem de elementos `.mat-mdc-tooltip` = 0).
- `03-hover-status-badge-tooltip.png` — mouse sobre o badge "Status" ("Error"): tooltip aparece com o texto **"Nenhuma rede social habilitada com credenciais validas para publicar este produto."** — exatamente o `ai_reason` do produto, ancorado corretamente na coluna Status.

Checklist do gate visual:
- [x] Header visível exatamente 1x (não há duplicação de shell nas 3 telas)
- [x] Nenhum componente estrutural duplicado
- [x] Layout (badges, tabela, filtros) condiz com o padrão visual existente — sem `ux-ui-spec.md` dedicado (rota `rapido`)
- N/A — Dark mode: aplicação não implementa dark mode

## Validação integrada (E2E manual via Playwright)
```
FIRST_ROW_STATUS -> Error
TOOLTIP_VISIBLE_ON_AI_SCORE_HOVER -> 0
TOOLTIP_VISIBLE_ON_STATUS_HOVER -> 2 | TEXT: Nenhuma rede social habilitada com credenciais validas para publicar este produto.
```
(contagem 2 no hover do Status = elemento visual + espelho ARIA do CDK Overlay, comportamento normal do Angular Material — texto único e correto.)

## Critérios de aceite (estado.md)
| Critério | Evidência | Status |
|---|---|---|
| Dev reproduz ao vivo | Já marcado pelo Dev | ✅ |
| Tooltip do motivo do erro movido para a coluna Status | `products.component.html` — `matTooltip` com `ai_reason` no `status-badge`, `matTooltipDisabled` no `ai-score-badge` quando `status==='Error'` | ✅ |
| Comportamento mantido (`ai_reason` no hover) no elemento correto | Confirmado com produto ML real — texto do tooltip bate com o `ai_reason` retornado pela API | ✅ |
| QA valida a mudança de posição do tooltip | Validado ao vivo: hover no AI Score não mostra tooltip; hover no Status mostra o motivo correto | ✅ |

## Issues encontradas
Nenhuma.

## Conclusão
100% dos critérios de aceite validados com evidência de execução real (não apenas leitura de código ou testes unitários). Aprovado.
