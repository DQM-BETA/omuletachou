---
issue: 228
titulo: feat: relatório de produtos com filtros (categoria/plataforma/status) na tela Reports
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
Adicionar relatório de quantidade de produtos publicados no site à tela `Reports`, com filtros combináveis (categoria, plataforma, status, outros).

## Contexto
A tela `Reports` (`localhost:8081/reports`) mostra cards de "Hoje/Semana/Mês" e gráfico de "Publicações por rede (últimos 7 dias)" — ambos baseados na fila de publicação social (`publication_queue`). Desde Issue #208, a visibilidade no site é desacoplada da rede social (um produto pode estar `Published` no site sem passar pela fila social). Falta indicador de quantos produtos estão realmente publicados e visíveis no site.

## Pedido (atualizado 2026-08-19)
O Gerente detalhou: não é só um card numérico simples de "produtos publicados no site" — é um **relatório com filtros combináveis**, onde o usuário possa refinar a view por:
- **Categoria**
- **Plataforma** (Mercado Livre, Amazon, Shopee)
- **Status**
- Possivelmente outros (subcategoria, faixa de data de coleta, faixa de desconto)

## Investigação necessária (refinamento)
- **Escopo visual:** layout do relatório (tabela, cards, gráficos?), arranjo dos filtros (dropdown, chips, range slider?), paginação se aplicável
- **UX de filtros:** combináveis (AND/OR?), modo "salvar favoritos"?
- **Fonte de dados:** agregação por dimensões (categoria, plataforma, status) + contagem; considerar cache/performance
- **Schema:** verificar se todas as dimensões já existem em `products` pós-#208 (provavelmente não precisa mudança, a confirmar)
- **Acionamento:** on-demand ou com time-based refresh?

## Rota
`backlog` — apenas documentação de descoberta; não entra em pipeline até priorização do Gerente.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (registar backlog) | Coordenador | haiku-4.5 | ~ | ~| ~ |
| 2 | Atualização (escopo expandido, 2026-08-19) | Coordenador | haiku-4.5 | ~ | ~ | ~ |
