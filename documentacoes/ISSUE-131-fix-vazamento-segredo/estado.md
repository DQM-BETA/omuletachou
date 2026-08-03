issue: 131
titulo: "fix: Vazamento de segredo curto no mascaramento de Settings"
etapa_atual: Dev concluido — PR feature->desenv aberto (aguardando merge do LT)
ultimo_agente: dev
openspec_change: ~
tech_stacks:
  - dotnet
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-131-fix-vazamento-segredo
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Historico
- Dev (rota rapido): corrigido `SettingsMasker.Mask` em `backend/src/AfiliadoBot.Api/Settings/SettingsMasker.cs` —
  quando `value.Length <= 4`, `last4` virava o valor inteiro e o segredo completo aparecia em claro apos os
  16 asteriscos. Agora, para `Length <= 4`, mascara totalmente (`16 + Length` asteriscos, nenhum char real
  revelado); comportamento de valores longos preservado (16 asteriscos + ultimos 4 reais). TDD: RED confirmado
  (4 casos falhando antes do fix) -> GREEN (16/16 testes de `SettingsMaskerTests` passando). Suite completa:
  310/310 (100%). Validado manualmente ponta-a-ponta via `docker compose up -d --build db api` (boot sem
  excecoes) + `PUT`/`GET /api/settings/{key}` reais: `"abcd"` -> `"********************"`,
  `"a"` -> `"*****************"`, valor longo -> `"****************a1b2"` (preservado). PR
  `fix/131-secret-masking` -> `desenv` aberto: https://github.com/DQM-BETA/omuletachou/pull/134. Worktree
  `.worktrees/131-fix-masking` removido ao final.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | Dev (fix + TDD + validacao Docker + PR) | dev-dotnet | sonnet | 48620 | 32 | 313s |
