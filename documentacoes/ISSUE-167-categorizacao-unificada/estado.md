---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Refinamento Técnico (Arquitetura concluída — Líder Técnico escreve especificação técnica)
ultimo_agente: arquiteto-engenheiro
rota: backlog
openspec_change: repos/omuletachou/openspec/changes/issue-167-categorizacao-unificada
tech_stacks:
  - dotnet
  - nodejs
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-167-categorizacao-unificada
openspec_path: repos/omuletachou/openspec/changes/issue-167-categorizacao-unificada
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: IC_kwDOTMlfyM8AAAABO7lC2w
createdAt: 2026-08-14
---

## Resumo
Demanda `backlog`: categorização unificada de produtos (Category + Subcategory) e remoção da distinção de plataforma (Amazon/MercadoLivre/Shopee) no site público. PM Fase 1 (validação técnica + levantamento) e Gate 1 (respostas do Gerente) concluídos na Issue. PM Fase 2 concluída: PRD (`proposal.md`) e critérios de aceite (Given/When/Then) escritos, incorporando todas as decisões do Gate 1.

## Decisões do Gate 1 (Gerente) incorporadas ao PRD
1. Taxonomia v1 fechada: 9 categorias, 3-5 subcategorias cada (~35 subcategorias). `Category`/`Subcategory` = VARCHAR livre (config versionada, não schema/enum).
2. Sem recategorização retroativa — só produtos novos a partir da mudança. Volume residual em "Geral" pós-lançamento é backlog separado.
3. Arquitetura de 2 camadas: dicionário (camada 1) roda na coleta (`CollectAsync`, sem custo de IA); fallback IA (camada 2) permanece restrito ao `ProcessorJob`, só para produtos aprovados (`Status == Queued`) — NÃO combinado com `ScoreProductAsync`. Teto de gasto: `claude.monthly_budget_limit_brl` em `app_settings`, default R$30/mês, desativa camada 2 automaticamente ao estourar (scoring/legenda sempre ativos).
4. Ordenação padrão continua por `AiScore` — novos filtros/ordenações são opcionais.
5. Remoção de `Platform` do DTO público é higiene de contrato de dados (não expor estratégia de curadoria por plataforma via scraping), não visual — confirmado que não há badge hoje. `Platform` continua interno/dashboard/AffiliateLink.

## Documentação produzida (PM Fase 2)
- `openspec/changes/issue-167-categorizacao-unificada/proposal.md` — PRD completo (objetivo, usuários, casos de uso, regras de negócio, integrações, restrições, definição de pronto).
- `documentacoes/ISSUE-167-categorizacao-unificada/criterios-aceite.md` — critérios Given/When/Then por funcionalidade (migration, dicionário na coleta, fallback IA, orçamento/app_settings, remoção de Platform, novos endpoints, frontend).

## Avaliação de ambiguidade arquitetural — PROSSEGUE PARA O ARQUITETO
A sequência de negócio (dicionário na coleta, IA restrita ao pós-aprovação, sem combinar com scoring) já foi decidida pelo Gerente — não é mais ambígua. Mas restam decisões técnicas de integração/infraestrutura sem resposta única de negócio, encaminhadas ao Arquiteto:
1. Onde calcular/persistir o custo estimado por chamada Claude para o contador de orçamento em `app_settings` (granularidade, "cofre" do contador).
2. Estrutura de índices compostos para os novos filtros combináveis (`category`, `subcategory`, `sale_price`, `discount_pct`).
3. Convivência ou substituição da rota atual `/api/public/deals/category/{categoria}` frente ao novo endpoint com querystring.

## Próximos passos
- Arquiteto: completar `design.md` com as 3 decisões técnicas acima.
- LT: task breakdown (tasks.md) e criação de sub-issues, após o design do Arquiteto.
- UX/UI: mockups de barra de filtros (dropdowns dependentes, slider, botões de desconto, seletor de ordenação) — pode rodar em paralelo/antes dos devs, conforme máquina de estados da rota normal.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 49906 | 22 | 158s |
| 2 | PM Fase 1 (validação técnica + levantamento, Gate 1) | PM | Sonnet | 60461 | 28 | 240s |
| 3 | PM Fase 2 (PRD + critérios de aceite) | PM | Sonnet | 51527 | 16 | 225s |
| 4 | Arquiteto (3 decisões técnicas + achado de dependência circular) | Arquiteto | Sonnet | 110481 | 49 | 673s |
