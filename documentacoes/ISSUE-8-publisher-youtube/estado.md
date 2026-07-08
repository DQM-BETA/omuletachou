# Estado — ISSUE-8: Publisher YouTube Shorts

## Campos principais
issue: 8
repo: omuletachou
titulo: feat: Publisher YouTube Shorts
rota: normal
etapa_atual: Em Desenvolvimento — PR #71 (desenv→homolog) criado, aguardando novo Code Review
docs_path: repos/omuletachou/documentacoes/ISSUE-8-publisher-youtube
openspec_path: repos/omuletachou/openspec/changes/ISSUE-8-publisher-youtube
ultimo_agente: lt
status_comment_id: 4914784828
pr_homologacao: 71
pr_release: ~
qa_status: reprovado (CA16) — 1ª reprovação — fix mergeado, aguardando revalidação
code_review_homolog_pr: 71

## Contexto
Stack: .NET 8, Google.Apis.YouTube.v3, OAuth2
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #6 (ProcessorJob) e #7 (PublisherJob/Hangfire) — ambas em produção (main)

**Achado técnico confirmado:** `PublisherJob` já implementa orquestração genérica de publishers via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork`. Adicionar `YoutubePublisher` é puramente aditivo — sem retrofit necessário no PublisherJob. A integração será idêntica à de TelegramPublisher (T-02 da Issue #7). `SocialNetwork.Youtube` já existe no enum.

**Gate 1 respondido pelo Gerente** (comentário https://github.com/DQM-BETA/omuletachou/issues/8#issuecomment-4914951494), fechando as 6 perguntas de levantamento:
1. `ProcessorJob` (Issue #6, já em produção) precisa de correção: excluir a rede Youtube da criação de `PublicationQueue` quando `Product.MediaType != "video"`. `YoutubePublisher` também tem fallback de segurança (falha sem retry se receber item sem vídeo).
2. Fonte do arquivo: `MediaLocalPath` com fallback `MediaUrl` — se usar `MediaUrl`, baixar para stream temporário antes do upload (API do YouTube não aceita URL direta).
3. Validação de proporção/duração é responsabilidade do YouTube, não do publisher.
4. Chunk fixo 8MB, timeout 5min/chunk, 15min total.
5. Falha de refresh_token: `Failed` sem retry + flag `youtube.token_invalid=true` em `app_settings`.
6. Categoria mapeada via dicionário `Product.Category → YouTube categoryId` (não mais fixa).

**PM Fase 2 concluída:**
- `prd.md` consolidado com todos os requisitos, regras de negócio, casos de exceção e definição de pronto.
- `criterios-aceite.md` criado com 20 critérios Given/When/Then cobrindo YoutubePublisher (metadados, token, upload por chunks, fallback de segurança) e a correção no ProcessorJob (com atenção específica a regressão nas demais redes — CA19).
- **Avaliação de ambiguidade arquitetural:** PM decidiu NÃO escalar para o Arquiteto.
  - Correção no `ProcessorJob`: risco avaliado como BAIXO — filtro aditivo isolado, um `if` a mais na decisão de criar entrada de fila para a rede Youtube especificamente; não altera lógica das demais redes. Ainda assim, por ser código já em produção, o PRD registra a exigência de teste de regressão explícito (CA19).
  - Download sob demanda de `MediaUrl` para stream temporário (fallback quando `MediaLocalPath` é nulo): avaliado como detalhe de implementação, não decisão arquitetural — não introduz nova dependência de infraestrutura/storage além do que já existe (`IMediaStorage`/`HttpClient`). Registrado no PRD para o LT decidir se reaproveita `IMediaStorage.DownloadAsync` ou implementa localmente ao publisher, como parte do task breakdown.
- Comentário de sumário do PRD postado na Issue #8. Comentário 📍 Status atualizado para "Líder Técnico — refinamento técnico".

**Refinamento técnico (LT) concluído:**
- `tasks.md` criado com decisão de escopo: **1 sub-issue única** (T-01) cobrindo YoutubePublisher + fix retroativo no ProcessorJob — ver justificativa completa em `tasks.md` (coesão do comportamento "sem vídeo nunca publica no YouTube" em 2 camadas, risco já avaliado BAIXO pelo PM, escopo pequeno o suficiente para um PR único, mitigação via teste de regressão obrigatório CA19).
- Reavaliação adicional do LT: `ProcessorJob.NetworkSettings` usa `youtube.access_token` como credencial de habilitação, desalinhado com as credenciais estáveis reais do publisher (`youtube.client_id`/`client_secret`/`refresh_token`) — corrigido como parte da mesma sub-issue (mesmo método/tabela estática, sem escopo adicional).
- Decisão de reaproveitar `IMediaStorage.DownloadAsync` (já usado pelo ProcessorJob) para o fallback de `MediaUrl` no YoutubePublisher, em vez de implementar um segundo caminho de download.
- Sub-issue #65 criada: "[ISSUE-8] Sub: YoutubePublisher + fix retroativo no ProcessorJob" (label stack:dotnet).
- Comentário de resumo técnico postado na Issue #8.

**Dev .NET (sub-issue #65) concluído:**
- `YoutubePublisher : ISocialPublisher` criado (`backend/src/AfiliadoBot.Infrastructure/Integrations/Social/YoutubePublisher.cs`): upload resumable em chunks de 8MB (timeout 5min/chunk, 15min total), OAuth2 com renovação de access_token via refresh_token, categoria mapeada por `Product.Category` (dicionário estático + fallback "22"), fallback de mídia `MediaLocalPath` → `MediaUrl` (via `IMediaStorage.DownloadAsync`), fallback de segurança sem vídeo (CA16) e falha de refresh_token (CA12) implementados via `FailPermanently` (esgota `RetryCount` para impedir reprocessamento).
- Decisão de implementação registrada no PR: chamadas HTTP diretas via `HttpClient` em vez do SDK `Google.Apis.YouTube.v3` (controle fino do chunking exigido pelos CAs).
- Fix retroativo no `ProcessorJob.CreatePublicationQueueEntriesAsync`: filtro `HasVideoAvailable` isolado para `SocialNetwork.Youtube` (CA17-CA19).
- Fix `NetworkSettings`: credencial Youtube trocada de `youtube.access_token` para `client_id`/`client_secret`/`refresh_token`.
- Seed de `app_settings` (`youtube.client_id`, `youtube.client_secret`, `youtube.refresh_token`) adicionado via migration `SeedYoutubeCredentials`.
- Registrado no DI (`Program.cs`, `AddHttpClient<ISocialPublisher, YoutubePublisher>()`).
- Testes: 122/122 passando (104 pré-existentes + 4 novos em `ProcessorJobTests` + 14 novos em `YoutubePublisherTests`, incluindo CA19 de regressão explícita).
- Boot Docker validado: `/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` todos HTTP 200, sem erro de DI/infra.
- PR #66 (`feature/65-youtube-publisher` → `desenv`) aberto e pronto para merge.

**Merge LT concluído:**
- PR #66 (`feature/65-youtube-publisher` → `desenv`) squash-merged com sucesso.
- Sub-issue #65 fechada.
- Única sub-issue da Issue #8 → todas as tasks concluídas.
- PR #67 (`desenv` → `homolog`) criado: "release(ISSUE-8): Publisher YouTube Shorts".

**Code Review PR #67 — REPROVADO (cobertura de testes, não bug funcional):**
- Build, 122/122 testes existentes e boot Docker (health + triggers processor/publisher) passaram sem erro.
- CA19 (não-regressão nas demais redes) confirmado coberto e aprovado.
- Gap: `YoutubePublisher.cs` implementa corretamente CA5 (title truncado a 100 chars, `BuildMetadataJson` linha 254), CA6 (description = `Product.AiCaption`, linha 255), CA7 (tags fixas `["oferta","desconto","promocao","youtube"]`, linha 264), CA10 (`privacyStatus="public"`, linha 269), CA14 (timeout 5min/chunk, `ChunkTimeout` linhas 31/333-334) e CA15 (timeout total 15min, `TotalTimeout` linhas 32/119-120) — mas `YoutubePublisherTests.cs` (8 métodos) não tem assert dedicado para nenhum desses 6 critérios. O único teste que inspeciona o corpo da requisição (`PublishAsync_MapeiaCategoriaCorreta_PorCategoriaDoProduto`) verifica apenas `categoryId`.
- Confirmado por leitura direta do LT: lógica existe no código, cobertura de teste realmente ausente para os 6 CAs listados. Não é bug — é lacuna de cobertura em relação à definição de pronto do `tasks.md` ("Todos os CA1-CA20 cobertos por teste e passando").
- **Decisão do LT:** correção via branch de fix direto `fix/67-youtube-publisher-test-coverage` (base: desenv), sem nova sub-issue formal — escopo mínimo (adicionar testes, sem alterar código de produção), continuação direta do trabalho da sub-issue #65. PR resultante deve ser mergeado em desenv e re-emendado ao PR #67 (ou #67 atualizado) antes de nova rodada de Code Review.
- Testes a adicionar em `YoutubePublisherTests.cs`:
  - CA5: título > 100 chars é truncado no metadata JSON enviado.
  - CA6: `description` no metadata JSON = `Product.AiCaption`.
  - CA7: `tags` no metadata JSON = `["oferta","desconto","promocao","youtube"]`.
  - CA10: `status.privacyStatus` no metadata JSON = `"public"`.
  - CA14: timeout de chunk (5min) aciona `InvalidOperationException` com mensagem de timeout de chunk (simular handler que atrasa/cancela).
  - CA15: timeout total (15min) aciona `InvalidOperationException` com mensagem de timeout total.

**Dev .NET (fix cobertura) concluído:**
- Branch `fix/67-youtube-publisher-test-coverage` (base `desenv`) via worktree — nenhum código de produção alterado, apenas `YoutubePublisherTests.cs`.
- 6 testes novos adicionados: `PublishAsync_TruncaTitulo_Para100CaracteresNoMetadata` (CA5), `PublishAsync_DescricaoNoMetadata_IgualAoAiCaption` (CA6), `PublishAsync_TagsNoMetadata_ContemValoresFixosEsperados` (CA7), `PublishAsync_PrivacyStatusNoMetadata_SempreIgualAPublic` (CA10), `PublishAsync_TimeoutDeChunk_LancaInvalidOperationExceptionComMensagemDeChunk` (CA14), `PublishAsync_TimeoutTotal_LancaInvalidOperationExceptionComMensagemDeTimeoutTotal` (CA15). Todos capturam/parseiam o JSON do corpo da requisição de início do upload resumable via `HttpMessageHandler` mockado (mesmo padrão dos 8 testes pré-existentes).
- Nota técnica CA14/CA15: `ChunkTimeout`/`TotalTimeout` são campos privados estáticos não parametrizáveis via construtor — reflection para reduzi-los foi tentada e rejeitada (`.NET` lança `FieldAccessException` em `initonly static field` após inicialização do tipo). Abordagem final: o mock de `HttpMessageHandler` lança `OperationCanceledException` diretamente no ponto de chamada relevante (PUT do chunk para CA14; POST de início do upload para CA15) — o código de produção não distingue a origem da exceção, apenas inspeciona `ct.IsCancellationRequested`/`totalCts.IsCancellationRequested`, então os testes exercitam exatamente os mesmos branches percorridos quando o timeout real decorre, sem esperar 5/15 minutos reais e sem alterar produção.
- Testes: 128/128 passando (122 pré-existentes + 6 novos), sem regressão.
- Boot Docker validado (`docker compose up`, `.env` local criado a partir de `.env.example` — não versionado): `/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` todos HTTP 200, sem erro de DI/infra.
- PR #68 (`fix/67-youtube-publisher-test-coverage` → `desenv`) aberto e pronto para merge.

**Merge PR #68 concluído (LT):**
- PR #68 (`fix/67-youtube-publisher-test-coverage` → `desenv`) squash-merged com sucesso (merge commit `53b7600`).
- PR #67 (`desenv` → `homolog`) confirmado atualizado automaticamente com o novo commit (mesma base `desenv`, sem necessidade de re-emendar): 23 commits, último = "test(ISSUE-67): cobertura de testes CA5/CA6/CA7/CA10/CA14/CA15 do YoutubePublisher", `mergeable: MERGEABLE`, +2289/-3.
- PR #67 pronto para nova rodada de Code Review (build/boot/testes + checklist de veto).
- **Pendência para o Coordenador:** atualizar comentário 📍 Status (id `4914784828`) para refletir "PR #67 aguardando nova rodada de Code Review" — não editado por este agente (fora do escopo do LT).

**Code Review PR #67 — REVALIDAÇÃO APROVADA (rodada 2):**
- Checkout limpo de `desenv` (HEAD `a63aa6a`, mesmo commit do PR #67 no momento da revalidação).
- Build: `dotnet build` — sucesso, 0 erros, 1 warning pré-existente (não relacionado ao diff).
- Testes: `dotnet test` — **128/128 aprovados**, 0 falhas, ~24s.
- Os 6 testes novos do PR #68 (CA5/CA6/CA7/CA10/CA14/CA15) foram lidos linha a linha no diff e confirmados como cobertura real: parseiam o JSON de metadata enviado no POST de início do upload resumable e/ou capturam corretamente `InvalidOperationException` com mensagem batendo exatamente com o código de produção (`UploadChunksAsync`/`InitiateResumableUploadAsync`, mensagens "timeout de chunk (5 min)"/"timeout total (15 min)"). Rodados isoladamente via `--filter`: 7/7 passaram (incluindo CA19).
- CA19 (regressão ProcessorJob) reconfirmado passando isolado e na suíte completa — nenhuma mudança de produção nesta rodada (PR #68 só tocou `YoutubePublisherTests.cs`).
- Boot Docker: `docker compose up -d --build` — 4 containers subiram sem erro, migration `SeedYoutubeCredentials` aplicada, `/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` todos HTTP 200, sem exception nos logs. Containers derrubados ao final (`docker compose down`, sem `-v`).
- Checklist de veto: sem secrets hardcoded (migration semeia valores vazios, testes usam placeholders), sem teste-lixo, PR é backend-only (sem `.spec.ts` no diff, `.first()`/E2E não aplicável).
- Evidência completa postada como comentário no PR #67 (https://github.com/DQM-BETA/omuletachou/pull/67#issuecomment-4918181794).
- **PR #67 mergeado (desenv→homolog, merge commit, não-squash) com sucesso.**

**QA (homolog) — REPROVADO — CA16 (1ª reprovação, comentário https://github.com/DQM-BETA/omuletachou/issues/8#issuecomment-4918299645):**
- 19/20 CAs passaram, muitos validados end-to-end real (Docker + Postgres real + chamada HTTP real ao Google OAuth2, ver `relatorio-qa.md`).
- **CA16 falhou na validação integrada real:** `PublisherJob.cs:62-63` sobrescreve incondicionalmente a `ErrorMessage` que `YoutubePublisher.FailPermanently` já havia setado especificamente para o fallback de segurança "sem vídeo", substituindo pela mensagem genérica `"Falha ao publicar (retorno negativo do publisher)."`. Causa raiz: `PublicationQueue.RegisterAttempt` (linhas 51-65) sempre sobrescreve `ErrorMessage` sem checar se o item já tinha uma mensagem mais específica. O teste unitário `PublishAsync_FalhaSemRetry_QuandoProdutoSemVideo` não pega isso porque chama `YoutubePublisher.PublishAsync` isoladamente, sem passar pelo `PublisherJob` real.
- Relatório completo: `relatorio-qa.md`.

**Mapeamento do fix (LT) — decisão de design e escopo:**

**Design escolhido:** em `PublisherJob.ExecuteAsync`, capturar `item.RetryCount` **antes** de chamar `publisher.PublishAsync`. Após a chamada:
- `success == true` → `item.RegisterAttempt(true)` (comportamento atual, inalterado).
- `success == false` e `item.RetryCount` **não mudou** (o publisher não se auto-registrou, ex.: `TelegramPublisher`, que nunca toca `RegisterAttempt`) → `item.RegisterAttempt(false, "Falha ao publicar (retorno negativo do publisher).")` — comportamento atual preservado.
- `success == false` e `item.RetryCount` **já mudou** (o publisher já se auto-registrou internamente antes de retornar `false`, caso `YoutubePublisher.FailPermanently`, usado tanto no CA16 quanto no CA12) → **não chamar `RegisterAttempt` de novo**, preservando a `ErrorMessage` específica já setada.

**Alternativas avaliadas e descartadas:**
- (a) *Mudar o contrato `ISocialPublisher.PublishAsync` para retornar um tipo mais rico (ex.: `PublishResult { bool Success, string? ErrorMessage }`)*: mais "correto" no papel, mas quebra o contrato de todos os publishers existentes (`TelegramPublisher`, já em produção desde a Issue #7) — exige alterar a assinatura da interface, o `TelegramPublisher`, o `YoutubePublisher` e todos os mocks em `PublisherJobTests.cs`/testes de publisher. Escopo desproporcional ao bug (troca de contrato para resolver uma sobrescrita indevida).
- (b) *`YoutubePublisher` lançar exceção em vez de retornar `false`* (sugestão (b) do QA): o `catch` do `PublisherJob` já preserva `ex.Message` (linha 71), resolveria o CA16. Mas mudaria a semântica de "falha sem retry" para o caminho de exceção, que hoje é reservado para erros inesperados/de rede (retry padrão) — misturaria os dois conceitos (falha definitiva vs falha retryable) no mesmo mecanismo, exigindo revisar toda a lógica de retry do `PublisherJob` para não tratar essa exceção como retryable. Risco maior de regressão que a alternativa escolhida.
- **Escolhida: verificação de `RetryCount` antes/depois.** Menor diff possível (1 arquivo de produção, `PublisherJob.cs`), não altera nenhum contrato existente, é compatível 1:1 com o comportamento atual do `TelegramPublisher` (que nunca se auto-registra) e do `YoutubePublisher` (que já se auto-registra via `FailPermanently` nos dois casos "sem retry"). Não introduz um novo conceito na interface — apenas faz o `PublisherJob` respeitar um efeito colateral que o `YoutubePublisher` já produzia.

**Decisão de escopo: sub-issue formal (#69), não branch de fix direto.**
Diferente do gap de cobertura do PR #68 (só testes, sem tocar código de produção — justificou branch de fix direto), este bug exige alterar código de produção já em produção (`PublisherJob.cs`), que é **compartilhado por todos os publishers**, incluindo `TelegramPublisher` (Issue #7, já em `main`). O risco de regressão no caminho do Telegram (que não se auto-registra) precisa de cobertura de teste explícita e rastreável via CA formal — não apenas um teste avulso numa branch de fix sem CA associado. Por isso: sub-issue formal com CA16 (revalidação) + CA21 (regressão Telegram/publishers que não se auto-registram) + CA22 (regressão sucesso), em vez de reaproveitar o padrão do PR #68.
- `criterios-aceite.md` atualizado com CA21 e CA22.
- `tasks.md` atualizado com T-02.
- Sub-issue #69 criada: "[ISSUE-8] Sub: fix ErrorMessage sobrescrita no PublisherJob (CA16/CA21)" (label stack:dotnet), branch alvo `feature/8-69-fix-publisherjob-errormessage` (base desenv).

**Dev .NET (sub-issue #69) concluído:**
- Branch `feature/69-publisherjob-errormessage-fix` (base `desenv`) via worktree isolado.
- TDD: testes de regressão CA16/CA21/CA22 escritos ANTES do fix em `PublisherJobTests.cs` (confirmado RED — o teste CA16 falhou exatamente como o QA relatou, `ErrorMessage` esperada "Produto sem mídia de vídeo, não aplicável ao YouTube" vs. obtida "Falha ao publicar (retorno negativo do publisher).").
- Fix aplicado em `PublisherJob.ExecuteAsync` (único arquivo de produção alterado): captura `item.RetryCount` antes de `publisher.PublishAsync`; após a chamada, só registra a mensagem genérica via `RegisterAttempt(false, ...)` se o `RetryCount` não mudou (publisher não se auto-registrou); se mudou (publisher já se auto-registrou, ex. `YoutubePublisher.FailPermanently`), preserva a `ErrorMessage` específica sem chamar `RegisterAttempt` de novo. Sucesso continua chamando `RegisterAttempt(true)` incondicionalmente.
- Testes novos: `ExecuteAsync_PreservaErrorMessageEspecifica_QuandoPublisherJaSeAutoRegistrou` (CA16), `ExecuteAsync_RegistraMensagemGenerica_QuandoPublisherNaoSeAutoRegistra` (CA21, regressão Telegram), `ExecuteAsync_RegistraSucesso_QuandoPublisherRetornaTrue` (CA22, regressão sucesso).
- Suíte completa: 131/131 passando (128 pré-existentes + 3 novos), 0 falhas.
- Gate de testes-dependentes: buscados todos os arquivos referenciando `PublisherJob`/`RegisterAttempt` (`PublisherJobTests.cs`, `PublicationQueueTests.cs`, `YoutubePublisher.cs`, `PublicationQueue.cs`, `Program.cs`) — nenhum precisou de ajuste (contrato `PublicationQueue.RegisterAttempt` e `ISocialPublisher` inalterados).
- Boot Docker validado: `.env` local criado a partir de `.env.example` (não versionado) — 4/4 containers subiram, `GET /health` → 200, `POST /api/jobs/processor/trigger` → 200, `POST /api/jobs/publisher/trigger` → 200, logs sem exceção. Stack derrubada ao final (`docker compose down -v`).
- PR #70 (`feature/69-publisherjob-errormessage-fix` → `desenv`) aberto e pronto para merge.
- Worktree `.worktrees/feature-69-publisherjob-errormessage` removido ao final.

**Merge LT (PR #70) concluído:**
- PR #70 (`feature/69-publisherjob-errormessage-fix` → `desenv`) squash-merged com sucesso (merge commit `3de49e8`).
- Sub-issue #69 fechada.
- Todas as sub-issues (#65, #69) concluídas e mergeadas em `desenv`.
- **PR #67 original (desenv→homolog) estava fechado** (já havia sido mergeado anteriormente, merge commit `a1a7496`) — não podia ser reaberto/reutilizado.
- **PR #71 (`desenv` → `homolog`) criado**, trazendo o fix de CA16 para uma nova rodada de Code Review + QA em homolog.

## Sub-issues
sub_issues: [#65 (stack:dotnet, task_id:T-01) — fechada, mergeada, #69 (stack:dotnet, task_id:T-02) — fechada, mergeada]
desenv_tasks_merged: [65, 69]

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |
| 3 | Gate 1 | Gerente | concluido — 6 perguntas respondidas |
| 4 | PM Fase 2 | pm | concluido — sem escalada ao Arquiteto |
| 5 | Refinamento técnico | lt | concluido — sub-issue #65 criada |
| 6 | Dev .NET (#65) | dev-dotnet | concluido — PR #66 aberto (feature/65 → desenv) |
| 7 | Merge desenv + PR homolog | lt | concluido — PR #66 mergeado, sub-issue #65 fechada, PR #67 (desenv→homolog) criado |
| 8 | Code Review PR #67 | code-review | reprovado — cobertura incompleta (CA5/6/7/10/14/15), não bug funcional |
| 9 | Mapear fix de cobertura | lt | concluido — branch fix/67-youtube-publisher-test-coverage mapeado, sem nova sub-issue |
| 10 | Dev .NET (fix cobertura) | dev-dotnet | concluido — PR #68 aberto (fix/67 → desenv), 128/128 testes passando |
| 11 | Merge PR #68 (fix cobertura) → desenv | lt | concluido — PR #68 squash-merged, PR #67 atualizado automaticamente e pronto para nova rodada de Code Review |
| 12 | Code Review PR #67 (revalidação) | code-review | aprovado — 128/128 testes, boot Docker ok, gap de cobertura sanado, merge desenv→homolog concluido |
| 13 | QA (homolog) | qa | reprovado — CA16 (ErrorMessage sobrescrita no PublisherJob), bug funcional real, 1ª reprovação |
| 14 | Mapear fix CA16 | lt | concluido — sub-issue #69 criada (design: RetryCount antes/depois em PublisherJob), CA21/CA22 adicionados |
| 15 | Dev .NET (#69) | dev-dotnet | concluido — PR #70 aberto (feature/69-publisherjob-errormessage-fix → desenv), 131/131 testes passando, boot Docker validado |
| 16 | Merge PR #70 + PR release rodada 2 | lt | concluido — PR #70 squash-merged, sub-issue #69 fechada, PR #71 (desenv→homolog) criado (PR #67 original estava fechado, não reutilizável) |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 50607 | 21 | 129s |
| 2 | PM Fase 1 | pm | sonnet | 40770 | 16 | 107s |
| 4 | PM Fase 2 | pm | sonnet | 55775 | 14 | 140s |
| 5 | Refinamento LT | lt | sonnet | 63630 | 17 | 148s |
| 6 | Dev #65 | dev-dotnet | sonnet | 162965 | 81 | 891s |
| 7 | Merge desenv + PR homolog | lt | sonnet | 40922 | 13 | 102s |
| 8 | Code Review PR #67 (reprovado — cobertura) | code-review | sonnet | 95702 | 23 | 265s |
| 9 | Mapear fix cobertura testes | lt | sonnet | 55994 | 8 | 87s |
| 10 | Dev .NET fix cobertura (PR #68) | dev-dotnet | sonnet | 104044 | 48 | 566s |
| 11 | Merge PR #68 → desenv | lt | sonnet | 48147 | 16 | 160s |
| 12 | Coordenador — atualizar 📍 Status | coordenador | haiku | 19147 | 3 | 30s |
| 13 | Code Review PR #67 (revalidação — aprovado, merge homolog) | code-review | sonnet | 84901 | 43 | 686s |
| 14 | QA (homolog) — reprovado (CA16) | qa | sonnet | 76226 | 50 | 518s |
| 15 | LT mapear fix CA16 (sub-issue #69 criada) | lt | sonnet | 81707 | 24 | 274s |
| 16 | Dev .NET fix CA16 (#69, PR #70) | dev-dotnet | sonnet | 83976 | 35 | 372s |
| 17 | Merge PR #70 + PR release rodada 2 (PR #71) | lt | sonnet | TBD | TBD | TBD |
