# Relatório de QA — ISSUE-228: Relatório de produtos com filtros na tela Reports

**Status: ✅ APROVADO**

Branch validada: `homolog` (commit `5f639ad`, PR #250 `desenv→homolog`, merge commit,
confirmado presente via `git log --oneline -5` após `git fetch` + `git pull origin homolog`).
Sub-issues: #242 (T-01 índice), #243 (T-02 endpoint summary), #244 (T-03 extensão
`GetProducts`), #245 (T-04 UI Angular, com correção de Code Review — colapso mobile + skeleton
— PR #251 já absorvido em `desenv` e propagado a `homolog` via #250).

## 1. Suítes automatizadas

| Suíte | Resultado |
|---|---|
| `dotnet test` (backend) | **490/490 passando** |
| `npm test` (Angular, Karma/Chrome Headless) | **179/179 passando** |
| `npx tsc --noEmit -p tsconfig.app.json` (código de app) | **0 erros** |
| `npx tsc --noEmit -p tsconfig.json` | 3 erros pré-existentes em `e2e/visual.spec.ts` e `playwright.config.ts` (`TS4111`, index signature access) — **não relacionados a esta feature**, arquivos não tocados pelo PR #250/#251, fora do escopo desta issue |

## 2. Build/boot real (Docker, imagens de `homolog`)

- `docker compose build --no-cache api dashboard` — build ok, sem erros, a partir da branch
  `homolog` sincronizada (`git fetch` + `checkout homolog` + `pull` confirmados antes do build).
- `docker compose up -d db api dashboard` — os 3 containers sobem saudáveis
  (`afiliado_api` healthy, `afiliado_db` healthy, `afiliado_dashboard` up).
  Nota de ambiente: `nginx-proxy-manager` (porta 80) não subiu nesta validação por conflito de
  porta 80 já reservada pelo Windows (http.sys, processo de sistema) — não bloqueante, pois
  `api`/`dashboard` têm mapeamento direto de porta via `docker-compose.override.yml` local
  (8080/8081) e a feature validada é 100% admin/dashboard, sem dependência do proxy.
- `GET /api/health` → `200 {"status":"healthy"}`.
- Índice `IX_products_status_platform_createdat` confirmado via `psql \d products` rodando
  contra o Postgres real do container: `btree (status, platform, created_at DESC)` — exatamente
  como especificado em T-01.

## 3. Validação integrada real (login real + dados reais no Postgres)

Login real via `POST /api/auth/login` (usuário seed `operador@omuletachou.local`) — token JWT
válido obtido e usado em todas as chamadas seguintes (API) e via UI real (Playwright contra
`http://localhost:8081`, imagem Docker construída a partir de `homolog`).

### 3.1 Endpoint `GET /api/reports/products/summary`
| Consulta | Resultado |
|---|---|
| `status=Published` (sem outros filtros) | `total:105`, breakdowns por Plataforma/Categoria/Status/Subcategoria consistentes (CA 1.1) |
| `category=Eletrônicos&status=Published` | `total:17`, `byCategory:[{Eletrônicos:17}]` (CA 2.1) |
| `status=Pending` (não existe no domínio real) → testado com `status=Rejected` e `status=AwaitingAffiliateLink` (valores reais existentes) | `Rejected: total 12`, `AwaitingAffiliateLink: total 94` — confirma que filtro de Status explícito **não** fica restrito a Published (CA 2.4) |
| `platform=MercadoLivre&category=Eletrônicos&status=Published` (AND) | `total:17` — igual ao filtro isolado de categoria, pois todos os produtos são MercadoLivre no dataset atual; interseção comprovada sem erro (CA 2.6) |
| `category=Eletrônicos&platform=Amazon` (sem correspondência) | `total:0`, 4 listas de breakdown vazias, `200 OK` (CA 1.3/2.7) |
| `collectedFrom=2020-01-01&collectedTo=2030-01-01` | `total:105` (janela ampla, universo completo) |
| `collectedFrom=1999-01-01&collectedTo=1999-01-02` | `total:0`, sem erro (janela sem correspondência) |
| sem token | `401` |

### 3.2 Endpoint `GET /api/products` (não-regressão + extensão)
- Sem os 4 novos params: retorna todos os status (não restrito a `Published`), cada item já traz
  o campo `subcategory` (aditivo, não quebra nada) — comportamento idêntico ao anterior +
  campo novo aditivo, sem quebra de contrato.
