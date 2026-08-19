---
issue: 228
titulo: feat: exibir quantidade de produtos publicados no site na tela Reports
etapa_atual: Backlog — aguardando priorização do Gerente
ultimo_agente: coordenador
openspec_change: ~
tech_stacks: []
repos:
  omuletachou: "https://github.com/DQM-BETA/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados
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
status_comment_id: ~
---

## Resumo
Adicionar indicador de quantidade de produtos publicados no site à tela `Reports`.

## Contexto
A tela `Reports` (`localhost:8081/reports`) mostra cards de "Hoje/Semana/Mês" e gráfico de "Publicações por rede (últimos 7 dias)" — ambos baseados na fila de publicação social (`publication_queue`). Desde Issue #208, a visibilidade no site é desacoplada da rede social (um produto pode estar `Published` no site sem passar pela fila social). Falta indicador de quantos produtos estão realmente publicados e visíveis no site.

## Pedido
Adicionar algum indicador de quantos produtos estão carregados/publicados no site.

## Investigação necessária (refinamento)
- Formato: card numérico simples ("Produtos no site: N") ou detalhado (por categoria/plataforma)?
- Fonte: `count(*) FROM products WHERE status = Published` (provavelmente sem mudança de schema)

## Rota
`backlog` — apenas documentação de descoberta; não entra em pipeline até priorização do Gerente.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (registar backlog) | Coordenador | haiku-4.5 | ~ | ~| ~ |
