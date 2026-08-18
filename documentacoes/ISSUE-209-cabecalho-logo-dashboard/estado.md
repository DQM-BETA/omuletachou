---
issue: 209
titulo: fix: cabeçalho/logo do dashboard não está renderizando corretamente
etapa_atual: Aguardando Aprovação (Gate 2)
ultimo_agente: lider-tecnico
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
pr_release: 214
code_review_homolog_pr: 213
qa_status: aprovado
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
- [x] QA valida visualmente em múltiplas resoluções

## Resultado da investigação
Em viewport padrão (desktop, ~1280x800) o bug não é visualmente perceptível. Reproduzido de forma determinística simulando viewport baixo (1280x350) + scroll do sidenav: o `mat-toolbar` (cabeçalho "omuletachou") era filho direto do `mat-sidenav`, na mesma área de scroll do `mat-nav-list` — quando o conteúdo do menu excede a altura da janela (janelas baixas, ou a lista crescendo como na Issue #185, hoje com 7 itens) e o sidenav rola, o cabeçalho é arrastado para fora da área visível, cortando/sobrepondo o texto no limite superior da barra azul.

## Correção (PR #212)
`shell.component.html`/`.scss`: `.shell-sidenav` em `display:flex; flex-direction:column`; `mat-toolbar` com `position:sticky; top:0` isolado do scroll; `mat-nav-list` isolado em container próprio com `overflow-y:auto`; texto do logo em elemento dedicado com proteção contra overflow (ellipsis).

## Merge feature→desenv
PR #212 (`feature/ISSUE-209-fix-cabecalho-dashboard` → `desenv`) mesclado via squash em 2026-08-18 (commit `08ce80f`). Testes reportados pelo Dev: 132/132.

## PR de homologação
PR #213 (`desenv` → `homolog`) — compartilhado com a Issue #210 (mesmo par de branches `desenv`→`homolog`, não é possível abrir dois PRs para o mesmo head/base; o corpo do PR foi atualizado para cobrir ambas as issues).

**Nota para o QA:** o bug só é reproduzível de forma determinística em viewport muito baixo (ex. altura 350px) + sidenav rolado — não aparece em viewport desktop padrão. Detalhes no corpo do PR #213.

## Code Review (2026-08-18): APROVADO
PR #213 mesclado `desenv→homolog` via merge commit `adfcfea5ae7202f20553782968218d37d4d10cfd`. Evidência completa no comentário do PR (https://github.com/DQM-BETA/omuletachou/pull/213#issuecomment-5332589071). Resumo específico da Issue #209:
- `docker compose build --no-cache api dashboard` + boot real (`db`/`api` healthy, `dashboard` up); confirmado que o bundle servido contém `shell-toolbar-logo` (código novo, não stale).
- **Validação em browser real (Chromium via Playwright)** logado com usuário seed, contra a app servida pelo container: viewport 1280×350 com `mat-nav-list` rolado até o fim → `.shell-toolbar` manteve `boundingBox {x:0,y:0,width:239,height:64}` (fixo no topo, não cortado) e `[data-testid="shell-logo"]` renderizou texto completo "omuletachou". Screenshots capturados (antes: cenário reproduzido; depois: header intacto). Viewport desktop padrão (1280×800) também validado sem regressão.
- Suíte Karma completa (compartilhada com Issue #210): 134/134 verdes.
- `etapa_atual` → QA. Apto a seguir.

## QA (2026-08-18): APROVADO
`documentacoes/ISSUE-209-cabecalho-logo-dashboard/relatorio-qa.md`. Backend 441/441, dashboard 134/134, gate visual com screenshots (viewport 1280x800 e 1280x350 antes/depois do scroll) e validação E2E manual via Playwright contra containers Docker reais. 100% dos critérios de aceite validados. Nenhuma issue encontrada.

## PR de release
PR #214 (`homolog` → `main`) aberto em 2026-08-18, cobrindo Issue #209 e Issue #210 (Closes #209, Closes #210). Merge commit `adfcfea5ae7202f20553782968218d37d4d10cfd` é a base validada em homolog. Aguardando aprovação do Gerente (Gate 2) — merge NÃO realizado pelo LT.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|---|---|---|---|---|---|
