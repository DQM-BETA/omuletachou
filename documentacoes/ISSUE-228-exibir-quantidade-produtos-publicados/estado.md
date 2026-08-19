---
issue: 228
titulo: feat: relatório de produtos com filtros (categoria/plataforma/status) na tela Reports
etapa_atual: PM Fase 1 — aguardando Gate 1
ultimo_agente: pm-analista-negocios
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
rota: backlog
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

## Levantamento (PM Fase 1 — 2026-08-19)
Perguntas postadas na Issue (comentário: https://github.com/DQM-BETA/omuletachou/issues/228#issuecomment-5346494763), aguardando resposta do Gerente no Gate 1:
1. Confirmação do objetivo de negócio — ferramenta operacional interna (dashboard admin), não feature do site público.
2. Escopo de filtros v1 vs. futuro — subcategoria / faixa de data de coleta / faixa de desconto entram agora ou ficam de fora do PRD atual?
3. Formato de exibição preferido — tabela detalhada, cards agregados, gráfico, ou combinação.
4. Necessidade de exportação/impressão (CSV/Excel) ou só consulta em tela.
5. Atualização em tempo real vs. recálculo só ao aplicar filtro (on-demand).
6. Confirmar rota `backlog` (mantida) ou promover para `normal`/`rapido` (priorizar agora), como decidido para a #227.

## Rota
`backlog` — mantida até o Gerente decidir o contrário no Gate 1. Apenas documentação de descoberta; não entra em pipeline de dev até priorização.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (registar backlog) | Coordenador | haiku-4.5 | ~ | ~| ~ |
| 2 | Atualização (escopo expandido, 2026-08-19) | Coordenador | haiku-4.5 | ~ | ~ | ~ |
| 3 | PM Fase 1 (levantamento) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
