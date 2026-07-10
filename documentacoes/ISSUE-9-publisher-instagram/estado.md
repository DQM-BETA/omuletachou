# Estado — ISSUE-9: Publisher Instagram Reels

## Campos principais
issue: 9
repo: omuletachou
titulo: feat: Publisher Instagram (Meta Graph API)
rota: normal
etapa_atual: Em Desenvolvimento (Dev .NET)
docs_path: repos/omuletachou/documentacoes/ISSUE-9-publisher-instagram
openspec_path: repos/omuletachou/openspec/changes/issue-9-publisher-instagram
openspec_change: repos/omuletachou/openspec/changes/issue-9-publisher-instagram
ultimo_agente: lider-tecnico
status_comment_id: 4927227668
pr_homologacao: ~
pr_release: ~
qa_status: ~
code_review_homolog_pr: ~
closedAt: ~

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

## Sub-issues
sub_issues: [#73 (stack:dotnet, task_id:T-01) — "InstagramPublisher + fix retroativo no ProcessorJob"]
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #9 preparada, estado.md criado, comentario 📍 Status adicionado (id 4927227668), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #9, comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — respostas do Gerente incorporadas, openspec change criado, prd.md + criterios-aceite.md (CA1-CA20) escritos, sem ambiguidade arquitetural, encaminhado ao Líder Técnico |
| 4 | Refinamento Técnico | lider-tecnico | concluido — design.md + especificacao-tecnica.md escritos, sub-issue única #73 criada (InstagramPublisher + fix ProcessorJob + UseStaticFiles), tasks.md com decisão de escopo/disclosure/CA20 documentada, comentário de resumo e status atualizados, encaminhado ao Dev .NET |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 62403 | 35 | 199s |
| 2 | PM Fase 1 | pm | sonnet | 30402 | 9 | 82s |
| 3 | PM Fase 2 | pm | sonnet | 62057 | 26 | 271s |
| 4 | Refinamento LT | lt | sonnet | 81511 | 33 | 251s |
