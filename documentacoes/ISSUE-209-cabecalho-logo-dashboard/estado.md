---
issue: 209
titulo: fix: cabeçalho/logo do dashboard não está renderizando corretamente
etapa_atual: Code Review
ultimo_agente: lt
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
pr_homologacao: 213
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
- [x] Dev reproduz ao vivo (`ng serve`, build de produção estático e container Docker real em `localhost:8081`, logado)
- [x] CSS do cabeçalho/logo inspecionado e corrigido no posicionamento/recorte
- [x] Screenshot antes/depois anexado ao PR
- [ ] QA valida visualmente em múltiplas resoluções

## Resultado da investigação
Em viewport padrão (desktop, ~1280x800) o bug não é visualmente perceptível. Reproduzido de forma determinística simulando viewport baixo (1280x350) + scroll do sidenav: o `mat-toolbar` (cabeçalho "omuletachou") era filho direto do `mat-sidenav`, na mesma área de scroll do `mat-nav-list` — quando o conteúdo do menu excede a altura da janela (janelas baixas, ou a lista crescendo como na Issue #185, hoje com 7 itens) e o sidenav rola, o cabeçalho é arrastado para fora da área visível, cortando/sobrepondo o texto no limite superior da barra azul.

## Correção (PR #212)
`shell.component.html`/`.scss`: `.shell-sidenav` em `display:flex; flex-direction:column`; `mat-toolbar` com `position:sticky; top:0` isolado do scroll; `mat-nav-list` isolado em container próprio com `overflow-y:auto`; texto do logo em elemento dedicado com proteção contra overflow (ellipsis).

## Merge feature→desenv
PR #212 (`feature/ISSUE-209-fix-cabecalho-dashboard` → `desenv`) mesclado via squash em 2026-08-18 (commit `08ce80f`). Testes reportados pelo Dev: 132/132.

## PR de homologação
PR #213 (`desenv` → `homolog`) — compartilhado com a Issue #210 (mesmo par de branches `desenv`→`homolog`, não é possível abrir dois PRs para o mesmo head/base; o corpo do PR foi atualizado para cobrir ambas as issues). Aguardando Code Review + QA + Gate 2.

**Nota para o QA:** o bug só é reproduzível de forma determinística em viewport muito baixo (ex. altura 350px) + sidenav rolado — não aparece em viewport desktop padrão. Detalhes no corpo do PR #213.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|---|---|---|---|---|---|
