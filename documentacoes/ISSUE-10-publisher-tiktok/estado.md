# Estado — ISSUE-10: Publisher TikTok

## Campos principais
issue: 10
repo: omuletachou
titulo: feat: Publisher TikTok (Content Posting API)
rota: normal
etapa_atual: Aguardando Aprovação (Gate 2)
docs_path: repos/omuletachou/documentacoes/ISSUE-10-publisher-tiktok
openspec_path: repos/omuletachou/openspec/changes/issue-10-publisher-tiktok
openspec_change: repos/omuletachou/openspec/changes/issue-10-publisher-tiktok
ultimo_agente: lider-tecnico
status_comment_id: 4959102860
pr_feature: 78
pr_homologacao: 79
pr_release: 80
qa_status: aprovado (CA1-CA19; CA20 equivalente pendente de aprovação TikTok, não-bloqueante)
code_review_homolog_pr: 79
closedAt: ~

## Contexto
Stack: .NET 8, TikTok Content Posting API (upload chunked), OAuth2
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #6 (ProcessorJob), #7 (PublisherJob/Telegram), #8 (Publisher YouTube), #9 (Publisher Instagram) — todas em produção (main)

**Achado técnico confirmado (pattern anterior):** `PublisherJob` já implementa orquestração genérica de publishers via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork`. Adicionar `TikTokPublisher` é puramente aditivo — sem retrofit necessário no PublisherJob. `SocialNetwork.TikTok` já existe no enum.

**Nota sobre a rota de complexidade:** Issue classificada com `rota: normal` (tag padrão — sem tag explícita na issue, default é `normal`). Conforme CLAUDE.md → ROTAS.md, pipeline completo: PM Fase 1/2, Arquiteto (se houver ambiguidade), LT, Dev(s), Code Review, QA, Gate 2.

## PM Fase 1 — levantamento de requisitos (concluído)
Perguntas postadas na Issue #10 (comentário https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959140533) cobrindo: credenciais/disponibilidade, modo de publicação, formato/duração, disclosure, retry, definição de pronto.

## Gate 1 — respostas do Gerente (concluído)
Comentário: https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959489840
1. Repete padrão da Issue #9: aprovação TikTok Developer 3-7 dias, sem garantia. Dev implementa contra spec oficial com conta sandbox/unaudited (publica só para contas de teste). Validação em produção = CA equivalente ao CA20 da Issue #9, mas **explicitamente NÃO bloqueante para o Gate 2 desta vez** (decisão antecipada do Gerente, diferente da Issue #9 onde a dispensa só ocorreu manualmente no Gate 2).
2. `FILE_UPLOAD` confirmado (init + PUT chunked + polling), não `PULL_FROM_URL`. `privacy_level` configurável via `app_settings` (`tiktok.privacy_level`), default `SELF_ONLY` em dev/teste, trocado manualmente para `PUBLIC_TO_EVERYONE` em produção pelo operador.
3. Validação de duração client-side obrigatória (3s-10min, parametrizável via `tiktok.min_duration_seconds`/`tiktok.max_duration_seconds`, seed 3/600). Fora do intervalo: `Failed` sem retry, mensagem específica. Proporção/resolução não precisam validação prévia.
4. Disclosure: `brand_content_toggle = true` no `/video/init` + `#publi` anexado à legenda (mesmo padrão determinístico da Issue #9).
5. Retry: backoff exponencial em 429 (3 tentativas, 2s/4s/8s), `RetryCount` até 3 no `PublisherJob`. Rate limit 6/min folgado, sem tratamento adicional.
6. Definição de pronto: dev prossegue com sandbox/`SELF_ONLY`, validação real de produção pendente mas explicitamente NÃO bloqueante para o Gate 2 desta vez.

