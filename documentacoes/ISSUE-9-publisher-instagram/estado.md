# Estado — ISSUE-9: Publisher Instagram Reels

## Campos principais
issue: 9
repo: omuletachou
titulo: feat: Publisher Instagram (Meta Graph API)
rota: normal
etapa_atual: Code Review aprovou PR #75 (merge desenv->homolog concluido) — proximo: QA
docs_path: repos/omuletachou/documentacoes/ISSUE-9-publisher-instagram
openspec_path: repos/omuletachou/openspec/changes/issue-9-publisher-instagram
openspec_change: repos/omuletachou/openspec/changes/issue-9-publisher-instagram
ultimo_agente: code-review
status_comment_id: 4927227668
pr_feature: 74 (feature/73-instagram-publisher -> desenv) — MERGED (squash) 2026-07-10T18:28:30Z
pr_homologacao: 75 (desenv -> homolog) — MERGED (merge commit) 2026-07-13T12:33:26Z, mergeCommit af9c26cbb61009113f0789c9eb93085b6ef78047
pr_release: ~ (será criado após QA aprovar, PR homolog -> main)
qa_status: ~
code_review_homolog_pr: 75 — APROVADO na 2ª rodada (revalidação completa apos recuperacao da infra Docker). Build (dotnet build: 0 erros), suite (dotnet test: 156/156) e boot Docker real (docker compose up -d --build db api) executados do zero em worktree isolado (nao reaproveitou a checagem informal da sessao principal). Endpoints validados contra container real: GET /health (200), GET /media/arquivo-inexistente.mp4 (404, confirma UseStaticFiles/PhysicalFileProvider sem excecao), POST /api/jobs/processor/trigger (200), POST /api/jobs/publisher/trigger (200). Logs sem excecao relacionada a InstagramPublisher/DI. Migration SeedInstagramCredentials aplicada e chaves instagram.* seedadas (confirmado via docker exec + psql: access_token/app_id/app_secret/page_id/token_expires_at vazios, token_invalid=false). Stack derrubada ao final (docker compose down -v). Revisão de código sem achados bloqueantes (InstagramPublisher.cs, ProcessorJob.cs, Program.cs, migration). Evidência completa postada em https://github.com/DQM-BETA/omuletachou/pull/75#issuecomment-4957978761. Merge desenv->homolog executado (merge commit, nao squash).
closedAt: ~
ca20_pendente: true — validacao em conta real do Instagram (credenciais reais ainda nao fornecidas pelo Gerente). NAO bloqueia o merge desenv->homolog (ja ocorrido), mas E BLOQUEANTE PARA O GATE 2 (release homolog->main). Confirmado presente e explicito em criterios-aceite.md (CA20) nesta revalidação. Reforçando novamente para não se perder no histórico.

## Contexto
Stack: .NET 8, Meta Graph API (instagram-graph-api), OAuth2
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #6 (ProcessorJob), #7 (PublisherJob/Telegram), #8 (Publisher YouTube) — todas em produção (main)

