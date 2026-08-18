issue: 199
titulo: fix: MercadoLivreCollector falha ao salvar produto com slug maior que 300 caracteres (perde o ciclo inteiro de coleta)
etapa_atual: Code Review
ultimo_agente: lider-tecnico
openspec_change: ~
tech_stacks: [".NET 8", "EF Core", "PostgreSQL"]
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-199-fix-mercadolivre-slug
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_feature: 200
pr_feature_2: 202
pr_feature_merge_commit: 0130af11783a633ed9be4b2c4e6193d9dc4475bb
pr_feature_2_merge_commit: 12331e8c8ca66eda081ebd8953d81683b19f968d
pr_homologacao: 201
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: 5328791206
rota: rapido

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Notas |
|---|---|---|---|---|---|---|---|
| 1 | Preparação | Coordenador | Haiku | 20665 | 8 | 70s | Issue criada, estado.md preparado |
| 2 | Dev .NET (fix slug) | Dev .NET | Sonnet | 86383 | 53 | 395s | Truncagem do slug preservando sufixo do externalId. TDD RED→GREEN, 437/437 testes, boot real contra Postgres validado. PR #200 feature→desenv aberto. |
| 3 | Merge feature→desenv + PR desenv→homolog | Líder Técnico | Sonnet | 30751 | 12 | 78s | PR #200 squash mergeado em desenv (437/437 testes, boot real Postgres validado); PR #201 (desenv→homolog, merge commit) aberto, não mesclado — aguarda Code Review/QA/Gate 2 |
| 4 | Code Review (tentativa 1, inconclusiva) | Code Review | Sonnet | 57668 | 46 | 381s | Agente travou aguardando notificação de job em background que nunca chega para subagentes — não devolveu HANDOFF nem mesclou o PR. Sessão principal assumiu a validação ao vivo diretamente: disparou o coletor real contra a API do Mercado Livre e reproduziu o MESMO erro `varchar(300)` — mas descobriu que o container rodando ainda usava a imagem Docker anterior ao merge do fix (rebuild necessário). Após rebuild+restart, disparou de novo e encontrou um **segundo bug real, diferente**: `Product.UpdateAiResult`/`MarkAsError` gravam `AiReason` (também `varchar(300)`) sem truncar — a resposta real da IA (Claude, não mock) pode passar de 300 caracteres. Afeta as 3 plataformas (Amazon/Shopee/MercadoLivre), não só ML. Adicionando essa correção na mesma Issue antes de reacionar Code Review/QA. |
| 5 | Dev .NET (fix ai_reason, 2ª correção) | Dev .NET | Sonnet | 73198 | 44 | 365s | Truncagem centralizada em `SetAiReason(string? reason)`, reaproveitado por `UpdateAiResult` e `MarkAsError`. TDD RED→GREEN (reason 350 chars truncado p/ 300; textos dentro do limite preservados). 441/441 testes, boot real Docker+Postgres validado. PR #202 feature→desenv aberto. |
| 6 | Merge feature→desenv (2ª correção) | Líder Técnico | Sonnet | ~ | ~ | ~ | PR #202 squash mergeado em desenv (441/441 testes, boot real validado). PR #201 (desenv→homolog) confirmado atualizado automaticamente — headRefOid passou a refletir o novo HEAD de desenv (12331e8c...) sem necessidade de intervenção manual. PR #201 permanece aberto, aguardando Code Review/QA/Gate 2. |
