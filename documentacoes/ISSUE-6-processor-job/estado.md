---
issue: 6
titulo: feat: Processor Job (Midia e Fila de Publicacao)
rota: normal
etapa_atual: Code Review (novo PR homolog — squash migrations)
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
  - "#52 (stack:dotnet, task_id:FIX-01) — Fix: permalink ML nao capturado, AffiliateLink com payload invalido (Code Review reprovou PR #51)"
desenv_tasks_merged: ["#47", "#48", "#52"]
sub_issues_frontend: {}
pr_homologacao: 57
pr_release: ~
code_review_homolog_pr: 51 (aprovado apos fix, rodada 2) — PR #55 (fix infra) aprovado (2 camadas) e mergeado em homolog — PR #56 (squash migrations) aprovado (2 camadas) e mergeado em desenv — PR #57 (desenv->homolog, consolidado) aguardando Code Review
qa_status: reprovado (bug de config — connection string mismatch, fix em PR #54/#55, mergeado em homolog); pendente revalidacao apos squash de migrations + auto-migrate (PR #57)
figma_url: ~
blockers: PR #57 (desenv -> homolog) aberto, aguardando Code Review (2 camadas)
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
- `relatorio-qa.md` — Relatório de QA (rodada com PR mergeado): 80/80 testes ok, mas
  validação integrada via Docker reprovada por bug de config de connection string.

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

## QA — bloqueado (2026-07-06), depois destravado, depois reprovado

**Verificação pré-validação obrigatória (conforme processo do agente QA) encontrou:**
- `gh pr view 51 --repo DQM-BETA/omuletachou --json state,mergedAt` → `{"state":"OPEN","mergedAt":null,"baseRefName":"homolog","headRefName":"desenv"}`
- `git log origin/homolog --oneline -5` → topo em `baddb12` (Merge pull request #45, referente à Issue #5), **sem nenhum commit** de #47/#48/#52 (Issue #6).

**Conclusão:** o merge desenv→homolog do PR #51 ainda não havia ocorrido, apesar do Code Review ter
aprovado (rodada 2) e do `estado.md` estar em `etapa_atual: QA`. Rodar a suíte de testes/build
contra a branch `homolog` naquele momento testaria código desatualizado (sem o fix do permalink
ML), gerando falso positivo ou falso negativo. **Validação NÃO prosseguiu** naquela rodada — nenhum
teste, build ou inspeção de screenshots foi executado.

**Destravado (2026-07-06):** LT executou o merge do PR #51 (desenv→homolog), merge commit
`c08e965`. `homolog` remoto avançou de `baddb12` para `c08e965`, agora contendo todos os
commits de #47/#48/#52. QA prosseguiu com a validação.

**Reprovado (2026-07-06, 2ª tentativa):** com o PR já mergeado, build (0 erros) e suite de testes
(80/80) passaram integralmente. Inspeção de código confirmou o fix #52 (`SourceUrl`) e cobertura
completa dos 21 CAs nos testes unitários. Porém, a **validação integrada obrigatória** (subir a
aplicação via `docker compose up` e exercer o fluxo real) encontrou falha: `GET /health` retorna
200, mas `POST /api/jobs/processor/trigger` retorna **HTTP 500**
(`Npgsql.PostgresException 28P01: password authentication failed for user "${DB_USER}"`),
reproduzido mesmo com volumes limpos (`docker compose down -v`). Causa raiz: `docker-compose.yml`
define a env var `ConnectionStrings__Default`, mas `Program.cs` lê
`GetConnectionString("DefaultConnection")` — chaves diferentes, então a env var do compose nunca é
usada; o app cai no `appsettings.json`, que tem a chave certa mas com placeholders literais
`${DB_USER}`/`${DB_PASSWORD}` nunca resolvidos. Qualquer operação que toque o banco falha em
ambiente Docker. Detalhes completos em `relatorio-qa.md`.

## Fix de infra — connection string (2026-07-06)

**Fix implementado (Dev .NET):** correção do mismatch de chave entre `docker-compose.yml`
(`ConnectionStrings__Default`) e `Program.cs` (`GetConnectionString("DefaultConnection")`).
PR #54 (`feature/ISSUE-6-fix-connection-string` → `desenv`).

**LT — merge e nova promoção (2026-07-06):**
- PR #54 mergeado em `desenv` via squash (`gh pr merge 54 --squash --auto`), confirmado
  `state: MERGED`, `mergedAt: 2026-07-06T20:51:36Z`.
- `git pull origin desenv` local: fast-forward `50e5620..e8a8616` (1 arquivo, `docker-compose.yml`).
- **Novo PR #55** (`desenv` → `homolog`) criado, consolidando o fix de infra para homologação.
  **NÃO mergeado ainda** — aguarda rodada de Code Review (2 camadas) antes da promoção e da
  revalidação do QA.

## Merge do PR #55 (fix connection string) — homolog (2026-07-06)

Code Review (2 camadas) aprovou o PR #55. LT mergeou (`gh pr merge 55 --merge`, merge commit,
sem squash — promoção `desenv→homolog`). Confirmado `state: MERGED`,
`mergedAt: 2026-07-06T21:37:23Z`, commit `26efaba` no topo de `origin/homolog`. Fix de connection
string agora presente em `homolog`. Ainda pendente: diagnóstico de por que as migrations do EF
Core não estão sendo aplicadas automaticamente no startup do container (necessário para a
revalidação completa do QA via `docker compose up`) — encaminhado ao DevOps.

## BLOQUEADO — decisão técnica pendente (2026-07-06), RESOLVIDA pelo Gerente

Trava anti-loop acionada: 3 problemas de infra encadeados na mesma tentativa de validar a Issue #6 em Docker:
1. Connection string mismatch (corrigido, PR #55 mergeado)
2. Migrations nunca aplicadas no startup — `Program.cs` sem `Database.Migrate()` (fix parcial aplicado pelo Dev)
3. `InitialSchema`/`AddClaudeMinScoreFallbackSeed` (escritas manualmente sem `dotnet ef migrations add`) sem `.Designer.cs` completo — EF falha ao aplicar `InsertData` do seed de `app_settings` por falta do model snapshot completo daquele estágio.

**Decisão do Gerente:** opção (b) — consolidar/squash todo o histórico de migrations num único
`InitialSchema` novo, gerado via `dotnet ef migrations add` a partir do modelo atual do código.
Aceitável pois o projeto não tem deploy em produção com dados reais a preservar ainda.

Branch `feature/ISSUE-6-fix-auto-migrate` (progresso anterior, incompleto, sem PR) permanece
como referência do diagnóstico, mas a implementação do squash deve ser feita em nova branch
dedicada pelo Dev .NET: `feature/ISSUE-6-squash-migrations` (base: `desenv`).

Detalhes completos do diagnóstico em `.claude/melhorias/2026-07-06-devops-omuletachou-ef-migrations-not-applied.md`.

## Squash de migrations — PR #56 mergeado em desenv, novo PR #57 homolog (2026-07-07)

**LT — merge do PR #56 (2026-07-07):** Code Review (2 camadas) aprovou o PR #56
(`feature/ISSUE-6-squash-migrations` → `desenv`). LT mergeou via squash
(`gh pr merge 56 --repo DQM-BETA/omuletachou --squash`), confirmado `state: MERGED`,
`mergedAt: 2026-07-07T13:08:17Z`. `git pull origin desenv` local: fast-forward `b2b2916..6a05d61`
(migration `InitialSchema` única, `Database.Migrate()` em `Program.cs`, `CustomWebApplicationFactory`
para testes com EF InMemory).

**Novo PR #57** (`desenv` → `homolog`) criado, consolidando o fix definitivo do bloqueio de infra
(squash de migrations + auto-migrate) para homologação. **NÃO mergeado ainda** — aguarda Code
Review (2 camadas) antes da promoção e da revalidação final do QA (que reprovou anteriormente por
esse mesmo bloqueio).

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
- 2026-07-06 — QA: verificação pré-validação encontrou PR #51 (desenv→homolog) ainda **OPEN** (mergedAt null). Branch homolog remota confirmada em `baddb12` (PR #45, Issue #5), sem nenhum commit de #47/#48/#52. Validação NÃO prosseguiu (rodar testes contra homolog sem o merge testaria código desatualizado). Bloqueado até o LT mergear o PR #51.
- 2026-07-06 — LT: mergeado o PR #51 (desenv→homolog) via merge commit (`gh pr merge 51 --merge`), commit `c08e965`. Confirmado: `gh pr view 51` retorna `state: MERGED`, `mergedAt: 2026-07-06T20:34:50Z`. `git log origin/homolog` confirma topo em `c08e965` ("Merge pull request #51 from DQM-BETA/desenv"), contendo os commits de #47/#48/#52. Bloqueio removido — pronto para nova tentativa de validação do QA.
- 2026-07-06 — QA (2ª tentativa): PR #51 confirmado MERGED (commit c08e965 no topo de homolog). Build ok, 80/80 testes passando, código inspecionado (fix #52 confirmado). Validação integrada via `docker compose up`: `/health` OK, mas `POST /api/jobs/processor/trigger` retornou HTTP 500 por falha de autenticação no Postgres (`password authentication failed for user "${DB_USER}"`), reproduzido mesmo com volumes limpos. Causa raiz: mismatch de chave entre `docker-compose.yml` (`ConnectionStrings__Default`) e `Program.cs` (`GetConnectionString("DefaultConnection")`) — o app usa o `appsettings.json` local com placeholders `${DB_USER}`/`${DB_PASSWORD}` nunca resolvidos. **QA REPROVADO** — fluxo integrado real quebrado apesar da suite unitária 100% ok. Relatório completo em `relatorio-qa.md`.
- 2026-07-06 — Dev .NET: fix de infra implementado — correção do mismatch entre `ConnectionStrings__Default` (docker-compose.yml) e `Program.cs` (`GetConnectionString("DefaultConnection")`). PR #54 (feature/ISSUE-6-fix-connection-string → desenv) aberto.
- 2026-07-06 — LT: merge squash do PR #54 (feature/ISSUE-6-fix-connection-string → desenv) concluído (`mergedAt: 2026-07-06T20:51:36Z`). `git pull origin desenv` confirmou fast-forward `50e5620..e8a8616`. Novo PR **#55** (desenv→homolog) criado consolidando o fix de infra, **não mergeado** — aguarda Code Review (2 camadas) antes da promoção e da revalidação do QA.
- 2026-07-06 — Code Review (PR #55, fix connection string): aprovado (2 camadas). LT mergeou PR #55 (desenv→homolog) via merge commit (`gh pr merge 55 --merge`), commit `26efaba`. Confirmado: `gh pr view 55` retorna `state: MERGED`, `mergedAt: 2026-07-06T21:37:23Z`. `git log origin/homolog` confirma topo em `26efaba` ("Merge pull request #55 from DQM-BETA/desenv"). Falta ainda resolver migrations do EF Core não aplicadas automaticamente no startup do container Docker — encaminhado ao DevOps para diagnóstico.
- 2026-07-07 — LT: recebida instrução para consolidar o histórico de migrations (squash em `InitialSchema` único), autorizado pelo Gerente após bloqueio da trava anti-loop. Instrução incluía passos de implementação (editar `Program.cs`, apagar/gerar migrations, ajustar testes, `dotnet build`/`dotnet test`, `docker compose up`) — **fora do escopo do LT** (sem `Edit`, não roda build/teste/docker de aplicação). Nenhum código foi alterado. `estado.md` atualizado com o escopo detalhado da implementação (opção b, decisão do Gerente) para o próximo Dev .NET executar em `feature/ISSUE-6-squash-migrations` (base `desenv`). Nenhuma branch nova criada pelo LT.
- 2026-07-07 — Dev .NET: squash de migrations implementado em `feature/ISSUE-6-squash-migrations`. `Database.Migrate()` adicionado em `Program.cs` (guardado por `IsRelational()`). Todas as 6 migrations antigas apagadas (`InitialSchema`, `AddClaudeMinScoreFallbackSeed`, `AddExternalIdToProduct`, `AddMediaFieldsAndNullableAffiliateLink`, `AddMediaLocalPathToProducts`, `AddSourceUrlToProducts`) e substituídas por uma única migration nova `InitialSchema` (20260707125445), gerada via `dotnet ef migrations add` a partir do modelo atual — 5 tabelas (`products`, `app_settings`, `publication_queue`, `publication_logs`, `push_subscriptions`) e os 31 seeds de `app_settings`, com `.Designer.cs` completo. Criada `CustomWebApplicationFactory` (substitui `AfiliadoBotDbContext` por EF InMemory no host de teste) e `HealthCheckTests` migrado para usá-la, evitando que `Migrate()` tente conectar a Postgres real durante os testes. Build ok, suite completa **80/80 passando** (sem regressão). **Validação Docker obrigatória confirmada:** `docker compose down -v` + `docker compose up -d --build` em ambiente limpo — logs do container `afiliado_api` mostram a migration `InitialSchema` aplicada com sucesso (todas as `CREATE TABLE`, os 31 `INSERT INTO app_settings`, índices e `INSERT INTO __EFMigrationsHistory`), app iniciado sem exceção. `GET /health` → **200** (`{"status":"healthy",...}`). `POST /api/jobs/processor/trigger` → **200** (sem erro de schema/conexão — bloqueio original resolvido). PR **#56** (`feature/ISSUE-6-squash-migrations` → `desenv`) aberto. Melhoria `.claude/melhorias/2026-07-06-devops-omuletachou-ef-migrations-not-applied.md` marcada como `status: implementado`.
- 2026-07-06 — Code Review (PR #56, squash migrations): ambas camadas aprovadas. Boot Docker confirmado independentemente 2x (Dev + Code Review).
- 2026-07-07 — LT: mergeado o PR #56 (`feature/ISSUE-6-squash-migrations` → `desenv`) via squash (`gh pr merge 56 --squash`), confirmado `state: MERGED`, `mergedAt: 2026-07-07T13:08:17Z`. `git pull origin desenv` local: fast-forward `b2b2916..6a05d61`. Novo PR **#57** (`desenv` → `homolog`) criado consolidando o fix definitivo do bloqueio de infra (squash de migrations + `Database.Migrate()`), **não mergeado** — aguarda Code Review (2 camadas) antes da promoção e da revalidação final do QA.

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
| 15 | QA (bloqueado) | qa | sonnet | 46913 | 8 | 107s |
| 16 | Merge PR #51 homolog | lt | sonnet | 43953 | 7 | 94s |
| 17 | QA (2ª tentativa — reprovado) | qa | sonnet | 88712 | 41 | 487s |
| 18 | DevOps diagnostico connection string | devops | haiku | 26039 | 10 | 40s |
| 19 | Dev fix connection string | dev-dotnet | sonnet | 29436 | 9 | 44s |
| 20 | Merge fix infra + PR #55 | lt | sonnet | 47076 | 7 | 113s |
| 21 | Code Review PR #55 (fix conn string) | code-review | sonnet | 40431 | 17 | 186s |
| 22 | Merge PR #55 homolog | lt | sonnet | 50031 | 9 | 154s |
| 23 | DevOps diagnostico auto-migrate | devops | haiku | 23251 | 8 | 33s |
| 24 | Dev fix auto-migrate (bloqueado) | dev-dotnet | sonnet | 103817 | 82 | 912s |
| 25 | Dev squash migrations (PR #56) | dev-dotnet | sonnet | 65277 | 44 | 376s |
| 26 | Code Review PR #56 (squash migrations) | code-review | sonnet | 62571 | 19 | 182s |
| 27 | Merge PR #56 + PR #57 homolog | lt | sonnet | 56094 | 9 | 141s |

---
*PR #57 (desenv→homolog) criado, consolidando squash de migrations + auto-migrate. Aguardando Code Review (2 camadas) antes da promoção e revalidação final do QA.*
