# Estado — ISSUE-9: Publisher Instagram Reels

## Campos principais
issue: 9
repo: omuletachou
titulo: feat: Publisher Instagram (Meta Graph API)
rota: normal
etapa_atual: Gate 1 — aguardando resposta do Gerente (perguntas de levantamento postadas)
docs_path: repos/omuletachou/documentacoes/ISSUE-9-publisher-instagram
openspec_path: repos/omuletachou/openspec/changes/ISSUE-9-publisher-instagram
ultimo_agente: pm-analista-negocios
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

**Achado técnico confirmado (pattern anterior):** `PublisherJob` já implementa orquestração genérica de publishers via `IEnumerable<ISocialPublisher>` filtrado por `item.SocialNetwork`. Adicionar `InstagramPublisher` é puramente aditivo — sem retrofit necessário no PublisherJob. A integração será idêntica à de `TelegramPublisher` (Issue #7) e `YoutubePublisher` (Issue #8). `SocialNetwork.Instagram` já existe no enum.

**Objetivo resumido:**
Implementar publicação de Reels (vídeos curtos) no Instagram via Meta Graph API, incluindo:
- Servir mídia em URL pública (requisito da API)
- Fluxo de upload de 3 etapas (create media → polling status → publish)
- Refresh automático de token de acesso (60 dias, renovar quando restar <7 dias)
- Validação de URL pública acessível antes de iniciar o upload

**Requisitos funcionais (do corpo da Issue):**
1. Servir mídia em URL pública (`Program.cs`: `app.UseStaticFiles` mapeando `/app/media` → `/media`)
2. `InstagramPublisher : ISocialPublisher` com fluxo de 3 etapas obrigatórias (POST media → polling GET status → POST media_publish)
3. Validação de URL pública (HEAD request com timeout)
4. Refresh automático de token quando restar <7 dias
5. Ler credenciais de `app_settings`: `instagram.access_token`, `instagram.page_id`
6. Testes com mock da Meta Graph API
7. Critérios de aceite: URL válida retorna sucesso, URL inválida retorna false com mensagem, falha de polling retorna erro descritivo, token renovado automaticamente

**Diferenças vs. Issue #8 (YouTube):**
- YouTube usa Google OAuth2 + YouTube API v3 + upload resumable em chunks
- Instagram usa Meta Graph API + fluxo sequencial simples (3 chamadas síncronas, sem chunking)
- Instagram exige URL pública para mídia (não upload direto como YouTube)
- Ambos exigem refresh de token automático (via credenciais salvas em `app_settings`)

## PM Fase 1 — levantamento de requisitos (postado na Issue)
Perguntas postadas em https://github.com/DQM-BETA/omuletachou/issues/9#issuecomment-4927260892, cobrindo:
1. Autenticação/credenciais: origem do token/App Meta, onboarding, comportamento em falha de refresh
2. Tipo de mídia/formato: confirmar escopo restrito a Reels (vídeo), tratamento de itens sem vídeo
3. Caption: template/regras próprias, disclosure de link de afiliado
4. Hospedagem da mídia pública: storage/CDN existente vs. solução via `/app/media`, retenção do arquivo
5. Falhas/retries/rate limit: retry automático vs. revisão manual, volume esperado
6. Definição de pronto: necessidade de validação em conta real do Instagram antes do Gate 2

Aguardando resposta do Gerente na Issue para prosseguir à Fase 2 (PRD + openspec change + critérios de aceite).

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #9 preparada, estado.md criado, comentario 📍 Status adicionado (id 4927227668), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #9, comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 62403 | 35 | 199s |
