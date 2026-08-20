# Relatório QA — ISSUE-227: Exibir data/hora da última execução de cada job na tela Jobs

**Status: ✅ APROVADO**

**Branch validada:** `homolog` (sincronizada via `git fetch` + `git pull` antes do teste).
Commit confirmado em `homolog`: `1c5e020e` (merge do PR #240, `desenv→homolog`), presente em
`git log --oneline -5` no momento da validação.

## Ambiente
- `docker compose build --no-cache api dashboard` a partir de `homolog` — build OK (backend e
  Angular, `ng build --configuration=production`).
- `docker compose up -d --force-recreate api dashboard db` — 3 containers `healthy`/`Up`.
  Migration `AddJobRuns` aplicada automaticamente no boot (log confirmado, sem erro).
- Login real via `POST /api/auth/login` (usuário seed `.env`) → 200 + JWT válido.

## Testes automatizados
| Suíte | Resultado |
|---|---|
| Backend (`dotnet test`) | **473/473 aprovados** (inclui `JobRunTrackerTests`, `JobsControllerTests`, `CollectorJobTests`, `ProcessorJobTests`, `PublisherJobTests`) |
| Frontend unitário (`ng test --code-coverage`) | **148/148 aprovados** — cobertura: statements 92.52%, **branches 81.3%** (≥80% ok), functions 92.04%, lines 92.66% |
| `tsc --noEmit -p tsconfig.app.json` (código-fonte da app) | Sem erros |
| `tsc --noEmit -p tsconfig.json` (inclui `e2e/`) | 3 erros pré-existentes em `playwright.config.ts`/`e2e/visual.spec.ts` (TS4111, index signature), introduzidos na ISSUE-232 e não tocados por esta issue — fora do escopo de #227, não bloqueiam |
| Playwright `test:visual` (`SCREENSHOTS_DIR={docs_path}/screenshots npm run test:visual`) | **8/8 aprovados** |

## Gate visual (screenshots arquivadas em `documentacoes/ISSUE-227-exibir-data-hora-ultima-execucao-jobs/screenshots/`)
- Header/sidebar (`omuletachou`) visível exatamente 1x em todas as 8 telas, sem duplicação.
- Nenhum componente estrutural duplicado.
- `jobs.png` (suíte padrão, API mockada/bloqueada): 6 cards, todos em estado "Nenhuma execução
  disparada ainda" / "Nenhuma execução ainda" — consistente (suíte não injeta dados reais, apenas
  valida shell/layout).
- Sem `ux-ui-spec.md` dedicado para #227 (issue não passou por fase UX/UI — reaproveita tela Jobs
  existente); paleta/tipografia idênticas às demais telas já aprovadas (products/queue/etc.).

## Validação integrada real (ponta a ponta, dado real do backend — não mock)
Login real → disparo real de jobs via API → confirmação via `GET /api/jobs/last-executions` →
confirmação visual no dashboard real (login real na UI, não suíte mockada):

1. Estado real observado no ambiente (dados de execuções reais, incluindo uma automática do
   Hangfire capturada durante a validação):
   - `collector` → `success`, início/fim reais.
   - `collector-amazon` → `failed`, com `errorMessage` "Credenciais da Amazon ... ausentes ou
     invalidas." (disparo real, sem credencial configurada).
   - `collector-mercadolivre` → `status: null` (nunca executado).
   - `processor` → `success`.
   - `publisher` → `success`, **iniciado automaticamente pelo Hangfire** (`started_at
     2026-08-19 18:00:11`, sem disparo manual do QA) — confirma CA 4.2/5.1 (execução automática
     também rastreada pelo mesmo `JobRunTracker`).
2. Disparei `POST /api/jobs/collector/shopee/trigger` (sem credencial Shopee configurada) duas
   vezes seguidas:
   - 1ª chamada: `collector-shopee` passou de `status: null` → `failed`, `startedAt`/`finishedAt`
     reais, `errorMessage: "Credencial ausente: shopee.app_id"` (CA 3.1→2.1, CA 4.1).
   - 2ª chamada: novo registro criado; consulta direta ao Postgres (`SELECT * FROM job_runs`)
     confirmou **2 linhas** para `job_name = CollectorShopee` (17:58:01 e 17:58:13), nenhuma
     sobrescrita — CA 4.1 confirmado no nível de persistência.
   - `GET /api/jobs/last-executions` após a 2ª chamada retornou **apenas** o registro mais recente
     (17:58:13), enquanto os dois continuam no banco — CA 4.3 confirmado.
