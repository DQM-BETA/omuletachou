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
pr_feature_merge_commit: 0130af11783a633ed9be4b2c4e6193d9dc4475bb
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
| 1 | Preparação | Coordenador | Haiku | — | — | — | Issue criada, estado.md preparado |
| 2 | Merge feature→desenv + PR desenv→homolog | Líder Técnico | Sonnet | — | — | — | PR #200 squash mergeado em desenv (437/437 testes, boot real Postgres validado); PR #201 (desenv→homolog, merge commit) aberto, não mesclado — aguarda Code Review/QA/Gate 2 |
