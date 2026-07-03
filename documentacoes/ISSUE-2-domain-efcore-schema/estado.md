---
issue: 2
titulo: feat: Domain, EF Core e Schema de Banco
repo: omuletachou
rota: normal
etapa_atual: Gate 1 — aguardando resposta do Gerente
ultimo_agente: pm-analista-negocios
openspec_change: ~
tech_stacks: [".NET 8", "EF Core 8", "PostgreSQL 16", "C#"]
repos:
  omuletachou: "repos/omuletachou"
repo_path: "repos/omuletachou"
docs_path: "repos/omuletachou/documentacoes/ISSUE-2-domain-efcore-schema"
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
---

## Contexto
Issue de implementação do domínio, contexto EF Core e schema de banco de dados para o projeto AfiliadoBot.

**Dependência:** Issue #1 (Setup Infraestrutura Base)

**Objetivo:** Criar todas as entidades do domínio, contexto EF Core, migrations iniciais e seeds de configuração.

### Artefatos entregáveis
- Entidades em `AfiliadoBot.Domain/Entities/`: `Product`, `PublicationQueue`, `AppSetting`, `PushSubscription`
- Enums em `AfiliadoBot.Domain/Enums/`: `Platform`, `SocialNetwork`, `ProductStatus`, `PublicationStatus`
- `AppDbContext` com configurações Fluent API
- Migration inicial com todas as tabelas
- Seeds de `app_settings` com 25+ campos
- Interfaces de domínio: `IPlatformCollector`, `ISocialPublisher`, `IMediaStorage`, `IAiService`
- Testes unitários de entidades e validações

### Critérios de aceite
- Given as migrations aplicadas When verificar o banco Then todas as tabelas existem com os campos corretos
- Given os seeds aplicados When consultar `app_settings` Then existem >= 25 registros
- Given `dotnet test` Then testes de domínio passam sem erro

## Histórico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | PM Fase 1 | PM | concluido |

## Custo (ledger)
<!-- Preenchido pelo orquestrador a cada etapa -->
