# Estado — ISSUE-8: Publisher YouTube Shorts

## Campos principais
issue: 8
repo: omuletachou
titulo: feat: Publisher YouTube Shorts
rota: normal
etapa_atual: Líder Técnico — refinamento técnico
docs_path: repos/omuletachou/documentacoes/ISSUE-8-publisher-youtube
openspec_path: repos/omuletachou/openspec/changes/ISSUE-8-publisher-youtube
ultimo_agente: pm
status_comment_id: 4914784828
pr_homologacao: ~
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

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |
| 3 | Gate 1 | Gerente | concluido — 6 perguntas respondidas |
| 4 | PM Fase 2 | pm | concluido — sem escalada ao Arquiteto |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 50607 | 21 | 129s |
| 2 | PM Fase 1 | pm | sonnet | 40770 | 16 | 107s |