## PM Fase 2 — PRD consolidado (concluído)
- `prd.md`, `criterios-aceite.md` (20 CAs) e `openspec/changes/issue-10-publisher-tiktok/proposal.md` escritos.
- Comentário de sumário do PRD postado: https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959522138
- **Avaliação de ambiguidade arquitetural: SEM ambiguidade.** Fluxo `FILE_UPLOAD` (upload chunked) é estruturalmente equivalente ao upload resumable já implementado para YouTube (Issue #8) — mesma natureza de integração HTTP com API externa, sem nova infraestrutura. Disclosure duplo (`brand_content_toggle` + hashtag) é decisão de negócio (parâmetro adicional no payload já existente), não arquitetural. Segue direto para o Líder Técnico, sem escalar ao Arquiteto.
- **Débito de validação formalizado como CA20**, com nota explícita de que NÃO bloqueia o Gate 2 desta issue (aprendizado da Issue #9).

## Líder Técnico — refinamento técnico (concluído)
- `design.md` escrito (sem escalada ao Arquiteto, conforme PM Fase 2): `openspec/changes/issue-10-publisher-tiktok/design.md`.
- `especificacao-tecnica.md` escrito: `documentacoes/ISSUE-10-publisher-tiktok/especificacao-tecnica.md` (contratos de API TikTok, schema de `app_settings`, padrões obrigatórios — `Mp4DurationReader`, `SocialDisclosureHelper` compartilhado com Instagram, retry 429 local).
- Decisões técnicas documentadas (pedidas no escopo do refinamento):
  1. **Duração do vídeo**: sem lib externa/ffmpeg — `Mp4DurationReader` dependency-free (parse do átomo MP4 `moov/mvhd`).
  2. **Disclosure duplo**: extrair `SocialDisclosureHelper` compartilhado entre Instagram e TikTok (reuso, não duplicação) — refatora `InstagramPublisher` para consumir o helper, comportamento inalterado.
  3. **Retry 429**: sem Polly (não há esse padrão no projeto) — helper local `SendWithRetryAsync` só no `TikTokPublisher`.
  4. **Credenciais/refresh**: TikTok exige `refresh_token` (confirmado no PRD/CA16) — mesmo padrão de `YoutubePublisher.RefreshAccessTokenAsync`. Nova migration `SeedTikTokCredentials` (`tiktok.client_key`, `tiktok.client_secret`, `tiktok.refresh_token`, `tiktok.privacy_level` seed `SELF_ONLY`, `tiktok.min_duration_seconds` seed `3`, `tiktok.max_duration_seconds` seed `600`); `tiktok.access_token`/`tiktok.open_id` já existem (ids 18/19), não recriados.
- `tasks.md` escrito com task única T-01 (escopo coeso, sem UI): `openspec/changes/issue-10-publisher-tiktok/tasks.md`.
- Sub-issue #77 criada: https://github.com/DQM-BETA/omuletachou/issues/77 (label `stack:dotnet`).
- Comentário de resumo técnico postado na Issue #10: https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959594604
- Comentário 📍 Status atualizado para "Em Desenvolvimento".
- Sem UI/mobile envolvido — próximo agente é Dev .NET direto (não passa por UX/UI).

## Dev .NET — sub-issue #77 (concluído)
- `TikTokPublisher` (init/upload chunked/polling), `Mp4DurationReader` (parser MP4 dependency-free), `SocialDisclosureHelper` compartilhado (InstagramPublisher refatorado, regressão confirmada), retry 429 local, refresh reativo em 401, migration `SeedTikTokCredentials`, DI registrado.
- 187/187 testes passando. Boot Docker Compose validado (`/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` → 200; seed confirmado via psql).
- PR #78 (`feature/77-tiktok-publisher` → `desenv`).

## Líder Técnico — merge sub-issue #77 + PR de release (concluído)
- Revisão rápida do PR #78 (diff consistente: TikTokPublisher aditivo, refactor de InstagramPublisher para consumir `SocialDisclosureHelper` sem regressão, nova migration, DI registrado). 187/187 testes reportados pelo Dev; boot Docker validado pelo Dev.
- Merge squash de PR #78 → `desenv` (`feat(ISSUE-77): TikTokPublisher + fluxo FILE_UPLOAD`), mergeCommit `892edd2`.
- Sub-issue #77 fechada (`gh issue close 77 --reason completed`).
- Única sub-issue da Issue #10 → todas concluídas (`desenv_tasks_merged` = `sub_issues`).
- PR de release criado: #79 (`desenv` → `homolog`), corpo revisado para **não** incluir keyword de auto-close (Closes #10 removido — a Issue #10 só fecha após o Gate 2 e merge homolog→main, via Coordenador).
- **CA20 (validação em conta real do TikTok) confirmado como débito de acompanhamento, registrado no Gate 1 acima — explicitamente NÃO bloqueante para o Gate 2 desta issue (decisão do Gerente).** Sem ação adicional necessária aqui.
- Branch local `desenv` limpa após merge (sem alterações pendentes; apenas diretório `.worktrees/` não rastreado, pré-existente).

## Code Review — PR #79 homologação (concluído, aprovado)
- Build limpo (`dotnet build`): 0 erros, 1 warning pré-existente (CS0618, Hangfire, não relacionado ao PR).
- Suíte completa (`dotnet test`): **187/187 aprovados** (confirmado independentemente, bate com o reportado pelo Dev).
- **Regressão Instagram confirmada:** `InstagramPublisherTests.cs` não foi modificado neste PR; rodado isoladamente (`--filter FullyQualifiedName~InstagramPublisherTests`) → **20/20 passando**, sem alteração no arquivo de teste. Refactor do `InstagramPublisher` para consumir `SocialDisclosureHelper.AppendIfMissing` preserva regex (`#publi\b|#publicidade\b`) e lógica de anexação — comportamento idêntico.
- `Mp4DurationReader`: não foi prático testar contra MP4 real neste ambiente (sem ffmpeg/amostra disponível). Confirmado que `Mp4DurationReaderTests.cs` cobre todos os casos de borda exigidos: `mvhd` v0 (32-bit) e v1 (64-bit), `moov` ausente, `mvhd` ausente dentro de `moov`, arquivo corrompido/inválido, arquivo inexistente, timescale zero.
- **Boot Docker real:** `docker compose up -d --build db api` → build e subida OK. `/health` → 200, `/api/jobs/processor/trigger` → 200, `/api/jobs/publisher/trigger` → 200 (logs Hangfire confirmam execução real dos jobs contra Postgres real).
- **Migration `SeedTikTokCredentials` confirmada aplicada** via `__EFMigrationsHistory` (psql). Novas chaves seedadas corretamente (ids 41-46: `client_key`/`client_secret`/`refresh_token` vazios sem segredo commitado, `privacy_level=SELF_ONLY`, `min_duration_seconds=3`, `max_duration_seconds=600`). **Confirmado que `tiktok.access_token`/`tiktok.open_id` (ids 18/19, pré-existentes) não foram duplicados nem sobrescritos** — mesmos ids e valores de antes.
- Nenhum comentário do plugin `/code-review` encontrado no PR no momento da revisão (nada a incorporar).
- Checklist de veto: sem teste-lixo, sem segredo commitado, sem uso de `.first()`/`.nth()` (PR 100% backend, sem E2E/Playwright), integração real coberta na medida do possível (boot Docker + Postgres real; HTTP externo do TikTok mockado via fake `HttpMessageHandler`, mesmo padrão de `YoutubePublisherTests`/`InstagramPublisherTests`), conformidade com `design.md`/`criterios-aceite.md` confirmada.
- **CA20 reconfirmado como débito não-bloqueante** (decisão do Gerente no Gate 1 desta issue) — nenhuma ação adicional necessária.
- Evidência completa postada no PR: https://github.com/DQM-BETA/omuletachou/pull/79#issuecomment-4959862215
- **Merge executado:** PR #79 mergeado (`--merge`, merge commit `b6149d8`) `desenv` → `homolog`.

## QA (homolog) — concluído, aprovado
- 187/187 testes confirmados via `dotnet test`. Docker Compose real (`db`+`api`), `/health` 200, triggers de jobs 200.
- Seeds `tiktok.*` confirmados via psql: `privacy_level=SELF_ONLY`, `min_duration_seconds=3`, `max_duration_seconds=600`, `networks.tiktok.enabled=true`.
- CA1-CA18 confirmados por inspeção de código + suíte de testes (init com brand_content_toggle, upload chunked com Content-Range, polling 15s/10min, disclosure via SocialDisclosureHelper, refresh reativo em 401, backoff 429).
- **Regressão do Instagram reconfirmada em ambiente real:** repetido o teste do fallback "sem vídeo" contra o container (`GET /media/{inexistente}` → 404), suíte do Instagram passando.
- Gate visual N/A (sem UI no diff, confirmado via package.json).
- **CA20 (equivalente) marcado "não avaliado nesta rodada — pendente de aprovação do app TikTok"**, não contabilizado como reprovação (decisão do Gate 1).
- Relatório: `relatorio-qa.md`. Comentário "✅ QA aprovado" postado na Issue #10.
- Próximo: Líder Técnico cria PR de release `homolog` → `main`.

## Líder Técnico — PR de release homolog→main (concluído)
- PR #80 criado (`homolog` → `main`, título "[ISSUE-10] Release: Publisher TikTok"), merge commit (NUNCA squash) a ser usado no merge final pelo Coordenador após o Gate 2.
- Corpo do PR documenta o escopo completo (TikTokPublisher, Mp4DurationReader, SocialDisclosureHelper compartilhado, retry/refresh, migration SeedTikTokCredentials) e a qualidade validada (187/187 testes, Code Review e QA aprovados).
- **Débito de acompanhamento registrado no PR sem linguagem de bloqueio:** validação em conta real do TikTok (CA20 equivalente) segue pendente de aprovação do app pelo TikTok Developer Portal, mas explicitamente NÃO bloqueia este release — decisão do Gerente no Gate 1 desta issue. PR pode ser mergeado normalmente assim que o Gate 2 for aprovado.
- `Closes #10` incluído no corpo do PR (fechamento da Issue ocorre no merge final para `main`, feito pelo Coordenador após aprovação do Gerente).
- NÃO mergeado — aguarda aprovação do Gerente (Gate 2). NÃO mexido comentário 📍 Status nem Kanban (fora do escopo desta invocação).

## Sub-issues
sub_issues: [#77 (stack:dotnet, task_id:T-01)]
desenv_tasks_merged: [#77]

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #10 preparada, estado.md criado, comentario 📍 Status adicionado (id 4959102860), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #10 (credenciais, modo de publicação, formato, disclosure, retry, definição de pronto); comentario 📍 Status atualizado para Gate 1 |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — Gate 1 respondido pelo Gerente; PRD consolidado (prd.md, criterios-aceite.md com 20 CAs, openspec proposal.md); sem ambiguidade arquitetural; comentario 📍 Status atualizado para Refinamento Técnico (LT) |
| 4 | Refinamento Técnico | lider-tecnico | concluido — design.md + especificacao-tecnica.md + tasks.md escritos; sub-issue #77 criada (stack:dotnet, T-01); comentario de resumo postado; 📍 Status atualizado para Em Desenvolvimento |
| 5 | Dev .NET (sub-issue #77) | dev-dotnet | concluido — `TikTokPublisher` (init/upload chunked/polling), `Mp4DurationReader` (parser MP4 dependency-free), `SocialDisclosureHelper` compartilhado (InstagramPublisher refatorado, regressão confirmada), retry 429 local, refresh reativo em 401, migration `SeedTikTokCredentials`, DI registrado. 187 testes passando (100%). Boot Docker Compose validado (`/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` → 200; seed confirmado via psql). PR feature→desenv #78 aberto. CA20 registrado como débito não-bloqueante. |
| 6 | Merge sub-issue #77 + PR release | lider-tecnico | concluido — PR #78 revisado e squash-merged em `desenv` (892edd2); sub-issue #77 fechada; todas as sub-issues concluídas; PR #79 (desenv→homolog) criado; CA20 confirmado não-bloqueante no estado; branch local limpa |
| 7 | Code Review (PR #79 homologação) | code-review | concluido — build limpo, 187/187 testes (independente), regressão Instagram confirmada (20/20 isolado, arquivo de teste inalterado), Mp4DurationReader com edge cases cobertos por unit tests, boot Docker real validado (/health, triggers 200), migration SeedTikTokCredentials confirmada via psql (ids 18/19 preservados, ids 41-46 novos), CA20 reconfirmado não-bloqueante, checklist de veto ok. Merge #79 → homolog executado (merge commit b6149d8). |
| 8 | QA (homolog) | qa | concluido — 187/187 testes, Docker Compose real, seeds tiktok.* confirmados via psql, CA1-CA19 aprovados com evidência real, CA20 equivalente marcado não avaliado/não-bloqueante (decisão Gate 1). Relatório relatorio-qa.md + comentário "✅ QA aprovado" na Issue #10. |
| 9 | PR de release (homolog→main) | lider-tecnico | concluido — PR #80 criado, corpo com débito de acompanhamento sem linguagem de bloqueio (CA20 equivalente), Closes #10 incluído. Aguardando Gate 2 (Gerente). Sem merge, sem alteração no comentário 📍 Status/Kanban. |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 67855 | 36 | 190s |
| 2 | PM Fase 1 | pm | sonnet | 35067 | 13 | 140s |
| 3 | PM Fase 2 | pm | sonnet | 67362 | 23 | 324s |
| 4 | Refinamento Técnico | lider-tecnico | sonnet | 92271 | 28 | 307s |
| 5 | Dev .NET (#77, PR #78) | dev-dotnet | sonnet | 177846 | 77 | 980s |
| 6 | Merge #77 + PR release #79 | lider-tecnico | sonnet | 42558 | 11 | 124s |
| 7 | Code Review PR #79 (aprovado, merge homolog) | code-review | sonnet | 103906 | 43 | 410s |
| 8 | QA (homolog) — aprovado (CA1-19, CA20 pendente) | qa | sonnet | 69803 | 19 | 214s |
| 9 | PR release #80 (homolog→main) | lider-tecnico | sonnet | ~ | ~ | ~ |

**Consolidação:** a preencher ao fecho da issue.
