---
issue: 6
titulo: feat: Processor Job (Midia e Fila de Publicacao)
rota: normal
etapa_atual: Code Review
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-6-processor-job
openspec_path: repos/omuletachou/openspec/changes/ISSUE-6-processor-job
tech_stacks:
  - .NET 8
  - Hangfire
  - HttpClient
ultimo_agente: lt
sub_issues:
  - "#47 (stack:dotnet, task_id:T-01) — LocalMediaStorage + Migration AddMediaLocalPathToProducts + CategoryDetector"
  - "#48 (stack:dotnet, task_id:T-02) — ProcessorJob.ExecuteAsync (orquestracao completa, depende de #47)"
desenv_tasks_merged: ["#47", "#48"]
sub_issues_frontend: {}
pr_homologacao: 51
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
- `tasks.md` — Task breakdown técnico: decisão de particionamento (T-01/T-02) e detalhamento
  de escopo, critérios e contexto técnico por sub-tarefa.

## Refinamento técnico (Líder Técnico)

Duas sub-issues, ambas stack `dotnet`, **sequenciais** (T-02 depende de T-01 mergeado em `desenv`):

- **#47 (T-01)** — `LocalMediaStorage` (download de mídia) + migration
  `AddMediaLocalPathToProducts` (campo `MediaLocalPath`, enum `Processing`/`Error`) +
  `CategoryDetector`. Unidades pequenas, testáveis isoladamente, sem dependência do fluxo do job.
- **#48 (T-02)** — `ProcessorJob.ExecuteAsync()` completo: orquestra busca Queued→Processing,
  mídia (via T-01), slug, categoria (via T-01), AffiliateLink ML, geração de legendas via
  `IAiService`, criação de `PublicationQueue` por rede com round-robin, finalização
  Published/Error. Máquina de estados coesa por produto — não fatiada além disso.

Justificativa completa da decisão de particionamento em `tasks.md` (seção "Decisão de
particionamento").

## Histórico
- 2026-07-06 — Coordenador preparou Issue (estado.md, diretórios, label, card no board)
- 2026-07-06 — PM Fase 1: PRD inicial (`prd.md`) escrito; 9 perguntas de Gate 1 postadas na Issue #6 (comentário https://github.com/DQM-BETA/omuletachou/issues/6#issuecomment-4896543914)
- 2026-07-06 — Gerente respondeu ao Gate 1 (comentário https://github.com/DQM-BETA/omuletachou/issues/6#issuecomment-4896910207)
- 2026-07-06 — PM Fase 2: PRD consolidado, `criterios-aceite.md` criado, sem ambiguidade arquitetural — segue direto para Líder Técnico
- 2026-07-06 — LT: refinamento técnico concluído. `tasks.md` criado com decisão de particionamento (T-01/T-02 sequenciais). Sub-issues criadas: #47 (T-01, stack:dotnet), #48 (T-02, stack:dotnet, depende de #47). Sem UI — pula UX/UI.
- 2026-07-06 — Coordenador: sincronizou board com sub-issues #47 e #48. Ambas movidas para "Em Desenvolvimento" junto com a issue mãe #6.
- 2026-07-06 — Dev .NET: T-01 (#47) implementado — migration `AddMediaLocalPathToProducts` (campo `MediaLocalPath` + enum `Processing`/`Error` aditivo), `IMediaStorage`/`LocalMediaStorage` (download HTTP para `/app/media/`, deteccao de tipo por extensao, retorna null sem exception em falha), `CategoryDetector` (deteccao por palavra-chave, fallback "Geral"). 14 novos testes (LocalMediaStorageTests, CategoryDetectorTests). Suite completa: 65/65 passando. Build e boot da app (`dotnet run` + `/health`) validados. PR #49 (feature/47-local-media-storage → desenv) aberto.
- 2026-07-06 — LT: merge squash do PR #49 (feature/47-local-media-storage → desenv) concluído. Sub-issue #47 fechada e card movido para "Concluído" no board. Como #48 (T-02) ainda não foi desenvolvida, PR desenv→homolog NÃO foi criado — aguarda merge de T-02 para consolidar as duas sub-issues em um único PR de homologação.
- 2026-07-06 — Dev .NET: T-02 (#48) implementado — `ProcessorJob.ExecuteAsync()` completo: busca `Queued` ordenado por `AiScore` desc, lock otimista via `MarkAsProcessing()` + SaveChanges imediato, download de mídia via `IMediaStorage` (T-01), geração de slug apenas quando vazio (`Product.SetSlugIfEmpty`), detecção de categoria via `CategoryDetector` (T-01, só sobrescreve "Geral" via novo `Product.SetCategory`), link de afiliado MercadoLivre via `POST /affiliate-tools/links` real (falha → `MarkAsError` + pula fila, sem exception não capturada), legendas via `IAiService.GenerateCaptionAsync` por rede habilitada com credenciais em `app_settings`, `PublicationQueue` com Facebook forçado a `ManualPending` (novo método `PublicationQueue.MarkAsManualPending()` e novo valor no enum `PublicationStatus`) e demais redes `Scheduled` com `ScheduledAt` por round-robin (9h/12h/15h/18h/20h UTC, offset 0-10min, ordenado por `AiScore` desc). Finalização `MarkAsPublished()` ao concluir sem erro. `AfiliadoBot.Application` passou a referenciar `AfiliadoBot.Infrastructure` (necessário para `AfiliadoBotDbContext`). Endpoint `POST /api/jobs/processor/trigger` e registro DI (`AddHttpClient<ProcessorJob>()`) adicionados em `Program.cs`. 14 novos testes (`ProcessorJobTests`) cobrindo CA4-CA9, CA12-CA19. Suite completa: 79/79 passando (sem regressão nos 65 pré-existentes). Build e boot da app (`dotnet run`) validados sem exceção. PR #50 (feature/48-processor-job → desenv) aberto.
- 2026-07-06 — LT: merge squash do PR #50 (feature/48-processor-job → desenv) concluído. Sub-issue #48 fechada (card movido para "Code Review" no board). Todas as sub-issues (#47, #48) mergeadas em `desenv`. PR #51 (desenv→homolog) criado consolidando T-01+T-02 da Issue #6 completa.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação | coordenador | haiku | 24343 | 17 | 104s |
| 2 | PM Fase 1 | pm | sonnet | 34424 | 11 | 100s |
| 3 | PM Fase 2 | pm | sonnet | 53235 | 16 | 146s |
| 4 | Refinamento LT | lt | sonnet | 62563 | 16 | 183s |
| 5 | Sincronização board | coordenador | haiku | 8721 | 4 | 52s |
| 6 | Dev T-01 #47 | dev-dotnet | sonnet | 84515 | 59 | 345s |
| 7 | Merge T-01 (#47) | lt | sonnet | 62895 | 19 | 155s |
| 8 | Dev T-02 #48 | dev-dotnet | sonnet | 96706 | 55 | 386s |
| 9 | Merge T-02 (#48) + PR desenv→homolog | lt | sonnet | (ver usage retornado) | — | — |

---
*PR #51 (desenv→homolog) criado, consolidando T-01 (#47) e T-02 (#48) — Issue #6 completa. Aguardando Code Review (plugin /code-review + agente Code Review).*