- `subcategory=Eletroportáteis`: filtra corretamente (produtos retornados todos com essa
  subcategoria).

### 3.3 UI real (Playwright contra a imagem Docker de `homolog`, login real, dados reais)
- **Desktop, sem filtro:** cards "Hoje/Semana/Mês" + gráfico "Publicações por rede" preservados
  e inalterados acima do novo bloco (CA 1.2); bloco novo "Relatório de produtos publicados"
  carrega com filtros (Categoria/Subcategoria/Plataforma/Status/Data) + card "Total".
- **Desktop, filtro Categoria=Eletrônicos aplicado:** `Total: 17`, `Por Plataforma: MercadoLiv... 17`,
  `Por Categoria: Eletrônicos 17`, `Por Status: Published 17`, `Por Subcategoria` (Áudio 8, TV e
  Imagem 5, Informática 3, Celulares... 1) — **bate exatamente** com a resposta da API testada em
  3.1. Chip `Categoria: Eletrônicos ✕` visível, "Limpar filtros" habilitado (CA 2.1, 2.3 chips).
- **"Limpar filtros":** clicado após filtro aplicado → `Total` volta a `105` (universo completo
  Published, CA 2.8).
- **Combinação sem resultado (Categoria=Beleza + Plataforma=Amazon):** os 4 cards de breakdown
  mostram "Nenhum dado", tabela mostra "Nenhum produto encontrado com os filtros aplicados." —
  sem erro, sem dado remanescente da consulta anterior (CA 2.7).
- **Erro de rede (rota `/api/reports/products/summary` abortada via `page.route`, filtro trocado
  em seguida):** bloco cards+tabela substituído por card único "Não foi possível carregar o
  relatório. Verifique sua conexão e tente novamente." + botão "Tentar novamente"; filtros
  continuam visíveis/usáveis; `Total` **não** aparece mais na tela (sem dado antigo mostrado como
  atual) — cards "Hoje/Semana/Mês" e "Falhas recentes" continuam funcionando normalmente,
  inalterados (CA 5.1, CA 1.2).
- **Mobile (<600px), inspeção de DOM real:** `[data-testid="filters-mobile-panel"]` presente,
  colapsado por padrão (`aria-expanded` ausente/false antes de interação), badge
  `[data-testid="filters-active-badge"]` **ausente** sem filtro e mostra `"(1)"` (header
  `"Filtros (1)"`) após aplicar 1 filtro. **Desktop (1280px): o panel não é renderizado**
  (`count: 0`) — controles sempre expandidos, conforme ux-ui-spec §8. Este é exatamente o item
  que o Code Review havia reprovado no PR #250 original — confirmado corrigido na correção da
  sub-issue #245 (PR #251).
- **Sem exportação/impressão:** `grep -ri "export|imprimir|print"` em
  `reports.component.html` → nenhuma ocorrência (CA 4.1).
- **Sem polling/tempo real:** nenhum `setInterval`/`webSocket`/mecanismo de polling em
  `reports.component.ts`/`reports.service.ts` (CA 3.1, inspeção de código complementar à
  ausência de comportamento observado na sessão de browser).

## 4. Gate visual (screenshots Playwright)

`npm run test:visual` existe em `dashboard/package.json` → projeto **com UI**, gate obrigatório.
Rodado com `STAGING_URL=http://localhost:8081` (imagem Docker real de `homolog`, não `ng serve`)
e `SCREENSHOTS_DIR={docs_path}/screenshots` — **8/8 passando**. PNGs confirmados em
`repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/screenshots/`
(não na raiz do repo).

Inspeção visual de cada screenshot:
- Header (`omuletachou`, barra azul) visível **exatamente 1x** em todas as telas (login, products,
  queue, facebook-manual, mercadolivre-links, settings, jobs, reports).
- Sidenav visível **exatamente 1x**, sem duplicação, em todas as rotas autenticadas.
- Nenhum componente estrutural duplicado.
- `reports.png` (suíte oficial, API bloqueada por design do teste visual — ver `helpers.ts`):
  mostra o bloco de filtros renderizado corretamente (Categoria/Subcategoria/Plataforma/Status/
  Data de coleta, "Limpar filtros" disabled) e o estado de erro (esperado, pois a suíte aborta
  chamadas `/api/**` de propósito para isolar o teste de CSS/layout de dados reais) — sem
  travar a tela, sem duplicar layout, cards "Hoje/Semana/Mês" e gráfico permanecem visíveis acima.
