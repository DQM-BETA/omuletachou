---
issue: 5
titulo: feat: Collectors MercadoLivre e Shopee
rota: normal
etapa_atual: Líder Técnico — refinamento técnico
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
- Stack: .NET 8 + HttpClient + OAuth2 (MercadoLivre) + HMAC-SHA256 (Shopee) + GraphQL manual
- Gate 1 respondido pelo Gerente (comentário na Issue #5): contrato `CollectAsync`, `MediaUrl`/`MediaType` dedicados (não reaproveitar `ImageUrl`), OAuth2 ML com cache/refresh (margem 5 min), `AffiliateLink` nullable na criação (preenchido pelo `ProcessorJob` da Issue #6 após scoring), rate limiting (ML 150ms/10 req/s, Shopee 1s) com retry 3x backoff 2s/4s/8s em 429, collectors independentes (falha de um não impede o outro), fail-fast `InvalidOperationException` identificando a chave ausente, produto Shopee sem mídia é salvo mesmo assim.
- PM Fase 2 avaliou ambiguidade arquitetural: NENHUMA escalação necessária. A mudança em `Product` (AffiliateLink nullable) é um relaxamento de validação bem definido pelo Gate 1, sem impacto no `AmazonCollector` (que segue preenchendo o link síncrono). O `CollectorJob` orquestrador mencionado nas respostas do Gate 1 não existe ainda no código — PM registrou como decisão do LT (incluir no escopo desta Issue como task ou abrir separado), sem risco de regressão identificado.
- PRD consolidado em `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/prd.md` (requisitos funcionais, mudança de contrato em `Product`, migration necessária, pontos em aberto para o LT: escopo do CollectorJob e comportamento do link de afiliado da Shopee).
- Critérios de aceite Given/When/Then em `documentacoes/ISSUE-5-collectors-mercadolivre-shopee/criterios-aceite.md` (24 cenários cobrindo ML, Shopee, independência entre collectors, mudança de contrato em Product, testes).

## Histórico de Etapas
Criada em 2026-07-06 pelo Coordenador.

| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparação Issue | Coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |
| 3 | PM Fase 2 | pm | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo |
|---|---|---|---|---|---|---|
| 2 | PM Fase 1 | pm | sonnet | 36738 | 14 | 106s |
