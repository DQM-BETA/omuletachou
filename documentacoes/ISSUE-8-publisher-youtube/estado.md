# Estado — ISSUE-8: Publisher YouTube Shorts

## Campos principais
issue: 8
repo: omuletachou
titulo: feat: Publisher YouTube Shorts
rota: normal
etapa_atual: PM Fase 2
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

**PRD inicial escrito** em `documentacoes/ISSUE-8-publisher-youtube/prd.md`. Perguntas de Gate 1 postadas na Issue #8 (comentário https://github.com/DQM-BETA/omuletachou/issues/8#issuecomment-4914804542), cobrindo: (1) produto sem vídeo destinado ao YouTube / possível lacuna no ProcessorJob; (2) fonte do arquivo (MediaLocalPath vs MediaUrl); (3) validação de duração/proporção do Short — automática do YouTube ou do publisher; (4) tamanho de chunk / timeout do upload resumable; (5) comportamento em falha de renovação do refresh_token; (6) categoryId fixo "26" vs. mapeado por categoria do produto.

Sem ambiguidade de stack/arquitetura na Issue (já definida: .NET 8 + Google.Apis.YouTube.v3 + OAuth2) — mas há ambiguidades de regra de negócio (ver perguntas acima) a resolver antes da Fase 2.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido |
| 2 | PM Fase 1 | pm | concluido — aguardando Gate 1 |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 50607 | 21 | 129s |
| 2 | PM Fase 1 | pm | sonnet | 40770 | 16 | 107s |
