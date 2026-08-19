issue: 223
titulo: fix: nginx do dashboard derruba o disparo de jobs longos com timeout (504) antes do job terminar
etapa_atual: Code Review
ultimo_agente: lt
openspec_change: ~
tech_stacks: []
repos: {}
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-223-nginx-timeout
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: 225
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Notas
- Rota: rapido (pula PM, Arquiteto, UX/UI, Gate 1)
- Labels aplicados: bug, stack:angular, rapido
- Descrição técnica completa já fornecida na issue — pronta para Dev
- PR #224 (feature/ISSUE-223-nginx-timeout → desenv): squash mergeado por LT em 2026-08-18T22:39:13Z. Dev validou ao vivo (job real 338.8s, HTTP 200, 122 produtos persistidos, sem timeout prematuro).
- PR #225 (desenv → homolog): criado por LT, merge commit pendente (aguarda Code Review + QA + Gate 2). NÃO mergear ainda.
