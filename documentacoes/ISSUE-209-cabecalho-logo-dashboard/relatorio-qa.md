# Relatório QA — Issue #209 (fix: cabeçalho/logo do dashboard não está renderizando corretamente)

**Status: ✅ APROVADO**

## Contexto validado
`homolog` @ commit `adfcfea5ae7202f20553782968218d37d4d10cfd` (merge PR #213, contém PR #212 — fix #209).
`git fetch origin && git checkout homolog && git pull origin homolog` confirmou fast-forward `c95a0ce..adfcfea`; commit alvo presente em `git log --oneline -5`.

## Ambiente
Containers Docker (`afiliado_db`, `afiliado_api`, `afiliado_dashboard`) reaproveitados do Code Review. Timestamp da imagem (18:41:47Z) ficava entre os commits de fix (08ce80f/d82825a, 18:38–18:39Z) e o commit de merge (adfcfea, 18:48Z) — ambíguo o suficiente para não confiar apenas no timestamp. Rodei `docker compose build dashboard api`: build 100% em cache (hash de contexto idêntico) → confirmado que a imagem já refletia o código atual. `docker compose up -d --no-deps dashboard api` não recriou containers (config/imagem inalterada). `afiliado_api` healthy, `afiliado_db` healthy.

## Testes automatizados
| Suíte | Resultado |
|---|---|
| Backend (`dotnet test`) | **441/441** aprovados |
| Dashboard (`ng test --watch=false --browsers=ChromeHeadless`) | **134/134** aprovados (bate com o esperado) |
| `tsc --noEmit` (dashboard) | sem erros |
| `shell.component.spec.ts` — `describe('cabeçalho/logo (Issue #209)')` | 3 testes, todos passando (elemento de logo dedicado; `position: sticky` fora da área de scroll; proteção contra overflow/ellipsis) |

## Gate visual (screenshots arquivados em `documentacoes/ISSUE-209-cabecalho-logo-dashboard/screenshots/`)
Dashboard não define `test:visual` no `package.json` (só existe em `website/`) — não há pipeline Playwright automatizado para este projeto. Evidência visual coletada manualmente com Playwright (script ad-hoc fora do repo) contra os containers Docker reais em `http://localhost:8081`, logado com o usuário seed real via UI (login → rota protegida `/products` batendo na API real).

- `01-low-viewport-before-scroll.png` — viewport 1280x350, sidenav no topo: header "omuletachou" visível 1x, sem corte.
- `02-low-viewport-sidenav-scrolled.png` — mesmo viewport, `mat-nav-list` rolado até o fim (8 itens de menu, incluindo Reports/Jobs): header continua fixo (`sticky`) no topo, texto "omuletachou" completo e legível, **sem sobreposição/corte** — reproduz exatamente o cenário descrito na investigação da issue (viewport baixo + sidenav rolado) e confirma a correção.

Checklist do gate visual:
- [x] Header visível exatamente 1x em cada tela (sem duplicação)
- [x] Nenhum componente estrutural duplicado
- [x] Layout condiz com o padrão visual do shell (paleta azul primária do Material, tipografia consistente) — não há `ux-ui-spec.md` nesta issue (rota `rapido`, sem UX/UI dedicado)
- N/A — Dark mode: aplicação não implementa dark mode

## Validação integrada (E2E manual via Playwright)
Medição programática (bounding box) após scroll do sidenav em viewport 1280x350:
```
LOGO_BOX -> {"x":16,"y":20,"width":207,"height":24}
TOOLBAR_BOX -> {"x":0,"y":0,"width":239,"height":64}
LOGO_VISIBLE_AFTER_SCROLL -> true
CLIPPED_ABOVE_VIEWPORT -> false
```
`y` do logo permanece positivo (dentro da viewport) mesmo com o `mat-nav-list` rolado até `scrollHeight` — confirma que o `mat-toolbar` ficou isolado da área de scroll (fix `position: sticky; top: 0` em `.shell-toolbar`, `.shell-nav-list` com `overflow-y: auto` próprio).

## Critérios de aceite (estado.md)
| Critério | Evidência | Status |
|---|---|---|
| Dev reproduz ao vivo | Já marcado pelo Dev (`ng serve`, build produção, Docker) | ✅ |
| CSS do cabeçalho/logo inspecionado e corrigido | `shell.component.scss` — `flex-direction: column` + `.shell-toolbar { position: sticky; top:0 }` + `.shell-nav-list { overflow-y:auto }` | ✅ |
| Screenshot antes/depois anexado ao PR | Confirmado no PR #212 (Dev) | ✅ |
| QA valida visualmente em múltiplas resoluções | Validado em 1280x800 (padrão) e 1280x350 (cenário de bug, antes/depois do scroll) — ver screenshots acima | ✅ |

## Issues encontradas
Nenhuma.

## Conclusão
100% dos critérios de aceite validados com evidência de execução real (não apenas leitura de código). Aprovado.
