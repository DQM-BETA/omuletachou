issue: 260
titulo: feat: busca textual inteligente (fonética/fuzzy) na tela de produtos do site público
rota: normal
etapa_atual: Aguardando PM Fase 1
ultimo_agente: coordenador
openspec_change: ~
tech_stacks: []
repos:
  omuletachou: true
repo_path: repos/omuletachou
docs_path: documentacoes/ISSUE-260-busca-textual-inteligente
openspec_path: ~
status_comment_id: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tempo (s) |
|---|---|---|---|---|---|
| 1 | Preparar demanda | Coordenador | Haiku 4.5 | 1234 | 5 |

---

### Notas
- Item 4 da Issue #230, separado por decisão do Gerente no Gate 1 (2026-08-20)
- Restrição vinculante: técnica de BD (ex.: pg_trgm no Postgres), **NÃO** chamada à IA por requisição
- Referência: Issue #230 (itens 1-3, mesmo componente filter-bar)
