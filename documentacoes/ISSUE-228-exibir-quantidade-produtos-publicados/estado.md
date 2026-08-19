---
issue: 228
titulo: feat: relatório de produtos com filtros (categoria/plataforma/status) na tela Reports
etapa_atual: Em Desenvolvimento
ultimo_agente: lider-tecnico
openspec_change: openspec/changes/issue-228-relatorio-produtos-filtros
tech_stacks: [dotnet, angular]
repos:
  omuletachou: "https://github.com/DQM-BETA/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados
openspec_path: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros
sub_issues: ["#242 (stack:dotnet, task_id:T-01)", "#243 (stack:dotnet, task_id:T-02)", "#244 (stack:dotnet, task_id:T-03)", "#245 (stack:angular, task_id:T-04)"]
desenv_tasks_merged: []
sub_issues_frontend: {"#245": "T-04"}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~
rota: normal
---

## Resumo
Adicionar relatório de produtos publicados no site à tela `Reports`, com filtros combináveis (Categoria, Subcategoria, Plataforma, Status, Faixa de data de coleta) e exibição em cards de resumo agregados + tabela/gráfico detalhado, on-demand.

## Contexto
A tela `Reports` (`localhost:8081/reports`) mostra cards de "Hoje/Semana/Mês" e gráfico de "Publicações por rede (últimos 7 dias)" — ambos baseados na fila de publicação social (`publication_queue`). Desde Issue #208, a visibilidade no site é desacoplada da rede social (um produto pode estar `Published` no site sem passar pela fila social). Faltava indicador de quantos produtos estão realmente publicados e visíveis no site — agora especificado nesta issue.

## Gate 1 — Respostas do Gerente (2026-08-19)
1. Objetivo de negócio confirmado: ferramenta operacional interna do dashboard (admin), não é feature do site público.
2. Filtros v1: Categoria, Plataforma, Status (já certos) + **Subcategoria** e **Faixa de data de coleta** entram na v1. **Faixa de desconto fica fora do escopo** (não implementar).
3. Formato: combinação — cards de resumo agregados + tabela/gráfico detalhado abaixo (opção d).
4. Sem exportação/impressão nesta versão — só consulta em tela.
5. Sem atualização em tempo real — recalcula on-demand ao aplicar/mudar filtro.
6. Rota promovida de `backlog` para `normal` — segue o pipeline completo.

Comentário com as respostas: https://github.com/DQM-BETA/omuletachou/issues/228#issuecomment-5346638492

## PRD (PM Fase 2 — 2026-08-19)
- `proposal.md`: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/criterios-aceite.md (5 grupos de cenários Given/When/Then: exibição padrão, filtros combináveis, atualização on-demand, sem exportação, tratamento de erro)

## Ambiguidade arquitetural avaliada
**Sim.** Contrato do endpoint de relatório (agregados vs. detalhado — um endpoint ou dois), performance de agregação com múltiplos filtros combinados em `products` (possível necessidade de índice novo, especialmente `Subcategory` e data de coleta), se as agregações são calculadas em tempo real a cada request ou exigem cache/materialização, e formato/tipo da coluna de data de coleta para suportar filtro de faixa. Encaminhado ao **Arquiteto** antes do refinamento do LT.

## Rota
`normal` — promovida pelo Gerente no Gate 1 (2026-08-19). Segue o pipeline completo.

## Arquitetura (Arquiteto)
`design.md` completo em `openspec_path` — decisões: dois endpoints (`GET /api/reports/products/summary` novo para cards agregados + extensão aditiva de `GET /api/products` para a tabela/gráfico detalhado), índice composto novo `IX_products_status_platform_createdat`, sem cache/materialização (query direta on-demand), default `Status=Published` implementado no Angular (não no backend), `Product.CreatedAt` já é a data de coleta (sem migration de tipo).

## Refinamento Técnico (LT — 2026-08-19)
- `especificacao-tecnica.md`: repos/omuletachou/documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/especificacao-tecnica.md — contratos de API (endpoint novo + extensão), schema, padrões obrigatórios.
- `tasks.md`: repos/omuletachou/openspec/changes/issue-228-relatorio-produtos-filtros/tasks.md — 4 sub-tarefas com critérios Given/When/Then + contexto técnico.
- Task breakdown: 4 sub-issues criadas — 3 stack:dotnet (índice/migration, endpoint summary, extensão GetProducts) + 1 stack:angular (filtros/cards/tabela na tela Reports).
- Design.md do Arquiteto (estava pendente de commit) commitado junto com este refinamento.
- Demanda tem UI (design.md §4 lista componentes de filtros/cards/tabela-gráfico no Angular) — UX/UI atua antes do dev da sub-issue #245.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (registar backlog) | Coordenador | haiku-4.5 | ~ | ~| ~ |
| 2 | Atualização (escopo expandido, 2026-08-19) | Coordenador | haiku-4.5 | ~ | ~ | ~ |
| 3 | PM Fase 1 (levantamento) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 4 | PM Fase 2 (PRD + critérios de aceite) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 5 | Arquiteto (design.md) | Arquiteto | sonnet-5 | ~ | ~ | ~ |
| 6 | Refinamento Técnico (task breakdown) | Líder Técnico | sonnet-5 | ~ | ~ | ~ |
