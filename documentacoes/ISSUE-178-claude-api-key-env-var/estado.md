---
issue: 178
titulo: "bug: Claude__ApiKey nunca chega no container da API (docker-compose.yml/.env.example/runbook incompletos)"
etapa_atual: Dev
rota: rapido
ultimo_agente: coordenador
openspec_change: ~
tech_stacks:
  - dotnet
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-178-claude-api-key-env-var
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: 5317053323
createdAt: "2026-08-17"
---

## Contexto

Bug de infra: variáveis de ambiente `Claude__ApiKey` e `Claude__Model` nunca chegam no container da API.

## Achado relacionado

`claude.api_key`/`claude.model` seedados em `app_settings` (ids 20/21) são código morto — nenhum lugar do sistema lê. Menção apenas; remoção é decisão de escopo do Dev.

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