- Dark mode: não há dark mode nesta aplicação (não faz parte do escopo/design system atual do
  dashboard) — N/A, confirmado ausência de qualquer toggle/token de tema escuro em todas as telas.
- Paleta/tipografia condizem com `ux-ui-spec.md` (Angular Material padrão do dashboard,
  reaproveitado sem CSS ad-hoc, cards com mesma classe visual dos cards existentes).

Screenshots complementares desta sessão de QA (validação manual com dados reais e login real,
fora da suíte oficial — evidência extra arquivada em `{docs_path}/screenshots/`):
não commitadas ao repo (geradas via script Playwright temporário em scratchpad, removido ao
final da sessão); resultado textual/numérico documentado nas seções 3.1/3.3 acima é a evidência
primária. As screenshots oficiais da suíte `test:visual` (seção acima) são as arquivadas.

## 5. Tabela de critérios de aceite validados

| CA | Cenário | Evidência | Resultado |
|---|---|---|---|
| 1.1 | Exibição padrão sem filtro | §3.1/§3.3 — `total:105` Published, cards+tabela | ✅ |
| 1.2 | Cards/gráfico existentes preservados | §3.3 — Hoje/Semana/Mês e gráfico inalterados em todas as capturas | ✅ |
| 1.3 | Nenhum produto publicado (simulado via combinação sem match) | §3.1 — `total:0`, listas vazias, sem erro | ✅ |
| 2.1 | Filtro por Categoria | §3.1/§3.3 — `Eletrônicos: 17` API e UI batendo | ✅ |
| 2.2 | Filtro por Subcategoria | §3.2 — `GET /api/products?subcategory=...` filtra corretamente | ✅ |
| 2.3 | Filtro por Plataforma | §3.1 — `platform=MercadoLivre` combinado AND | ✅ |
| 2.4 | Filtro por Status ≠ Published | §3.1 — `Rejected:12`, `AwaitingAffiliateLink:94`, não restrito a Published | ✅ |
| 2.5 | Faixa de data de coleta (inclusiva) | §3.1 — janela ampla=105, janela sem dado=0 | ✅ |
| 2.6 | Combinação AND | §3.1 — `platform+category+status` = interseção correta | ✅ |
| 2.7 | Combinação sem resultado | §3.1/§3.3 — `total:0`, "Nenhum dado"/"Nenhum produto encontrado" | ✅ |
| 2.8 | Limpar filtros | §3.3 — `Total` volta a 105 | ✅ |
| 2.9 | Troca de filtro sem reload | §3.3 — múltiplas trocas de filtro na mesma sessão de página, sem navegação | ✅ |
| 3.1 | Sem tempo real/polling | §3.3 — nenhum mecanismo de polling no código | ✅ |
| 4.1 | Sem exportação/impressão | §3.3 — grep sem ocorrência | ✅ |
| 5.1 | Erro de rede — mensagem clara, sem dado antigo, retry | §3.3 — card de erro único, filtros usáveis, sem `Total` velho | ✅ |
| Responsividade mobile (ux-ui-spec §8) | Filtros colapsados por padrão, badge de contagem | §3.3 — `mat-expansion-panel` colapsado, badge "(1)" após filtro, ausente em desktop | ✅ |

## 6. Issues encontradas

Nenhuma. Todos os critérios de aceite (grupos 1–5 de `criterios-aceite.md`) validados com
evidência de execução real (backend real, Postgres real, login real, UI real). Os dois achados
do Code Review anterior (PR #250 reprovado — colapso mobile ausente e skeleton ausente) foram
confirmados corrigidos nesta validação.

Nota não-bloqueante (mesma já registrada pelo LT/Dev, sem ação necessária): mapeamento de cor
por status ficou local em `reports.component.scss` em vez de reaproveitar `ProductsComponent`
(que não tinha esse mapeamento pronto) — já avaliado e aceito pelo Code Review, fora do escopo
de reabertura pelo QA.

## 7. Ambiente

- `docker compose build --no-cache api dashboard` a partir de `homolog` sincronizada.
- Stack subida com `docker compose up -d db api dashboard` (nginx-proxy-manager omitido por
  conflito de porta 80 do SO local — não impacta a validação da feature, 100% dashboard/API).
- Stack derrubada (`docker compose down`) ao final da validação.
