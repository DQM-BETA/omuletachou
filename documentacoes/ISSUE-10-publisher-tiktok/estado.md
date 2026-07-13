# Estado — ISSUE-10: Publisher TikTok

## Campos principais
issue: 10
repo: omuletachou
titulo: feat: Publisher TikTok (Content Posting API)
rota: normal
etapa_atual: Em Desenvolvimento
docs_path: repos/omuletachou/documentacoes/ISSUE-10-publisher-tiktok
openspec_path: repos/omuletachou/openspec/changes/issue-10-publisher-tiktok
openspec_change: repos/omuletachou/openspec/changes/issue-10-publisher-tiktok
ultimo_agente: dev-dotnet
status_comment_id: 4959102860
pr_feature: 78
pr_homologacao: ~
pr_release: ~
qa_status: ~
code_review_homolog_pr: ~
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

## Sub-issues
sub_issues: [#77 (stack:dotnet, task_id:T-01)]
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #10 preparada, estado.md criado, comentario 📍 Status adicionado (id 4959102860), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #10 (credenciais, modo de publicação, formato, disclosure, retry, definição de pronto); comentario 📍 Status atualizado para Gate 1 |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — Gate 1 respondido pelo Gerente; PRD consolidado (prd.md, criterios-aceite.md com 20 CAs, openspec proposal.md); sem ambiguidade arquitetural; comentario 📍 Status atualizado para Refinamento Técnico (LT) |
| 4 | Refinamento Técnico | lider-tecnico | concluido — design.md + especificacao-tecnica.md + tasks.md escritos; sub-issue #77 criada (stack:dotnet, T-01); comentario de resumo postado; 📍 Status atualizado para Em Desenvolvimento |
| 5 | Dev .NET (sub-issue #77) | dev-dotnet | concluido — `TikTokPublisher` (init/upload chunked/polling), `Mp4DurationReader` (parser MP4 dependency-free), `SocialDisclosureHelper` compartilhado (InstagramPublisher refatorado, regressão confirmada), retry 429 local, refresh reativo em 401, migration `SeedTikTokCredentials`, DI registrado. 187 testes passando (100%). Boot Docker Compose validado (`/health`, `/api/jobs/processor/trigger`, `/api/jobs/publisher/trigger` → 200; seed confirmado via psql). PR feature→desenv #78 aberto. CA20 registrado como débito não-bloqueante. |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 67855 | 36 | 190s |
| 2 | PM Fase 1 | pm | sonnet | 35067 | 13 | 140s |
| 3 | PM Fase 2 | pm | sonnet | 67362 | 23 | 324s |
| 4 | Refinamento Técnico | lider-tecnico | sonnet | 92271 | 28 | 307s |
| 5 | Dev .NET (#77, PR #78) | dev-dotnet | sonnet | 177846 | 77 | 980s |

**Consolidação:** a preencher ao fecho da issue.
