---
issue: 6
titulo: feat: Processor Job (Midia e Fila de Publicacao)
rota: normal
etapa_atual: QA
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-6-processor-job
openspec_path: repos/omuletachou/openspec/changes/ISSUE-6-processor-job
tech_stacks:
  - .NET 8
  - Hangfire
  - HttpClient
ultimo_agente: code-review
sub_issues:
  - "#47 (stack:dotnet, task_id:T-01) — LocalMediaStorage + Migration AddMediaLocalPathToProducts + CategoryDetector"
  - "#48 (stack:dotnet, task_id:T-02) — ProcessorJob.ExecuteAsync (orquestracao completa, depende de #47)"
  - "#52 (stack:dotnet, task_id:FIX-01) — Fix: permalink ML nao capturado, AffiliateLink com payload invalido (Code Review reprovou PR #51)"
desenv_tasks_merged: ["#47", "#48", "#52"]
sub_issues_frontend: {}
pr_homologacao: 51
pr_release: ~
code_review_homolog_pr: 51 (aprovado apos fix)
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

## Code Review reprovou o PR #51 (2026-07-06)

**Bloqueador:** `ProcessorJob.cs` usa `product.ImageUrl ?? product.MediaUrl ?? product.ExternalId`
como payload da chamada `POST /affiliate-tools/links`, mas o endpoint espera o **permalink**
(URL da página do produto no ML). `MercadoLivreCollector` (Issue #5) nunca captura/salva o
campo `permalink` retornado por `GET /sites/MLB/search` em nenhum campo do `Product` — hoje
`ImageUrl` é sempre null para ML e `ExternalId` é só o ID (`MLB123456`), não uma URL. Resultado:
toda chamada ao endpoint de afiliados em produção gera payload inválido.

**Correção mapeada (sub-issue #52):**
- Novo campo `Product.SourceUrl` (string?) + migration incremental.
- `MercadoLivreCollector`: capturar `permalink` da resposta de busca (`MercadoLivreItem` +
  `ParseItems` + `UpsertProductAsync`) e popular `SourceUrl`.
- `ProcessorJob`: usar `product.SourceUrl` (não `ImageUrl`/`MediaUrl`/`ExternalId`) no payload
  `{"url": SourceUrl}`; se nulo, `MarkAsError` com mensagem descritiva em vez de payload inválido.
- Atualizar `MercadoLivreCollectorTests` e `ProcessorJobTests`.
- Branch: `feature/ISSUE-6-fix-permalink-ml` (base: `desenv`).

PR #51 (desenv→homolog) permanece aberto e bloqueado até #52 ser corrigida, mergeada em `desenv`
e o PR #51 refletir o fix.

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
- 2026-07-06 — Code Review (PR #51): Camada 2 (build/testes/veto) aprovada (79/79 testes, build ok). Camada 1 (revisão manual) **reprovou**: `ProcessorJob.cs` usa `ImageUrl ?? MediaUrl ?? ExternalId` como payload do endpoint de afiliados ML, que espera o `permalink` do produto — campo nunca capturado pelo `MercadoLivreCollector` (Issue #5). Comentário postado na Issue #6.
- 2026-07-06 — LT: mapeou a correção. Confirmado no código que `MercadoLivreCollector.ParseItems`/`MercadoLivreItem`/`UpsertProductAsync` não capturam `permalink`. Criada sub-issue de fix **#52** (branch `feature/ISSUE-6-fix-permalink-ml`, novo campo `Product.SourceUrl` + migration, ajuste no collector e no `ProcessorJob`). PR #51 permanece aberto/bloqueado.
- 2026-07-06 — Dev .NET: fix #52 implementado. Novo campo `Product.SourceUrl` (nullable, incluído no construtor e em `UpdateFromCollector`) + migration `AddSourceUrlToProducts` (coluna `source_url` nullable). `MercadoLivreCollector`: `MercadoLivreItem` ganhou campo `Permalink` capturado de `item.permalink` em `ParseItems`; `UpsertProductAsync` passa `sourceUrl: item.Permalink` tanto na criação quanto no upsert de produto existente. `ProcessorJob.EnsureAffiliateLinkAsync`: payload agora usa `product.SourceUrl` (não mais `ImageUrl`/`MediaUrl`/`ExternalId`); se `SourceUrl` nulo/vazio, `MarkAsError("SourceUrl ausente — nao e possivel gerar link de afiliado ML")` e retorna `false` sem chamar a API. Testes atualizados: `MercadoLivreCollectorTests` (mock com `permalink`, assert `SourceUrl` na criação e no upsert) e `ProcessorJobTests` (`CriarProduto` com parâmetro `sourceUrl`, teste existente de falha HTTP ML agora seta `sourceUrl` válido, novo teste `ExecuteAsync_MarcaError_QuandoSourceUrlAusente` confirmando que a API não é chamada quando `SourceUrl` está ausente). Suite completa: 80/80 passando (79 pré-existentes + 1 novo). Build e boot da app (`dotnet run`) validados sem exceção. PR #53 (feature/ISSUE-6-fix-permalink-ml → desenv) aberto.
- 2026-07-06 — LT: merge squash do PR #53 (feature/ISSUE-6-fix-permalink-ml → desenv) concluído. Sub-issue #52 fechada, card movido para "Concluído" no board. PR #51 (desenv→homolog) reflete o fix automaticamente (mesma branch desenv).
- 2026-07-06 — Code Review (PR #51, rodada 2): ambas camadas aprovaram. Bug do permalink ML confirmado corrigido — `EnsureAffiliateLinkAsync` usa `product.SourceUrl`, sem chamada HTTP quando `SourceUrl` ausente. Build ok, 80/80 testes. Nenhuma regressão nos collectors Amazon/ML/Shopee.

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
| 9 | Merge T-02 + PR homolog | lt | sonnet | 65413 | 21 | 208s |
| 10 | Code Review PR #51 | code-review | sonnet | 69276 | 16 | 156s |
| 11 | LT mapear fix permalink | lt | sonnet | 47367 | 7 | 93s |
| 12 | Dev fix permalink #52 | dev-dotnet | sonnet | 79148 | 41 | 222s |
| 13 | Merge fix permalink #52 | lt | sonnet | 36285 | 11 | 105s |
| 14 | Code Review PR #51 (rodada 2) | code-review | sonnet | 48258 | 19 | 126s |

---
*Code Review (rodada 2) aprovou o PR #51 apos correcao do bug do permalink ML — 80/80 testes. Pronto para QA.*
