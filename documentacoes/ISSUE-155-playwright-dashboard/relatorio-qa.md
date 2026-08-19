# Relatório de QA — Issue #155 (sub-issue #232) — Playwright (`test:visual`) no dashboard

**Status: ✅ APROVADO**

Branch validada: `homolog` (commit `44f9df9`, PR #234 mergeado). Sincronização confirmada via
`git fetch origin && git checkout homolog && git pull origin homolog` — commit presente em
`git log --oneline -5`.

## Objetivo da issue (validado)
O próprio objeto deste QA era confirmar que o Gate Visual obrigatório do QA passa a **disparar
de verdade** para o dashboard (achado original #154/#155: script `test:visual` não existia).
Confirmado: `npm run test:visual` existe, roda de ponta a ponta e gera 8 screenshots reais.

## 1. Build/boot real a partir de `homolog`
- `docker compose build --no-cache dashboard` — build limpo, sem cache, sucesso (`ng build
  --configuration=production` completo em ~17s, sem erros, apenas warnings de budget de bundle
  já existentes/não relacionados a esta issue).
- `docker compose up -d dashboard` — container `afiliado_dashboard` recriado e subiu saudável.
- `curl -sI http://localhost:8081/` → `HTTP/1.1 200 OK` (nginx servindo o build de produção).

## 2. `npm run test:visual` — execução real
```
SCREENSHOTS_DIR={docs_path}/screenshots npm run test:visual
```
- `@playwright/test@1.62.1` instalado, browser `chromium` instalado.
- webServer (`npm start`, `http://localhost:4200`) subiu automaticamente via config.
- **Resultado: 8 passed (27.3s)** — 8/8 specs passando, screenshots reais gerados em
  `documentacoes/ISSUE-155-playwright-dashboard/screenshots/` (login, products, queue,
  facebook-manual, mercadolivre-links, settings, jobs, reports).

## 3. Gate Visual obrigatório do QA (regra d2) — inspeção das 8 screenshots
Todas as 8 imagens foram abertas e inspecionadas visualmente:

| Tela | Header/Sidenav 1x | Footer | Estrutura duplicada | CSS/Material aplicado |
|---|---|---|---|---|
| login.png | N/A (rota pública sem shell) | N/A | Não | Sim — card centralizado, inputs e botão estilizados |
| products.png | Sim (sidenav "omuletachou" 1x) | N/A (admin tool sem footer) | Não | Sim — tabela, filtros, dropdowns Material |
| queue.png | Sim | N/A | Não | Sim |
| facebook-manual.png | Sim | N/A | Não | Sim |
| mercadolivre-links.png | Sim | N/A | Não | Sim |
| settings.png | Sim | N/A | Não | Sim |
| jobs.png | Sim | N/A | Não | Sim — cards com botões "Disparar" estilizados |
| reports.png | Sim | N/A | Não | Sim |

Observações:
- Sidenav/header "omuletachou" aparece exatamente 1x em todas as telas autenticadas — sem
  duplicação de componente estrutural.
- Não existe footer no dashboard (ferramenta interna/admin) — nenhuma spec exige footer aqui;
  não é uma omissão, é o layout esperado.
- Mensagens de erro ("Erro ao carregar produtos", "Não foi possível carregar as configurações"
  etc.) aparecem nas telas autenticadas — **isto é o comportamento esperado e documentado na
  especificação técnica** (`blockApiCalls` aborta `/api/**` de propósito, para tornar o teste
  determinístico independente da API estar no ar; o objetivo é validar CSS/layout, não dado).
  Os componentes tratam o erro de forma estilizada (snackbar/mensagem), sem quebra visual.
- Não há `ux-ui-spec.md` específico para esta issue (chore de infraestrutura de teste, sem
  mudança de UI/design) — não há paleta/tipografia nova a comparar. Nenhuma regressão visual
  encontrada nos componentes existentes (Material Design consistente com o padrão já usado no
  dashboard).
- Dark mode: não é um requisito documentado para o dashboard; nenhum toggle presente.

**Gate Visual: PASSOU.**

## 4. Validação integrada (d3 — obrigatória)
Fluxo ponta a ponta exercido contra a stack real (`homolog`, containers Docker rebuildados sem
cache): dashboard (nginx, build de produção) → API .NET real → PostgreSQL real.

```
POST http://localhost:8081/api/auth/login  (via proxy nginx do dashboard)
  body: {"email":"operador@omuletachou.local","password":"DemoLocal123!"}
  → 200 OK, JWT retornado

GET http://localhost:8081/api/products?page=1&pageSize=5  (Authorization: Bearer <jwt>)
  → 200 OK, 110 produtos reais retornados do Postgres
```
Confirma que o container do dashboard, buildado a partir de `homolog`, funciona de ponta a
ponta com o backend real (login real + chamada autenticada real).

## 5. Critérios de aceite (Given/When/Then — especificação técnica)

| # | Critério | Evidência | Status |
|---|---|---|---|
| CA-1 | `dashboard/package.json` tem `"test:visual": "playwright test"` | Confirmado por leitura do `package.json` em `homolog` | ✅ |
| CA-2 | `npm run test:visual` roda sem erro de config e gera pelo menos `/login` e `/products` | 8/8 specs passando, `login.png` e `products.png` gerados (+ 6 rotas extras) | ✅ |
| CA-3 | `dashboard/playwright.config.ts` segue o mesmo padrão de `website/playwright.config.ts` (`SCREENSHOTS_DIR`, `STAGING_URL`, reporter list+html, `screenshot: 'only-on-failure'`) | Comparação lado a lado dos dois arquivos — mesmo padrão, adaptado (viewport desktop, projeto único `chromium`, `npm start`/porta 4200) | ✅ |
| CA-4 | `dashboard/.gitignore` ignora `/screenshots` e `/playwright-report` | Confirmado (`/screenshots`, `/playwright-report`, `/test-results`) | ✅ |
| CA-5 | Documentação de como rodar `test:visual` (CLAUDE.md ou equivalente) | Não é possível editar `CLAUDE.md` (trava dura de ferramenta, documentado pelo Dev). Documentado em `dashboard/README.md` seção "Running visual tests" com todos os detalhes (comando, `STAGING_URL`, `SCREENSHOTS_DIR`, nota de autenticação). Equivalente funcional aceito. | ✅ |

## 6. Testes automatizados / regressão
- `npx ng test --watch=false --browsers=ChromeHeadless` → **140/140 SUCCESS**, sem regressão.
- `npx tsc --noEmit -p tsconfig.json` (root, fora do escopo de `ng build`) aponta 3 erros de
  estilo (`noPropertyAccessFromIndexSignature`) em `playwright.config.ts` e `e2e/visual.spec.ts`
  (acesso a `process.env.X` em vez de `process.env['X']`). **Não bloqueante**: esses arquivos
  não fazem parte de `tsconfig.app.json` (não entram no `ng build`, que passou limpo), e o mesmo
  padrão de "tsc --noEmit root não é gate limpo" já existe no projeto irmão `website` (erros
  pré-existentes não relacionados). Registrado como melhoria menor, não como reprovação.

## Conclusão
Todos os critérios de aceite (CA-1 a CA-5) passam. Build/boot real a partir de `homolog`
funcionou. `npm run test:visual` roda de verdade e gera 8/8 screenshots. Gate Visual do QA
inspecionado manualmente em todas as 8 telas — sem duplicação estrutural, sem quebra de CSS.
Fluxo integrado (dashboard real + API real + Postgres real) validado com sucesso. Testes
unitários sem regressão (140/140).

**O objetivo da issue está cumprido: o Gate Visual do QA agora dispara de verdade para o
dashboard.**

## Achados não bloqueantes (registrar para follow-up, não impedem aprovação)
1. `npx tsc --noEmit` na raiz do `dashboard` aponta 3 erros de estilo
   (`noPropertyAccessFromIndexSignature`) em `playwright.config.ts` e `e2e/visual.spec.ts` —
   trocar `process.env.X` por `process.env['X']`. Fora do gate de build real (`ng build`), mas
   vale um ajuste rápido em PR futuro.