**Achado técnico confirmado (pattern anterior):** `PublisherJob` já implementa orquestração genérica de publishers via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork`. Adicionar `InstagramPublisher` é puramente aditivo — sem retrofit necessário no PublisherJob. `SocialNetwork.Instagram` já existe no enum.

**Nota sobre o path do openspec change:** o slug gerado pelo `openspec new change` ficou em minúsculas (`issue-9-publisher-instagram`), diferente do padrão maiúsculo usado em `documentacoes/` (`ISSUE-9-publisher-instagram`). Path real registrado acima em `openspec_change`/`openspec_path`.

## PM Fase 1 — levantamento de requisitos (postado na Issue)
Perguntas postadas em https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4927260892, cobrindo autenticação/onboarding, escopo de mídia, caption/disclosure, hospedagem de mídia pública, falhas/retries/rate limit e definição de pronto.

## PM Fase 2 — PRD consolidado (Gate 1 respondido)
Respostas do Gerente em https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4935151980 (postadas 2026-07-10), fechando as 6 perguntas:
1. Token de longa duração (60 dias) gerado manualmente no onboarding via Meta Developer App; salvo em `app_settings` (`instagram.access_token`, `instagram.token_expires_at`). Renovação automática via `fb_exchange_token` com margem de 7 dias. Falha de renovação: `Failed` sem retry + `instagram.token_invalid=true` + alerta no dashboard (mesmo padrão do YouTube).
2. Escopo restrito a Reels (vídeo). Produto sem `MediaType = "video"`: fix retroativo no `ProcessorJob` exclui a rede Instagram da fila (mesmo padrão do CA17 do YouTube, Issue #8).
3. Caption via `GenerateCaptionAsync` (Claude), tom Instagram. Disclosure `#publi`/`#publicidade` anexado automaticamente ao final da legenda — exigência CONAR, não pode ser omitido pela IA.
4. Mídia pública via `UseStaticFiles` do próprio ASP.NET Core (`/app/media/{filename}`), sem CDN externa. Fallback: `MediaUrl` original se `MediaLocalPath` nulo. Retenção indefinida (débito técnico futuro, fora de escopo).
5. Retry padrão (`RetryCount` até 3). Rate limit (25/24h) folgado, sem tratamento especial além de backoff em 429. Fluxo assíncrono: create container → polling a cada 3s, timeout 2min → publish. Timeout sem FINISHED → `Failed`, retry no próximo ciclo.
6. Validação em conta real do Instagram é **obrigatória** antes do Gate 2 (não apenas mock/análise de código) — publicar Reel de teste, confirmar visualmente no perfil, validar disclosure na legenda. Virou CA20 formal em `criterios-aceite.md`.

Entregáveis desta fase:
- `openspec/changes/issue-9-publisher-instagram/proposal.md`
- `documentacoes/ISSUE-9-publisher-instagram/prd.md`
- `documentacoes/ISSUE-9-publisher-instagram/criterios-aceite.md` (CA1–CA20)
- Comentário de sumário do PRD postado na Issue #9.

