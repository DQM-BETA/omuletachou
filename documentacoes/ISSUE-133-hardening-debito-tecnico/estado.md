---
issue: 133
titulo: "chore: Hardening e débito técnico — auditoria completa 2026-08-03"
etapa_atual: Backlog
ultimo_agente: coordenador
rota: backlog
status_comment_id: 5178622317
openspec_change: ~
tech_stacks:
  - dotnet
  - angular
  - infra
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-133-hardening-debito-tecnico
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
createdAt: "2026-08-04"
closedAt: ~
---

## Descrição
Consolidação de achados não-bloqueantes da auditoria completa de código (Code Review) + teste funcional (QA) pedida pelo Gerente em 2026-08-03. Achados categorizados por tema:

- **Segurança**: DELETE sem rate-limiting, senha com comparação não tempo-constante, SSRF, header forwarding
- **Dependências vulneráveis**: Angular, next-pwa, Newtonsoft.Json com vulnerabilidades High
- **Infraestrutura**: .gitignore bloqueando .dockerignore, deploy sem healthcheck, imagens sem pin de versão
- **Qualidade de código**: Código morto (Class1.cs, testes boilerplate)
- **Lacuna funcional**: ProcessorJob com falsa sensação de "publicado", Facebook credentials não seedadas

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | — | — | — |

**Total acumulado:** — tokens · — min proc. (merge pendente)

---
_Criado: 2026-08-04 — Coordenador_
