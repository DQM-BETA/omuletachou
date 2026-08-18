issue: 204
titulo: feat: permitir escolher a quantidade de produtos por lote na tela de importação de links ML (limite real da ferramenta oficial do ML)
etapa_atual: Code Review
ultimo_agente: lider-tecnico
openspec_change: ~
tech_stacks:
  - angular
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-204-ml-links-quantidade-lote/
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_feature: 205
pr_homologacao: 206
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
rota: rapido
status_comment_id: ~

## Notas
- PR #205 (feature/ISSUE-204-quantidade-lote-ml → desenv) merged via squash em 2026-08-18 (commit 045c60d99928493f8343667079184cd824094395).
- Testes reportados pelo Dev: 129/129 unitários verdes, build de produção OK, `ng serve` validado.
- PR #206 (desenv → homolog) criado via merge commit em 2026-08-18. Aguarda Code Review + QA + Gate 2. NÃO mesclar sem aprovação.
- Correção: limite real de 30 URLs por vez na ferramenta oficial do Mercado Livre (descoberto ao vivo pelo Gerente); campo de quantidade por lote editável, respeitado na cópia e na importação de volta.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|------------|-----------|
| 1 | Preparar Issue | coordenador | haiku | — | gh issue create, gh label, gh issue edit | — |
| 2 | Merge PR205 + PR206 | lider-tecnico | sonnet | — | gh pr merge, gh pr create, git | — |
