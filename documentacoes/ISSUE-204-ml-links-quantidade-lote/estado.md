issue: 204
titulo: feat: permitir escolher a quantidade de produtos por lote na tela de importação de links ML (limite real da ferramenta oficial do ML)
etapa_atual: Aguardando Gate 2 (Gerente) — PR release aberto
ultimo_agente: lider-tecnico
openspec_change: ~
tech_stacks:
  - angular
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-204-ml-links-quantidade-lote/
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_feature: 205
pr_homologacao: 206
pr_release: 207
code_review_homolog_pr: 206
qa_status: aprovado — ver relatorio-qa.md e ledger etapa 4
figma_url: ~
blockers: nenhum
rota: rapido
status_comment_id: ~

## Notas
- PR #205 (feature/ISSUE-204-quantidade-lote-ml → desenv) merged via squash em 2026-08-18 (commit 045c60d99928493f8343667079184cd824094395).
- Testes reportados pelo Dev: 129/129 unitários verdes, build de produção OK, `ng serve` validado.
- PR #206 (desenv → homolog) criado via merge commit em 2026-08-18. Aguarda Code Review + QA + Gate 2. NÃO mesclar sem aprovação.
- Correção: limite real de 30 URLs por vez na ferramenta oficial do Mercado Livre (descoberto ao vivo pelo Gerente); campo de quantidade por lote editável, respeitado na cópia e na importação de volta.
- **Code Review (2026-08-18): APROVADO.** PR #206 mesclado `desenv→homolog` via merge commit `c95a0ceec2d5243398383bb5bd6ffd393eab4f52`. Evidência completa no comentário do PR. Resumo:
  - Suíte Karma: 129/129 verdes (`ng test --watch=false --browsers=ChromeHeadless`). Cobertura com `--code-coverage`: statements 92.41%, branches 82.05%, functions 91.61%, lines 92.6% (todas ≥80%).
  - `ng build` (produção) OK.
  - Docker rebuild sem cache (`docker compose build --no-cache api dashboard`, containers antigos já pegaram gente de surpresa nesta sessão) + boot real: `db`/`api`/`dashboard` healthy, `GET /health` 200, dashboard HTTP 200.
  - Confirmado que o bundle servido pelo container (`chunk-7GNBTIOG.js`, via `curl http://localhost:8081/...`) contém de fato o código novo (`Quantidade por lote`, `batch-size-input`, hint do limite de 30) — não é imagem stale.
  - **Integração real (não mock-only):** login real (`/api/auth/login`) + `GET /api/products?status=AwaitingAffiliateLink&pageSize=200` contra os 111 produtos ML reais em `AwaitingAffiliateLink` no Postgres local. Simulado o fluxo de lote (batchSize=2 → primeiros 2 produtos) via `POST /api/products/affiliate-links/import` com apenas 2 itens: `{"imported":2,"skipped":[]}`. Confirmado via SQL direto no Postgres que **só** os 2 produtos do lote mudaram de status (`AwaitingAffiliateLink`→`1`, com `affiliate_link` setado) e o 3º produto (fora do lote) permaneceu intocado (`status=6`, `affiliate_link` vazio). `totalItems` de pendentes: 111→109, exatamente os 2 importados.
  - Campo de quantidade: setter (`batchSize`) não tem limite superior hardcoded — aceita qualquer valor inteiro positivo (`Math.floor(parsed) : 0` se inválido/≤0), conforme exigido (30 é só sugestão inicial, não hardcoded na lógica). `displayedProducts` (subconjunto de `products`) é a fonte única usada tanto para cópia de URLs quanto para o payload de importação — sem duplicação de lógica.
  - **Achado informativo (fora do escopo desta PR, não bloqueante):** `GET /api/products` tem `MaxPageSize=100` hardcoded no backend (`PaginationExtensions.cs`), então mesmo pedindo `pageSize=200` o dashboard só recebe no máximo 100 produtos por carregamento — dos 111 reais em `AwaitingAffiliateLink`, só 100 aparecem na tela por vez. Pré-existente à Issue #204 (endpoint de listagem é da Issue #185), não introduzido por este PR. Registrado como sugestão de melhoria, não impede aprovação desta PR.
  - `.first()`/`.nth()`/`.last()`: N/A — PR não toca specs E2E (dashboard usa apenas testes de componente Karma; Playwright/e2e só existe em `website/`, não tocado neste diff).
  - `/code-review` (plugin Anthropic): 0 comentários/reviews no PR — nada a incorporar.
  - Sem segredos commitados, sem violação OWASP identificável (campo numérico simples, sem superfície de injeção nova).
  - **Efeito colateral da validação ao vivo:** 2 dos 111 produtos reais (`920cc7b3-b1c2-46ff-9fd9-5b7c6aed9cb4`, `5e910e71-0d33-4d02-ae6e-03ff4172623f`) foram marcados como importados com link de afiliado de teste (`CR_TEST_BATCH_1`/`CR_TEST_BATCH_2`) no ambiente local durante a validação de integração — não revertido (não há endpoint de rollback; consistente com a prática já usada em CRs anteriores desta squad que disparam fluxos reais). QA deve estar ciente ao contar produtos pendentes.
  - `etapa_atual` → QA. Apto a seguir.
- **PR release #207 (homolog→main) criado em 2026-08-18** pelo Líder Técnico. Corpo do PR descreve o limite real de 30 URLs/vez da ferramenta oficial do ML e a correção (campo de quantidade por lote editável), referencia PRs #205 e #206 e `relatorio-qa.md`. **NÃO mesclado — aguarda aprovação humana do Gerente (Gate 2).**

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|------------|-----------|
| 1 | Preparar Issue | coordenador | haiku | 21439 | 4 | 57s |
| 2 | Merge PR205 + PR206 | lider-tecnico | sonnet | 33339 | 14 | 72s |
| 3 | Code Review (PR #206, homologação) | code-review | sonnet | 98546 | 62 | 520s | Aprovado, ver Notas para evidência completa |
| 4 | QA (homolog) | qa | sonnet | 86286 | 40 | 502s | **APROVADO.** homolog sincronizado (fast-forward, commit `c95a0ce...`). 129/129 testes, cobertura ≥80% em todas as métricas. Validação real com N=3 (valor arbitrário, provando ausência de hardcode) via import real, confirmado no Postgres: pareamento correto, produto fora do lote intocado. Pendentes 109→106 (efeito colateral esperado da validação ao vivo, mesma prática do CR). E2E N/A (sem Playwright no dashboard). Achado informativo não-bloqueante já registrado pelo CR (MaxPageSize=100). Relatório: `relatorio-qa.md`. Comentário: https://github.com/DQM-BETA/omuletachou/issues/204#issuecomment-5331958841
| 5 | PR release (homolog→main) | lider-tecnico | sonnet | pendente | pendente | pendente |
