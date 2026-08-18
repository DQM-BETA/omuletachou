---
issue: 210
titulo: fix: tooltip de motivo do erro aparece ao passar o mouse no AI Score, não no Status
etapa_atual: Backlog
ultimo_agente: coordenador
openspec_change: ~
tech_stacks:
  - angular
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-210-tooltip-erro-ai-score
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
status_comment_id: 5332289407
---

## Contexto
Na tela `Products`, quando um produto está com Status = `Error`, a mensagem explicando o motivo do erro (ex.: "Nenhuma rede social habilitada com credenciais válidas para publicar este produto.") aparece como tooltip ao passar o mouse sobre a coluna **AI Score**, não sobre a coluna **Status** — o que é contraintuitivo, já que o motivo é do erro (Status), não da nota da IA.

## Investigação
Componente da tela de produtos no dashboard (provavelmente `dashboard/src/app/pages/products/products.component.html`), procurar o binding do tooltip (`matTooltip` ou similar) e verificar em qual elemento/coluna ele está de fato anexado.

## Aceite
- [ ] Dev reproduz ao vivo (`ng serve` ou via container em `localhost:8081`, logado)
- [ ] Tooltip do motivo do erro movido para a coluna Status (ex.: badge "Error")
- [ ] Comportamento mantido (mostrar `ai_reason` ao passar o mouse) no elemento correto
- [ ] QA valida a mudança de posição do tooltip

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|---|---|---|---|---|---|
