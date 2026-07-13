# Estado — ISSUE-10: Publisher TikTok

## Campos principais
issue: 10
repo: omuletachou
titulo: feat: Publisher TikTok (Content Posting API)
rota: normal
etapa_atual: Gate 1 — aguardando resposta do Gerente
docs_path: repos/omuletachou/documentacoes/ISSUE-10-publisher-tiktok
openspec_path: repos/omuletachou/openspec/changes/issue-10-publisher-tiktok
openspec_change: ~
ultimo_agente: pm-analista-negocios
status_comment_id: 4959102860
pr_feature: ~
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

**Achado técnico confirmado (pattern anterior):** `PublisherJob` já implementa orquestração genérica de publishers via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork`. Adicionar `TikTokPublisher` é puramente aditivo — sem retrofit necessário no PublisherJob. `SocialNetwork.TikTok` já existe no enum (utilizado inclusive em testes de regressão anteriores).

**Nota sobre a rota de complexidade:** Issue classificada com `rota: normal` (tag padrão — sem tag explícita na issue, default é `normal`). Conforme CLAUDE.md → ROTAS.md, pipeline completo: PM Fase 1/2, Arquiteto (se houver ambiguidade), LT, Dev(s), Code Review, QA, Gate 2.

## PM Fase 1 — levantamento de requisitos (concluído)
Perguntas postadas na Issue #10 (comentário https://github.com/DQM-BETA/omuletachou/issues/10#issuecomment-4959140533) cobrindo:
1. Credenciais/disponibilidade — app TikTok for Developers aprovado para Content Posting API? Credenciais reais disponíveis ou pendência recorrente (repete o padrão do CA20 dispensado na Issue #9)? Preferência: iniciar dev com mocks e deixar validação real pendente, ou aguardar credenciais antes de abrir sub-issue de Dev?
2. Modo de publicação — confirmar `FILE_UPLOAD` (init + PUT chunked + polling) em vez de `PULL_FROM_URL`; `privacy_level: PUBLIC_TO_EVERYONE` fixo ou configurável (rascunho/privado para teste)?
3. Conteúdo/formato — vídeo já vem formatado (proporção/resolução) de etapa anterior do pipeline, ou o Publisher precisa validar proporção (ex. 9:16)? Duração 3s–10min é regra fixa ou parametrizável?
4. Disclosure — exigência de "Paid partnership"/"Promotional content" (brand_content_toggle) se aplica a conteúdo de afiliado? Caption/hashtags seguem o mesmo padrão do Instagram (Issue #9)?
5. Retry/rate limit — mesmo padrão das demais redes (Telegram/YouTube/Instagram) ou tratamento particular (ex. draft para reprocessamento manual)?
6. Definição de pronto — aceitar antecipadamente validação real pendente (como Issue #9) com testes mockados cobrindo o fluxo, ou Gate 2 exige validação real concluída?

Aguardando resposta do Gerente (Gate 1).

## PM Fase 2 — PRD consolidado (pendente)
Aguardando resposta do Gate 1.

## Líder Técnico — refinamento técnico (pendente)
Aguardando tras PM Fase 2.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #10 preparada, estado.md criado, comentario 📍 Status adicionado (id 4959102860), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #10 (credenciais, modo de publicação, formato, disclosure, retry, definição de pronto); comentario 📍 Status atualizado para Gate 1 |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 67855 | 36 | 190s |

**Consolidação:** a preencher ao fecho da issue.
