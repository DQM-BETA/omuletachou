issue: 132
titulo: "fix: Triggers de collector individual retornam 500 não tratado sem credenciais"
etapa_atual: Dev concluido — PR feature->desenv aberto (rota rapido)
ultimo_agente: dev-dotnet
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
pr_homologacao: ~
pr_release: ~
pr_feature: 135
branch_feature: fix/132-collector-error-handling
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Historico
- 2026-08-03 — Dev .NET: worktree `.worktrees/132-fix-collector-errors` (branch `fix/132-collector-error-handling`, base `desenv`). TDD: RED confirmado (3 testes de integracao novos em `JobsTriggerTests.cs` reproduzindo 500 sem credenciais para amazon/mercadolivre/shopee) -> GREEN (`JobsController.cs`: try/catch `InvalidOperationException` nos 3 endpoints de trigger individual, retornando `BadRequest(new { message = "Credenciais não configuradas para {plataforma}: {detalhe}" })`, seguindo o padrao de `PushController`/`ProductsController`/`QueueController`). Suite completa: 309/309 testes passando. Validacao manual via boot Docker real (build + `docker compose -p issue132 up db api`, sem credenciais de plataforma configuradas, projeto/portas isolados do worktree de outra sub-issue rodando em paralelo): os 3 endpoints, com JWT valido, retornaram 400 estruturado (evidencia no corpo do PR). Worktree removido apos push. PR `fix/132-collector-error-handling` -> `desenv` aberto: #135 (sem merge, conforme escopo).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | Dev (fix + testes + PR) | dev-dotnet | sonnet | 61094 | 46 | 358s |
