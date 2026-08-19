---
issue: 155
titulo: "chore: Configurar Playwright (test:visual) no dashboard — Gate Visual do QA nunca dispara"
rota: rapido
etapa_atual: "Concluído"
ultimo_agente: coordenador
openspec_change: ~
tech_stacks:
  - Angular
  - Playwright
repos:
  omuletachou: dashboard
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-155-playwright-dashboard
openspec_path: ~
sub_issues: ["#232 (stack:angular, task_id:T-01)"]
desenv_tasks_merged: ["#232"]
sub_issues_frontend: {}
pr_homologacao: 234
pr_release: 235
code_review_homolog_pr: 234
qa_status: aprovado
figma_url: ~
blockers: nenhum
status_comment_id: IC_kwDOTMlfyM8AAAABPnxeBw
createdAt: 2026-08-14T13:45:04Z
closedAt: 2026-08-19T14:50:24Z
---

## Escopo
Configurar Playwright (`test:visual` script no `package.json`, config básica, screenshots por rota principal) no `dashboard` (Angular), seguindo o padrão já usado com sucesso em `website` (Issue #154/#156, mesmo repo).

## Referências
- `.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md`
- Issue #154 (achado original) / #156 (implementação de referência no `website`)
- Padrão de implementação: `website/playwright.config.ts`, `website/e2e/visual.spec.ts`, `website/e2e/helpers.ts`

## Rota
`rapido` — mudança simples, padrão já provado no mesmo repo (`website`), sem ambiguidade de requisito. Pulou PM Fase 1, Arquiteto, UX/UI, Gate 1; mantém Dev+testes, CR leve, QA, Gate 2.

## Refinamento (LT)
Especificação técnica completa em `especificacao-tecnica.md` (mesmo diretório). Task breakdown mínimo: 1 sub-issue de stack `angular` cobrindo toda a config (script, playwright.config.ts, .gitignore, testes de screenshot para /login e /products, nota sobre auth via sessionStorage sem depender de subir a API).

## Sub-issues
- #232 — stack:angular, task_id:T-01 — Configurar Playwright (test:visual) no dashboard — **feito, PR #233 (feature→desenv), mergeado (squash) e fechada**

## Dev (sub-issue #232)
- Branch `feature/ISSUE-232-playwright-dashboard` (worktree), PR #233: https://github.com/DQM-BETA/omuletachou/pull/233 (mergeado via squash em desenv, commit `0afd100`)
- `@playwright/test@^1.62.1` instalado em `dashboard/`; script `test:visual`; `dashboard/playwright.config.ts` (mesmo padrão de `website/`, projeto `chromium` desktop); `dashboard/.gitignore` atualizado (`/screenshots`, `/playwright-report`, `/test-results`); `dashboard/e2e/visual.spec.ts` (8 specs: `/login` + 7 rotas autenticadas) + `dashboard/e2e/helpers.ts`.
- **Achado durante a implementação (ajuste sobre a spec técnica):** a Nota de autenticação da spec assumia que chamadas de API com token dummy falhariam "silenciosamente". Na prática, se a API .NET estiver de fato no ar localmente, ela responde 401 real ao token dummy inválido, e `authInterceptor` trata qualquer 401 fora de `/api/auth/login` como sessão expirada — disparando logout + redirect para `/login`, quebrando o screenshot da rota autenticada. Solução: `blockApiCalls` (`e2e/helpers.ts`) aborta as chamadas `/api/**` via `page.route`, tornando o teste determinístico independente do estado da API local.
- **CA-5 (documentar em `dashboard/CLAUDE.md`/`CLAUDE.md` do repo):** não realizável como especificado — edição/criação de qualquer arquivo `CLAUDE.md` é bloqueada por permissão de ferramenta (trava dura), independente do path. Documentado em `dashboard/README.md` (seção "Running visual tests") como alternativa equivalente.
- Testes: `npm test` (Karma) 140/140 passando (baseline igual, sem regressão). `npm run test:visual` (Playwright real) 8/8 passando — screenshots gerados e inspecionados visualmente (Material Design aplicado, sidenav/tabelas/formulários estilizados, estados de erro com feedback visível, sem CSS quebrado).

## Líder Técnico (merge + PR de homologação)
- PR #233 (feature→desenv) revisado e mergeado via squash. Sub-issue #232 fechada.
- Todas as sub-issues da Issue #155 concluídas (única sub-issue).
- PR #234 (desenv→homolog, merge commit): https://github.com/DQM-BETA/omuletachou/pull/234

## Code Review (PR #234, rota `rapido` — CR leve, mas real) — APROVADO
Execução real (não leitura de diff), evidência completa postada como comentário no PR (`gh pr comment 234`):
- `npm ci` (dashboard): 945 pacotes, sem erro.
- `npm test` (Karma, `ChromeHeadless`, `--watch=false`): **140/140 SUCCESS** — sem regressão, bate com o baseline do Dev.
- `ng build` (produção): sucesso; únicos warnings são de budget pré-existentes (não relacionados a este diff).
- `npm run test:visual` (Playwright real, chromium, `webServer` subindo `ng serve` automaticamente): **8/8 passed (18.9s)** — specs rodaram de verdade, não só o script existindo. 8 screenshots gerados em `dashboard/screenshots/`; `login.png` e `products.png` inspecionados visualmente — Material Design aplicado, sidenav/tabelas/formulários estilizados, erro de API tratado via snackbar sem quebrar layout (consistente com `blockApiCalls`).
- Checklist de veto: compila e sobe (ok); integração real — specs navegam o app Angular real servido por `ng serve` (não mock), decisão `blockApiCalls`/`injectDummyAuth` justificada e coerente com `auth.guard.ts`/`auth.interceptor.ts` (Gate Visual = layout/CSS, não dado); conformidade com spec — CA-1 a CA-5 atendidos (CA-5 redirecionado para `README.md` por trava dura de permissão em `CLAUDE.md`, decisão razoável); sem teste-lixo — asserts usam `data-testid`/classes reais confirmadas no código-fonte; sem segredo commitado (`dummy-token-e2e-visual` é literal não-secreto); nenhuma ocorrência de `.first()`/`.nth()`/`.last()` em `dashboard/e2e/*.spec.ts`.
- Plugin `/code-review` (Anthropic): 0 comentários/reviews no PR no momento da checagem (`gh pr view --json comments,reviews`) — sem achados a incorporar.
- Nota fora de escopo (sem impacto na aprovação): o diff também carrega docs de outras issues (#223, #227–#231) já pendentes de sync `desenv→homolog` em `desenv` — puramente docs/estado.md, sem código de app.
- **PR #234 mesclado `desenv→homolog` via merge commit (`44f9df9`).**

## QA
- Validado em `homolog` (commit `44f9df9`, PR #234 mergeado). Branch sincronizada via `git fetch` + `git pull origin homolog` antes da validação.
- `docker compose build --no-cache dashboard` (sem cache) + `docker compose up -d dashboard` → build de produção sucesso, container saudável, `http://localhost:8081/` → 200 OK.
- `npm run test:visual` real (`SCREENSHOTS_DIR={docs_path}/screenshots`) → **8/8 passed**, screenshots reais gerados e substituídos na pasta `screenshots/` (evidência da rodada de QA).
- Gate Visual obrigatório do QA aplicado nas 8 screenshots: header/sidenav 1x em todas as telas, sem duplicação estrutural, sem CSS quebrado, mensagens de erro tratadas de forma estilizada (comportamento esperado — `blockApiCalls` aborta `/api/**` de propósito).
- Validação integrada (d3): login real via `POST /api/auth/login` (proxy nginx do container `dashboard` → API .NET real → Postgres real) → 200 + JWT; `GET /api/products` autenticado → 200 com 110 produtos reais.
- `npx ng test --watch=false --browsers=ChromeHeadless` → 140/140, sem regressão.
- CA-1 a CA-5 (especificação técnica): todos ✅ (ver `relatorio-qa.md`).
- Achado não bloqueante: `tsc --noEmit` na raiz aponta 3 erros de estilo (`noPropertyAccessFromIndexSignature`) em `playwright.config.ts`/`e2e/visual.spec.ts` — fora do gate de `ng build`, mesmo padrão pré-existente em `website`. Não impede aprovação.
- **Status: APROVADO.** Relatório completo em `relatorio-qa.md`.

## Líder Técnico (PR de release)
- Evidências do QA (`estado.md`, `relatorio-qa.md`, `screenshots/.last-run.json`) commitadas em `desenv` (mesmo padrão já usado pelo Code Review) — `homolog`/`main` são protegidas por branch protection (`enforce_admins:true`, PR obrigatório), sem push direto possível.
- PR #235 (`homolog→main`, merge commit): https://github.com/DQM-BETA/omuletachou/pull/235 — referencia Issue #155, sub-issue #232, PR #234 e `relatorio-qa.md`.
- **PR #235 mergeada em main via merge commit (`0940a3ca`).**

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Notas |
|---|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | — | — | — | Rota rapido |
| 2 | Refinamento (rapido) — spec técnica + sub-issue #232 | LT | Sonnet | — | — | — | Padrão website replicado |
| 3 | Dev (sub-issue #232, PR #233) | Dev Angular | Sonnet | — | — | — | Implementação + blockApiCalls |
| 4 | Merge PR #233 (squash) + PR #234 (desenv→homolog) | LT | Sonnet | — | — | — | Merge commit homolog |
| 5 | Code Review (PR #234 — build/boot/testes reais, merge homolog) | Code Review | Sonnet | — | — | — | Execução real 8/8 visual |
| 6 | QA (validação em homolog) | QA | Sonnet | — | — | — | Gate Visual + integração |
| 7 | PR release (homolog→main, PR #235) | LT | Sonnet | — | — | — | Merge main concluído |
| 8 | Gate 2 (merge main + consolidação) | Coordenador | Haiku | — | — | — | Merge 0940a3ca, Issue fechada |

**Tempo decorrido:** 2026-08-14 13:45 → 2026-08-19 14:50 = **5 dias, 1 hora e 5 minutos** (~121 horas)

_Atualizado: 2026-08-19 — Gate 2 concluído. Merge para main realizado._
