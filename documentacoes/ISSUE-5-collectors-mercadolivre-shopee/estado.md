---
issue: 5
titulo: feat: Collectors MercadoLivre e Shopee
rota: normal
etapa_atual: QA
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
ultimo_agente: qa
sub_issues:
  - "#41 (stack:dotnet, task_id:T-01)"
  - "#42 (stack:dotnet, task_id:T-02)"
desenv_tasks_merged: ["#41", "#42"]
pr_homologacao: 45
pr_release: ~
code_review_homolog_pr: 45
qa_status: aprovado
blockers: nenhum
---

## Contexto
- Dependência: Issues #2 (Domain/EF Core/Schema), #3 (ClaudeAiService), #4 (AmazonCollector pattern)
- Objetivo: implementar collectors para plataformas MercadoLivre e Shopee, reutilizando o padrão IPlatformCollector, ExternalId, upsert estabelecido no AmazonCollector
- Stack: .NET 8 + HttpClient + OAuth2 (MercadoLivre) + HMAC-SHA256 (Shopee) + GraphQL manual
- Gate 1 respondido pelo Gerente (comentário na Issue #5): contrato `CollectAsync`, `MediaUrl`/`MediaType` dedicados (não reaproveitar `ImageUrl`), OAuth2 ML com cache/refresh (margem 5 min), `AffiliateLink` nullable na criação (preenchido pelo `ProcessorJob` da Issue #6 após scoring), rate limiting (ML 150ms/10 req/s, Shopee 1s) com retry 3x backoff 2s/4s/8s em 429, collectors independentes (falha de um não impede o outro), fail-fast `InvalidOperationException` identificando a chave ausente, produto Shopee sem mídia é salvo mesmo assim.
- PM Fase 2 avaliou ambiguidade arquitetural: NENHUMA escalação necessária. A mudança em `Product` (AffiliateLink nullable) é um relaxamento de validação bem definido pelo Gate 1, sem impacto no `AmazonCollector` (que segue preenchendo o link síncrono). O `CollectorJob` orquestrador mencionado nas respostas do Gate 1 não existe ainda no código — PM registrou como decisão do LT (incluir no escopo desta Issue como task ou abrir separado), sem risco de regressão identificado.
- PRD consolidado em `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/prd.md` (requisitos funcionais, mudança de contrato em `Product`, migration necessária, pontos em aberto para o LT: escopo do CollectorJob e comportamento do link de afiliado da Shopee).
- Critérios de aceite Given/When/Then em `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/criterios-aceite.md` (24 cenários cobrindo ML, Shopee, independência entre collectors, mudança de contrato em Product, testes).
- LT concluiu refinamento técnico: task breakdown em `tasks.md` com decisões documentadas — (1) CollectorJob orquestrador NÃO incluído nesta Issue (fica para issue futura de Scheduler); (2) migration única feita em T-01, T-02 depende de T-01 mergeado em desenv (execução sequencial, não paralela, para evitar migrations concorrentes no mesmo schema); (3) confirmado que Shopee preenche `AffiliateLink` diretamente na criação (offerLink já vem pronto na API, diferente do ML que aguarda scoring).
- Sub-issues criadas: #41 (T-01: MercadoLivreCollector — migration + OAuth2 + cache token + scoring) e #42 (T-02: ShopeeCollector — HMAC-SHA256 + GraphQL + fallback mídia), ambas stack:dotnet, label já existente no repo.
- T-01 (#41) mergeada em desenv via PR #43 (squash). T-02 (#42) mergeada em desenv via PR #44 (squash). Ambas sub-issues concluídas — PR desenv→homolog #45 criado com o release conjunto da Issue #5.
- Code Review (2 camadas) aprovou o PR #45 — 51/51 testes, build ok, sem regressão. PR #45 mergeado desenv→homolog via merge commit em 2026-07-06.
- QA validou os 24 critérios de aceite em `homolog` (branch sincronizada via `git reset --hard origin/homolog`, commit `baddb12` confirmado): build ok, 51/51 testes passando, sem regressão no AmazonCollector (7/7). Aplicação subiu via Docker (`/health` 200 OK), mas o teste manual dos endpoints de trigger foi bloqueado por um problema de infraestrutura local (interpolação `${DB_USER}`/`${DB_PASSWORD}` no `docker-compose.yml`/`.env`, não relacionado ao código desta Issue — `docker-compose.yml` não tocado pelos PRs #43/#44/#45). Relatório completo em `relatorio-qa.md`.

## Histórico de Etapas
Criada em 2026-07-06 pelo Coordenador.

| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparação Issue | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |
| 3 | PM Fase 2 | pm | concluido |
| 4 | Refinamento LT | LT | concluido |
| 5 | Sincronizar board | Coordenador | concluido |
| 6 | Dev T-01 #41 | dev-dotnet | concluido |
| 7 | Merge T-01 (#41) | LT | concluido |
| 8 | Dev T-02 #42 | dev-dotnet | concluido |
| 9 | Merge T-02 + PR homolog | LT | concluido |
| 10 | Code Review PR #45 | code-review | concluido |
| 11 | Merge PR homolog #45 | LT | concluido |
| 12 | QA homolog | qa | concluido — CAs ok |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
| 2 | PM Fase 1 | pm | sonnet | 36738 | 14 | 106s |
| 3 | PM Fase 2 | pm | sonnet | 44795 | 10 | 121s |
| 4 | Refinamento LT | lt | sonnet | 61583 | 19 | 175s |
| 5 | Sincronizar board | coordenador | haiku | 81314 | 72 | 347s |
| 6 | Dev T-01 #41 | dev-dotnet | sonnet | 100099 | 49 | 557s |
| 7 | Merge T-01 (#41) | lt | sonnet | 39609 | 17 | 200s |
| 8 | Dev T-02 #42 | dev-dotnet | sonnet | 78665 | 28 | 244s |
| 9 | Merge T-02 + PR homolog | lt | sonnet | 39160 | 19 | 183s |
| 10 | Code Review PR #45 | code-review | sonnet | 72123 | 18 | 155s |
| 11 | Merge PR homolog #45 | lt | sonnet | 33020 | 8 | 67s |