**Avaliação de ambiguidade arquitetural: SEM ambiguidade.** Stack (.NET 8 + Meta Graph API + HttpClient), protocolo de integração e padrão de credenciais já definidos na Issue e nas respostas do Gate 1. O fluxo de polling (create → poll → publish) é equivalente em natureza ao upload em chunks do YouTube (Issue #8) — sequência de chamadas HTTP contra API externa já definida, sem nova dependência de infraestrutura/storage/fila. Segue direto para o Líder Técnico, sem escalar ao Arquiteto.

## Líder Técnico — refinamento técnico (concluído)
- `design.md` (resumido, PM não escalou ao Arquiteto) escrito em `openspec/changes/issue-9-publisher-instagram/design.md`.
- `especificacao-tecnica.md` escrito em `documentacoes/ISSUE-9-publisher-instagram/especificacao-tecnica.md`: contratos de API da Meta Graph (create container, polling, publish, fb_exchange_token), schema de dados (reaproveita `app_settings`/`Product`/`PublicationQueue`, sem migration nova), padrões obrigatórios (`FailPermanently` vs. `RegisterAttempt` simples no timeout, resolução de URL pública sem download, `UseStaticFiles` ausente hoje em `Program.cs`), decisão de disclosure e fix retroativo no `ProcessorJob`.
- **Decisão de escopo:** 1 sub-issue única (#73), seguindo o padrão da Issue #8 (sub-issue #65) — publisher aditivo + fix retroativo pequeno e acoplado, sem ambiguidade nem necessidade de UI. Justificativa completa em `openspec/changes/issue-9-publisher-instagram/tasks.md`.
- **Decisão de disclosure (`#publi`/`#publicidade`):** pós-processamento isolado dentro do `InstagramPublisher` (regex/Contains determinístico), NÃO alteração do prompt/serviço `ClaudeAiService` — evita risco de regressão nas demais redes e garante determinismo exigido por CA10/CA11 (não depender do LLM seguir instrução).
- **`UseStaticFiles`:** confirmado por leitura direta que NÃO está configurado em `Program.cs` — adicionado como tarefa da sub-issue #73 (pré-requisito do `InstagramPublisher`, mapeando o path físico do `LocalMediaStorage` para `/media`).
- **Fix retroativo no `ProcessorJob`:** reaproveita o método `HasVideoAvailable` já existente (criado na Issue #8 para Youtube), generalizando a condição para também cobrir `SocialNetwork.Instagram` — sem duplicar lógica.
- **CA20:** sinalizado explicitamente na sub-issue #73 — Dev precisa solicitar ao Gerente credenciais reais de teste (App Meta já onboardado manualmente) antes de considerar a task pronta; validação manual obrigatória, não pode ser satisfeita só por mock/CI.
- Comentário de resumo técnico postado na Issue #9: https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4935217327
- Comentário 📍 Status atualizado para "Em Desenvolvimento": https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4927227668

## Líder Técnico — merge sub-issue #73 e PR de release (concluído)
- PR #74 (`feature/73-instagram-publisher` → `desenv`) revisado (build/testes reportados: 156/156 passando pelo Dev). Merge squash executado com sucesso — `mergedAt: 2026-07-10T18:28:30Z`. Branch `feature/73-instagram-publisher` deletada.
- Sub-issue #73 fechada (`gh issue close 73 --reason completed`).
- **Validação de boot Docker: NÃO REALIZADA pelo LT.** O escopo do papel de LT restringe o `Bash` exclusivamente a git/gh/movimentação de arquivos — sem permissão para executar `docker compose up`, `dotnet test` ou qualquer código de aplicação. Não é uma limitação de ambiente (Docker disponível ou não), é trava de escopo do papel. **Portanto o gap de boot Docker segue TOTALMENTE não validado** — nem pelo Dev (Docker Desktop indisponível no sandbox dele), nem pelo LT (sem permissão de execução). Fica formalmente registrado como pendência **obrigatória para o Code Review**, que possui permissão de build/boot/execução: deve rodar `docker compose up -d --build` e validar `/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger`. Se falhar novamente (não só por ausência de permissão, mas por erro real de infra), escalar via DevOps como possível problema de infra transversal.
- Única sub-issue da Issue #9 → todas concluídas (`desenv_tasks_merged` = `sub_issues`). PR de release criado: **#75** (`desenv` → `homolog`), corpo do PR documenta testes, pendência de boot Docker e CA20.
- **CA20 (validação em conta real do Instagram) segue pendente** — não bloqueia este merge desenv→homolog, mas é bloqueante para o Gate 2 (release homolog→main). Repetido aqui e no campo `ca20_pendente` acima para não se perder no histórico.

## Code Review — PR #75, 1ª rodada (BLOQUEADO — infra Docker, não código)
- Diff revisado via `gh pr diff 75` + leitura de `InstagramPublisher.cs`, `ProcessorJob.cs`, `Program.cs`, `AppSettingConfiguration.cs`, migrations, `LocalMediaStorage.cs` e testes (`InstagramPublisherTests.cs`, `ProcessorJobTests.cs`, `JobsTriggerTests.cs`). Sem achados bloqueantes de segurança/correção: sem secrets hardcoded (migration semeia valores vazios), `FailPermanently` vs. `RegisterAttempt` simples aplicados corretamente (CA5 vs CA14/CA15), disclosure determinístico (regex, não depende do LLM), fix retroativo do `ProcessorJob` com cobertura de regressão (CA16-CA18) revisada.
- **Build + suíte executados nativamente (fora do Docker), por execução própria:** checkout do PR em worktree isolado (`git fetch origin pull/75/head` + `git worktree add`). `dotnet build`: compilação com êxito, 0 erros (1 warning pré-existente, não relacionado). `dotnet test`: **156/156 aprovados**, confirmando o número reportado pelo Dev com evidência própria (não apenas leitura do PR).
- **Boot Docker real: TENTATIVA ATIVA REALIZADA, FALHOU por motivo de infra (não código).** `.env` criado a partir de `.env.example`, `docker compose build api` executado no worktree do PR. Docker Desktop (Windows) não conseguiu subir o engine Linux (WSL2 backend) neste ambiente, mesmo após 2 tentativas de iniciar `Docker Desktop.exe` e ~20 minutos de polling em `docker info`. Diagnóstico: `wsl -l -v` mostra `docker-desktop`/`docker-desktop-data` permanentemente `Stopped`; serviço Windows `com.docker.service` está `Stopped` e `Start-Service` retorna erro de permissão. Erro final consistente: `error during connect: ... open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.` **Não foi possível validar** `/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` e `GET /media/{arquivo}` contra Postgres real via `docker compose`.
- **Mitigação parcial já presente no PR:** `JobsTriggerTests.cs` (WebApplicationFactory + InMemory DB, dentro dos 156 testes validados) cobre os 3 endpoints, incluindo o teste específico de que `GET /media/arquivo-inexistente.mp4` retorna 404 (não 500), confirmando que o middleware `UseStaticFiles`/`PhysicalFileProvider` não lança exceção no boot do DI. Isso cobre o *código*, mas não o *ambiente* real (Postgres real, volumes Docker reais).
- **Esta foi a 3ª tentativa consecutiva de boot Docker fracassada nesta Issue** (Dev: Docker Desktop indisponível no sandbox dele; LT: sem permissão de execução no papel; Code Review: tentativa ativa e diagnosticada, falhou por infra do sandbox). Conforme CLAUDE.md da squad (trava anti-loop), recomendou-se escalar como infra transversal via DevOps antes de nova tentativa, em vez de repetir a mesma falha com outro agente.
- Achados detalhados e evidências completas postados no PR: https://github.com/DQM-BETA/omuletachou/pull/75#issuecomment-4938523342
- **Merge NÃO executado nesta rodada** (não é aprovação — status bloqueado, aguardando resolução do bloqueio de infra antes de reavaliar).

## DevOps — diagnóstico Docker/WSL2 (concluído)
- Causa raiz identificada (usuário sem privilégios de admin, serviço `com.docker.service` não inicia sem elevação); sugestão documentada em `.claude/melhorias/2026-07-10-devops-docker-desktop-wsl2-service-permissions.md`; não implementou correção (fora do escopo).

## Code Review — PR #75, 2ª rodada (APROVADO — revalidação completa após recuperação de infra)
- **Refeita do zero, sem confiar na checagem informal da sessão principal.** Worktree isolado (`git fetch origin pull/75/head` + `git worktree add`), HEAD do PR `bf13785`.
- **Build:** `dotnet build` → compilação com êxito, 0 erros (1 warning pré-existente CS0618, não relacionado).
- **Suíte:** `dotnet test` → **156/156 aprovados**, 0 falhas.
- **Boot Docker real (executado com sucesso):** `docker version`/`docker info` confirmaram engine Linux operacional (Docker Desktop 4.81.0). `.env` criado a partir de `.env.example`. `docker compose -p pr75review up -d --build db api` → build da imagem e subida dos containers `afiliado_db`/`afiliado_api` com sucesso. Logs de inicialização revisados: migrations aplicadas em ordem (incluindo `SeedInstagramCredentials`), Hangfire instalado, `Application started`, **sem nenhuma exceção relacionada a `InstagramPublisher` ou DI**.
- **Endpoints validados contra container real:** `GET /health` → 200; `GET /media/arquivo-inexistente.mp4` → **404** (confirma `UseStaticFiles`/`PhysicalFileProvider` sem exceção); `POST /api/jobs/processor/trigger` → 200; `POST /api/jobs/publisher/trigger` → 200. Logs pós-trigger sem exceções reais (grep só bateu na coluna SQL `error_message`, não em falha).
- **Seed de credenciais confirmado via `docker exec afiliado_db psql`:** `instagram.access_token`, `instagram.app_id`, `instagram.app_secret`, `instagram.page_id`, `instagram.token_expires_at` = `""` (placeholders vazios); `instagram.token_invalid` = `"false"`; `api.public_base_url` = `""`. Consistente com o padrão `youtube.*`.
- Stack derrubada ao final (`docker compose down -v`), sem containers/volumes residuais.
- **Revisão de código:** `InstagramPublisher.cs`, `ProcessorJob.cs`, `Program.cs`, migration `SeedInstagramCredentials` — sem achados bloqueantes (detalhes no campo `code_review_homolog_pr` acima).
- **Checklist de veto:** todos os itens ok. `.first()`/`.nth()`/`.last()` não se aplica (PR backend puro, sem specs E2E/Playwright neste diff).
- **CA20** confirmado presente e explícito em `criterios-aceite.md`/`estado.md` como pendência bloqueante apenas para o Gate 2 (não para este merge).
- Evidência completa postada no PR: https://github.com/DQM-BETA/omuletachou/pull/75#issuecomment-4957978761
- **Merge desenv→homolog EXECUTADO** (merge commit, `af9c26cbb61009113f0789c9eb93085b6ef78047`, `mergedAt: 2026-07-13T12:33:26Z`).

## Sub-issues
sub_issues: [#73 (stack:dotnet, task_id:T-01) — "InstagramPublisher + fix retroativo no ProcessorJob" — PR #74 MERGED (squash) em desenv]
desenv_tasks_merged: [#73]

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #9 preparada, estado.md criado, comentario 📍 Status adicionado (id 4927227668), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #9, comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — respostas do Gerente incorporadas, openspec change criado, prd.md + criterios-aceite.md (CA1-CA20) escritos, sem ambiguidade arquitetural, encaminhado ao Líder Técnico |
| 4 | Refinamento Técnico | lider-tecnico | concluido — design.md + especificacao-tecnica.md escritos, sub-issue única #73 criada (InstagramPublisher + fix ProcessorJob + UseStaticFiles), tasks.md com decisão de escopo/disclosure/CA20 documentada, comentário de resumo e status atualizados, encaminhado ao Dev .NET |
| 5 | Dev .NET (sub-issue #73) | dev-dotnet | concluido — `InstagramPublisher` implementado (3 etapas, renovação de token, disclosure determinístico), fix retroativo `ProcessorJob.HasVideoAvailable` generalizado p/ Instagram, `UseStaticFiles` adicionado em `Program.cs`, migration `SeedInstagramCredentials` (app_settings placeholders), 156/156 testes passando (CA1-CA19). Docker Desktop indisponível no sandbox (engine não iniciou após ~10min) — boot do DI validado via `WebApplicationFactory<Program>` (`JobsTriggerTests.cs`, 3 novos testes de boot/trigger). PR #74 aberto (feature/73-instagram-publisher → desenv). **CA20 pendente** (validação em conta real), bloqueante apenas para o Gate 2. |
| 6 | Merge sub-issue #73 + PR release | lider-tecnico | concluido — PR #74 revisado e merged (squash) em desenv (`mergedAt: 2026-07-10T18:28:30Z`), sub-issue #73 fechada. Validação de boot Docker NÃO realizada (fora do escopo de ferramentas do LT — sem permissão para rodar código/infra); registrado como pendência obrigatória para o Code Review. Única sub-issue concluída → PR de release #75 (desenv→homolog) criado. CA20 segue pendente, bloqueante apenas para o Gate 2. |
| 7 | Code Review (PR #75) — 1ª rodada | code-review | bloqueado — build/suíte validados por execução própria (156/156, sem Docker). Boot Docker real tentado ativamente (~20min, 2 tentativas de start, diagnóstico completo via wsl/services) e falhou por infra do sandbox (WSL2/serviço Docker indisponível), não por código do PR. Sem achados de segurança/correção bloqueantes no diff. Merge NÃO executado — recomenda escalar via DevOps antes de nova tentativa (3ª falha consecutiva de boot Docker nesta Issue). |
| 8 | DevOps — diagnóstico Docker/WSL2 | devops | concluido — causa raiz identificada (usuário sem privilégios de admin, serviço `com.docker.service` não inicia sem elevação); sugestão documentada em `.claude/melhorias/2026-07-10-devops-docker-desktop-wsl2-service-permissions.md`; não implementou correção (fora do escopo). |
| 9 | Verificação informal de infra (sessão principal) | sessao-principal | a sessão principal confirmou informalmente que `docker compose up/down` volta a funcionar no host (serviço `com.docker.service` recuperou) — **isso NÃO substitui uma rodada formal de Code Review**; a sessão principal não revisa PR nem certifica aprovação (regra da squad). Próximo passo: spawnar novo agente Code Review para revalidar o PR #75 do zero, agora que a infra está disponível. Não gera linha de custo (overhead do orquestrador não entra no ledger). |
| 10 | Code Review (PR #75) — 2ª rodada (revalidação completa) | code-review | **concluido/aprovado** — build (0 erros) + suíte (156/156) + boot Docker real (docker compose up -d --build) executados do zero em worktree isolado. Endpoints validados contra container real: /health (200), /media/arquivo-inexistente.mp4 (404), /api/jobs/processor/trigger (200), /api/jobs/publisher/trigger (200). Logs sem exceção de InstagramPublisher/DI. Migration SeedInstagramCredentials confirmada aplicada e seedada via psql. Revisão de código sem achados bloqueantes. Checklist de veto ok (`.first()` N/A — sem specs E2E neste PR). CA20 confirmado como pendência clara para o Gate 2. Stack derrubada ao final. **Merge desenv→homolog executado** (merge commit af9c26cbb61009113f0789c9eb93085b6ef78047). Próximo: QA. |
| 11 | QA (homolog) | qa | **concluido/aprovado** — CA1-CA19 aprovados com evidência de execução real (docker compose up, Postgres real, /health 200, /media/arquivo-inexistente 404, triggers 200, chaves instagram.* seedadas confirmadas via psql). CA20 explicitamente marcado "não avaliado nesta rodada — pendente de credenciais reais", sem contar como reprovação. Gate visual N/A (issue 100% backend, confirmado ausência de script test:visual). relatorio-qa.md criado (ficou sem commit inicialmente — corrigido pela sessão principal). Próximo: Líder Técnico (PR release homolog→main). |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 62403 | 35 | 199s |
| 2 | PM Fase 1 | pm | sonnet | 30402 | 9 | 82s |
| 3 | PM Fase 2 | pm | sonnet | 62057 | 26 | 271s |
| 4 | Refinamento LT | lt | sonnet | 81511 | 33 | 251s |
| 5 | Dev #73 (PR #74) | dev-dotnet | sonnet | 184449 | 91 | 1785s |
| 6 | Merge PR #74 + PR release #75 | lt | sonnet | 43278 | 11 | 113s |
| 7 | Code Review PR #75 (bloqueado — infra Docker) | code-review | sonnet | 94747 | 45 | 1359s |
| 8 | DevOps — diagnóstico Docker/WSL2 (fora do board, infra transversal) | devops | haiku | 28875 | 28 | 268s |
| 9 | Code Review PR #75 (2ª rodada — aprovado, revalidação completa) | code-review | sonnet | 83847 | 39 | 412s |
| 10 | QA (homolog) — aprovado (CA1-19, CA20 pendente) | qa | sonnet | 69852 | 22 | 212s |
