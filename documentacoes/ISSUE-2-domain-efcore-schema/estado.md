---
issue: 2
titulo: feat: Domain, EF Core e Schema de Banco
repo: omuletachou
rota: normal
etapa_atual: Gate 2 — aguardando aprovação do Gerente para merge em main
ultimo_agente: lt
openspec_change: ~
tech_stacks: [".NET 8", "EF Core 8", "PostgreSQL 16", "C#"]
repos:
  omuletachou: "repos/omuletachou"
repo_path: "repos/omuletachou"
docs_path: "repos/omuletachou/documentacoes/ISSUE-2-domain-efcore-schema"
openspec_path: ~
sub_issues: [25, 26]
desenv_tasks_merged: [25, 26]
sub_issues_frontend: {}
pr_homologacao: 29
pr_release: 31
code_review_homolog_pr: ~
qa_status: aprovado
figma_url: ~
blockers: nenhum
---

## Contexto
Issue de implementação do domínio, contexto EF Core e schema de banco de dados para o projeto AfiliadoBot.

**Dependência:** Issue #1 (Setup Infraestrutura Base)

**Objetivo:** Criar todas as entidades do domínio, contexto EF Core, migrations iniciais e seeds de configuração.

### Artefatos entregáveis
- Entidades em `AfiliadoBot.Domain/Entities/`: `Product`, `PublicationQueue`, `AppSetting`, `PushSubscription`, `PublicationLog`
- Enums em `AfiliadoBot.Domain/Enums/`: `Platform`, `SocialNetwork`, `ProductStatus`, `PublicationStatus`
- `AppDbContext` com configurações Fluent API
- Migration inicial com todas as tabelas
- Seeds de `app_settings` com 30 campos (>= 25 exigidos)
- Interfaces de domínio: `IPlatformCollector`, `ISocialPublisher`, `IMediaStorage`, `IAiService`
- Testes unitários de entidades e validações

### Sub-issues
- #25 — T-01: Domain — Entidades, enums e interfaces (stack:dotnet)
- #26 — T-02: Infrastructure — DbContext, Migrations e Seeds (stack:dotnet)

### Critérios de aceite
- Given as migrations aplicadas When verificar o banco Then todas as tabelas existem com os campos corretos
- Given os seeds aplicados When consultar `app_settings` Then existem >= 25 registros
- Given `dotnet test` Then testes de domínio passam sem erro

## Histórico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | PM Fase 1 | PM | concluido |
| 2 | PM Fase 2 | PM | concluido |
| 3 | Refinamento LT | LT | concluido |
| 4 | Dev T-01 #25 (PR#27) | Dev .NET | concluido |
| 5 | Dev T-02 #26 (PR#28) | Dev .NET | concluido |
| 6 | PR desenv→homolog (#29) | LT | concluido |
| 7 | QA | QA | concluido |
| 8 | PR homolog→main | LT | concluido |

## Custo (ledger)
<!-- Preenchido pelo orquestrador a cada etapa -->
