---
issue: 6
titulo: feat: Processor Job (Midia e Fila de Publicacao)
rota: normal
etapa_atual: Líder Técnico — refinamento técnico
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-6-processor-job
openspec_path: repos/omuletachou/openspec/changes/ISSUE-6-processor-job
tech_stacks:
  - .NET 8
  - Hangfire
  - HttpClient
ultimo_agente: pm
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

Issue #6 implementa o **Processor Job** — subsistema de processamento assíncrono de mídia e fila de publicação. É **dependente direto das Issues #2, #3, #4 e #5** (funciona com qualquer collector já implementado).

## Resolução das ambiguidades (Gate 1 — Gerente respondeu em 2026-07-06)

**(0) Status de entrada** — Resolvido: buscar `Status = Queued`. Novo status intermediário
`Processing` setado imediatamente ao pegar o produto (evita colisão entre execuções Hangfire
paralelas). Fluxo: `Pending` → `Queued` (CollectorJob) → `Processing` (ProcessorJob ao iniciar)
→ `Published` (sucesso) | `Error` (falha). Nota do PM: `Published` já existe no enum atual;
somente `Processing` e `Error` são valores novos — adição aditiva, sem risco de regressão nos
collectors já em produção (#4/#5).

**(a) Slug** — Resolvido: pular se já preenchido; gerar só se nulo/vazio. Nunca regerar.

**(b) AffiliateLink MercadoLivre** — Resolvido: responsabilidade do ProcessorJob, chamada real
a `POST /affiliate-tools/links`. Falha → `Status = Error` com mensagem descritiva.

**(c) MediaLocalPath** — Resolvido: nova migration incremental `AddMediaLocalPathToProducts`.

**(d) Detecção de Category** — Resolvido: `CategoryDetector` estático por palavras-chave em
`AfiliadoBot.Application`, sem IA/banco. Fallback `"Geral"`.

**(e) Distribuição de ScheduledAt** — Resolvido: round-robin pelos 5 horários do cron
(9h/12h/15h/18h/20h UTC-3), ordenado por `AiScore` desc, offset aleatório 0-10min. PM avaliou
que a mecânica de cálculo dos slots (execução única por ciclo, calculando todos os slots do
lote) é detalhe de implementação — não requer Arquiteto.

**(f) Produtos Rejected** — Resolvido: ficam definitivos, sem retry automático.

**(g) Falha no download de mídia** — Resolvido: processa sem mídia local, `PublisherJob` usa
`MediaUrl` original como fallback.

**(h) Redes habilitadas sem credenciais** — Resolvido: pula a rede, não cria entrada na fila.

## Avaliação de ambiguidade arquitetural (PM, Fase 2)
- Adição de `Processing`/`Error` ao enum `ProductStatus`: mudança aditiva, sem remoção/
  renomeação de valores existentes. Risco de regressão nos collectors (#4/#5) avaliado como
  **baixo** — eles só escrevem `Pending`/`Queued`/`Rejected`, nunca leem/comparam com os
  novos valores. Não requer revisão arquitetural.
- Round-robin multi-dia de `ScheduledAt`: decidiu-se que é **detalhe de implementação** do LT
  (execução única do ProcessorJob por ciclo, calculando todos os slots do lote na mesma
  chamada — não depende de scheduling contínuo do Hangfire para "avançar" o round-robin).
- **Conclusão: sem ambiguidade arquitetural genuína. Segue direto para o Líder Técnico**
  (não escalado para o Arquiteto).

## Documentos produzidos
- `prd.md` — PRD consolidado com fluxo de status, regras de negócio, casos de exceção,
  mudanças na entidade `Product`, integrações externas e definição de pronto.
- `criterios-aceite.md` — 21 critérios de aceite em Given/When/Then cobrindo
  LocalMediaStorage, máquina de estados, Slug, Category, AffiliateLink ML, PublicationQueue,
  migration e encadeamento de jobs.

## Histórico
- 2026-07-06 — Coordenador preparou Issue (estado.md, diretórios, label, card no board)
- 2026-07-06 — PM Fase 1: PRD inicial (`prd.md`) escrito; 9 perguntas de Gate 1 postadas na Issue #6 (comentário https://github.com/DQM-BETA/omuletachou/issues/6#issuecomment-4896543914)
- 2026-07-06 — Gerente respondeu ao Gate 1 (comentário https://github.com/DQM-BETA/omuletachou/issues/6#issuecomment-4896910207)
- 2026-07-06 — PM Fase 2: PRD consolidado, `criterios-aceite.md` criado, sem ambiguidade arquitetural — segue direto para Líder Técnico

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação | coordenador | haiku | 24343 | 17 | 104s |
| 2 | PM Fase 1 | pm | sonnet | 34424 | 11 | 100s |
| 3 | PM Fase 2 | pm | sonnet | ~ | ~ | ~ |

---
*Aguardando refinamento técnico do Líder Técnico.*
