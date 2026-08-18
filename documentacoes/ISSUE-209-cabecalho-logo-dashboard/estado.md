---
issue: 209
titulo: fix: cabeçalho/logo do dashboard não está renderizando corretamente
etapa_atual: Backlog
ultimo_agente: coordenador
openspec_change: ~
tech_stacks:
  - angular
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-209-cabecalho-logo-dashboard
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: 5332288701
---

## Contexto
Na tela `Products` (e possivelmente em todas as telas, já que é parte do shell/layout compartilhado), o texto/logo "omuletachou" no topo da barra lateral azul não aparece corretamente — parece cortado/mal posicionado no topo, sobrepondo o limite superior da barra.

## Investigação
`dashboard/src/app/core/shell/shell.component.html`/`.scss` — componente de shell/layout compartilhado usado em todas as telas autenticadas.

## Aceite
- [ ] Dev reproduz ao vivo (`ng serve` ou via container em `localhost:8081`, logado)
- [ ] CSS do cabeçalho/logo inspecionado e corrigido no posicionamento/recorte
- [ ] Screenshot antes/depois anexado ao PR
- [ ] QA valida visualmente em múltiplas resoluções

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|---|---|---|---|---|---|
