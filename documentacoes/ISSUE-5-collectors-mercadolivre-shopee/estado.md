---
issue: 5
titulo: feat: Collectors MercadoLivre e Shopee
rota: normal
etapa_atual: PM Fase 1 — aguardando resposta Gate 1 (Gerente)
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-5-collectors-mercadolivre-shopee
openspec_path: repos/omuletachou/openspec/changes/ISSUE-5-collectors-mercadolivre-shopee
openspec_change: ~
tech_stacks:
  - .NET 8
  - HttpClient
  - OAuth2
  - GraphQL manual
  - HMAC-SHA256
ultimo_agente: pm
sub_issues: []
desenv_tasks_merged: []
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
blockers: nenhum
---

## Contexto
- Dependência: Issues #2 (Domain/EF Core/Schema), #3 (ClaudeAiService), #4 (AmazonCollector pattern)
- Objetivo: implementar collectors para plataformas MercadoLivre e Shopee, reutilizando o padrão IPlatformCollector, ExternalId, upsert estabelecido no AmazonCollector
- Stack: .NET 8 + HttpClient + OAuth2 (MercadoLivre) + HMAC-SHA256 (Shopee) + GraphQL manual se necessário
- Atenção: ambas as plataformas exigem credenciais/tokens de produção — reaproveita infra de credentials do projeto
- Task breakdown: awaiting PM Fase 1 (pode resultar em 1 ou 2 sub-issues, dependendo de ambiguidade arquitetural ou stack separada)
- PRD inicial em `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/prd.md`
- Perguntas de Gate 1 postadas na Issue #5 (comentário)

## Histórico de Etapas
Criada em 2026-07-06 pelo Coordenador.

| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparação Issue | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
