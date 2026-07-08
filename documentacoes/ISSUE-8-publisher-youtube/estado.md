# Estado — ISSUE-8: Publisher YouTube Shorts

## Campos principais
issue: 8
repo: omuletachou
titulo: feat: Publisher YouTube Shorts
rota: normal
etapa_atual: LT — mapear falhas (Code Review reprovou por cobertura de testes)
docs_path: repos/omuletachou/documentacoes/ISSUE-8-publisher-youtube
openspec_path: repos/omuletachou/openspec/changes/ISSUE-8-publisher-youtube
ultimo_agente: lt
status_comment_id: 4914784828
pr_homologacao: 67
pr_release: ~
qa_status: ~

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

## Sub-issues
sub_issues: [#65 (stack:dotnet, task_id:T-01)]
desenv_tasks_merged: [65]

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
