issue: 132
titulo: "fix: Triggers de collector individual retornam 500 não tratado sem credenciais"
etapa_atual: Code Review aprovado — PR #136 mergeado em homolog; aguardando QA
ultimo_agente: code-review
openspec_change: ~
tech_stacks:
  - dotnet
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-132-fix-triggers-collector
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: 136
pr_release: ~
pr_feature: 135
branch_feature: fix/132-collector-error-handling (deletada apos merge)
code_review_homolog_pr: 136
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Historico
- 2026-08-03 — Dev .NET: worktree `.worktrees/132-fix-collector-errors` (branch `fix/132-collector-error-handling`, base `desenv`). TDD: RED confirmado (3 testes de integracao novos em `JobsTriggerTests.cs` reproduzindo 500 sem credenciais para amazon/mercadolivre/shopee) -> GREEN (`JobsController.cs`: try/catch `InvalidOperationException` nos 3 endpoints de trigger individual, retornando `BadRequest(new { message = "Credenciais não configuradas para {plataforma}: {detalhe}" })`, seguindo o padrao de `PushController`/`ProductsController`/`QueueController`). Suite completa: 309/309 testes passando. Validacao manual via boot Docker real (build + `docker compose -p issue132 up db api`, sem credenciais de plataforma configuradas, projeto/portas isolados do worktree de outra sub-issue rodando em paralelo): os 3 endpoints, com JWT valido, retornaram 400 estruturado (evidencia no corpo do PR). Worktree removido apos push. PR `fix/132-collector-error-handling` -> `desenv` aberto: #135 (sem merge, conforme escopo).
- 2026-08-03 — LT (merge de sub-issue / rota rapido): `git pull origin desenv` limpo (sem pendencias). Revisado diff do PR #135 (`gh pr diff 135`) — try/catch `InvalidOperationException` nos 3 triggers individuais + 3 testes novos, consistente com o historico do dev. Confirmado `mergeable: MERGEABLE` / `mergeStateStatus: CLEAN`. Merge squash de `fix/132-collector-error-handling` -> `desenv` (PR #135), branch remota deletada. `git pull origin desenv` fast-forward (941dbe3..5cc212c), sem conflitos. Comentario postado na Issue #132 informando o merge (issue mantida aberta).
  **Decisao de homologacao:** o PR #136 (`desenv` -> `homolog`, aberto para a issue #131) ja existia com `head=desenv` (branch, nao SHA fixo) e ainda nao tinha sido processado por Code Review/QA. Ao mergear #135 em desenv, o PR #136 automaticamente absorveu o novo commit (confirmado: de 15 para 16 commits, `mergeable: MERGEABLE` / `mergeStateStatus: CLEAN` apos o merge). Como #131 e #132 sao fixes pequenos e independentes, e #136 ainda estava aberto e limpo, optou-se por **NAO criar um PR #137 separado** — #136 agora cobre ambos os fixes (#131 + #132) no mesmo ciclo de homologacao, evitando dois PRs `desenv->homolog` concorrentes (o que geraria conflito/duplicacao ao tentar mergear qualquer um dos dois). Este e o cenario "desenv ja avancou, #136 ainda nao processado" previsto no criterio de decisao.
  `pr_homologacao` atualizado para 136 (compartilhado com #131). Nenhum PR novo criado nesta invocacao.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | Dev (fix + testes + PR) | dev-dotnet | sonnet | 61094 | 46 | 358s |
| 3 | LT (merge PR#135->desenv, decisao de consolidar homologacao em #136) | lt | sonnet | 37476 | 14 | 98s |
| 4 | Code Review — validacao final PR #136 (compartilhada com #131) | code-review | sonnet | 62979 | 46 | 359s |
## Code Review — PR #136 (validacao final)
- `git pull origin desenv`: já atualizado (HEAD do PR #136 = `desenv`). `dotnet test`: **318/318 passando** (100%).
- Boot Docker real: `docker compose up -d --build db api` (`.env` local temporario, nunca commitado) — build da imagem `omuletachou-api` OK, containers `afiliado_db` e `afiliado_api` **healthy**, sem exceção de boot/DI.
- Validação ao vivo do fix (tratamento de credenciais ausentes nos triggers individuais de collector): login via `/api/auth/login`, sem nenhuma credencial de plataforma configurada (confirmado via `GET /api/settings` — todas as chaves `amazon.*`/`mercadolivre.*`/`shopee.*` nulas). `POST /api/jobs/collector/{amazon|mercadolivre|shopee}/trigger`, todos os 3 retornaram **400** estruturado (não 500):
  - amazon: `{"message":"Credenciais não configuradas para amazon: Credenciais da Amazon (access_key, secret_key, partner_tag) ausentes ou invalidas."}`
  - mercadolivre: `{"message":"Credenciais não configuradas para mercadolivre: Credencial ausente: mercadolivre.client_id"}`
  - shopee: `{"message":"Credenciais não configuradas para shopee: Credencial ausente: shopee.app_id"}`
- Achados do `/code-review` (plugin) no PR #136: nenhum achado relativo a esta issue (os 2 achados registrados foram sobre o fix de #131/mascaramento); comentário de re-verificação final confirma ausência de pendências.
- Checklist de veto: sem segredos commitados no diff (`.env` gerado localmente para o teste, gitignored, removido ao final); código aderente ao CLAUDE.md; integração real (não mock-only) — endpoints exercitados ponta-a-ponta contra Postgres real em container, sem credenciais mockadas.
- Containers derrubados (`docker compose down -v`) e `.env` local removido ao final. Repo checked out em `desenv`.
- **Veredito: APROVADO.** Merge `desenv` -> `homolog` (PR #136, merge commit) autorizado.
