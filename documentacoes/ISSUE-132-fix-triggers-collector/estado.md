issue: 132
titulo: "fix: Triggers de collector individual retornam 500 não tratado sem credenciais"
etapa_atual: Merged em desenv — PR de homologacao compartilhado (#136, junto com #131) — aguardando Code Review + QA + Gate 2
ultimo_agente: lt
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
code_review_homolog_pr: ~
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