3. Consulta direta ao Postgres (`job_runs`) confirmou que a fonte de dados é a tabela própria da
   aplicação (schema `public`, não `hangfire.*`) — CA 5.1/5.2 confirmado por inspeção do schema
   (`JobRunConfiguration`, migration `AddJobRuns`) e pela consulta real.
4. Login real na UI (`/login` com usuário seed) + navegação real para `/jobs` (script Playwright
   ad-hoc, sem mock de API, apontando para os containers reais em `localhost:8081`) — screenshot
   confirma visualmente:
   - **Sucesso** (ícone verde `check_circle` + "Sucesso") nos cards Collector (geral), Processor,
     Publisher, com Início/Fim formatados `dd/MM/yyyy HH:mm`.
   - **Falha** (ícone vermelho `error` + "Falha", cor distinta) nos cards Collector — Amazon e
     Collector — Shopee, com a mensagem de erro visível abaixo do timestamp — nunca confundida com
     sucesso.
   - **"Nenhuma execução ainda"** no card Collector — MercadoLivre (nunca executado), sem erro nem
     data mal formatada.
   - Refetch automático confirmado: após o `trigger()` via UI/API, o card refletiu o novo estado
     sem F5 (verificado tanto via API quanto via nova carga da tela).

## Critérios de aceite — tabela de rastreamento

| Cenário (criterios-aceite.md) | Coberto por | Evidência |
|---|---|---|
| 1.1 — card mostra data/hora real do backend | `JobsControllerTests`, `jobs.component.spec.ts` + validação integrada | Screenshot real + `GET /last-executions` |
| 1.2 — persiste entre sessões | Implementação (sem cache local, sempre busca no `ngOnInit`) + persistência Postgres confirmada (dados sobreviveram a `--force-recreate` dos containers, volume nomeado) | Consulta `job_runs` |
| 1.3 — status de sucesso visível junto à data/hora | `jobs.component.spec.ts` + screenshot real | `jobs-real-data.png` (Collector geral/Processor/Publisher) |
| 2.1 — falha comunicada claramente | Disparo real (`collector-shopee`) + screenshot | Ícone/cor de falha + mensagem de erro |
| 2.2 — falha não confundida com sucesso | Template (`*ngIf` mutuamente exclusivo) + screenshot | Classes CSS distintas `--success`/`--failed` |
| 3.1 — "Nenhuma execução ainda" | `GET /last-executions` (`collector-mercadolivre` com `status: null`) + screenshot | Card MercadoLivre |
| 4.1 — novo registro por execução, sem sobrescrever | `JobRunTrackerTests` + 2 disparos reais do Shopee | 2 linhas em `job_runs` |
| 4.2 — histórico cobre manual e automático | Disparo manual (Shopee, via API) + execução automática real do Hangfire (Publisher, 18:00:11) capturadas na mesma tabela | `job_runs` |
| 4.3 — tela mostra só a mais recente, histórico completo persiste | `GET /last-executions` após 2º disparo do Shopee | Retornou só 17:58:13; banco tem as 2 linhas |
| 5.1 — mesma entidade para manual e automático | Inspeção `JobRunTracker`/`CollectorJob`/`JobsController` + evidência real acima | `especificacao-tecnica.md` §3, `design.md` §2 |
| 5.2 — consulta não depende de tabelas do Hangfire | Inspeção `JobsController.GetLastExecutions` (usa `AfiliadoBotDbContext.JobRuns`, schema `public`) | Código + query real ao Postgres |

## Issues encontradas
Nenhuma. 100% dos critérios de aceite validados com evidência de execução real (não apenas
testes unitários/mock).

## Observação (não bloqueante)
- `tsc --noEmit` no `tsconfig.json` completo (incluindo `e2e/`) acusa 3 erros de tipo
  pré-existentes (`TS4111`, index signature em `process.env`), introduzidos na ISSUE-232 e não
  tocados nesta issue. Não afeta build de produção (`tsconfig.app.json` limpo) nem os testes
  Playwright (rodaram e passaram normalmente). Registrado para eventual limpeza futura, fora do
  escopo de #227.

---
*QA executado em 2026-08-19. Ambiente: `homolog` local via Docker Compose, rebuild `--no-cache`.*
