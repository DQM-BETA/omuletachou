---
issue: 231
titulo: feat: faixa de produtos sugeridos (site) + rastreio de cliques + melhorias no grid de Products (dashboard)
etapa_atual: Backlog — aguardando priorização do Gerente (bloqueada por #230)
ultimo_agente: coordenador
openspec_change: ~
tech_stacks: 
  - Backend (ASP.NET Core 8.0)
  - Frontend (Angular 17+, Next.js 14+)
  - Banco (PostgreSQL 16)
repos:
  - omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-231-faixa-de-produtos-sugeridos
openspec_path: repos/omuletachou/openspec/changes/ISSUE-231-faixa-de-produtos-sugeridos
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: Bloqueada por Issue #230 (revisão de filtros do site público)
status_comment_id: ~
---

## Descrição

Pedido do Gerente com dependência explícita da **Issue #230** — deve ser refinada/iniciada somente após conclusão da #230.

### 1. Faixa de produtos sugeridos inteligente (site público, tela de produtos)
- Adicionar uma faixa/linha de "produtos sugeridos" na tela de listagem de produtos do site, baseada na categoria dos produtos atualmente filtrados pelo usuário.
- Se o filtro atual não retornar nenhum produto (lista vazia), a faixa de sugeridos deve mostrar os produtos **mais clicados** em vez de sugestões por categoria.

### 2. Rastreio de cliques (nova funcionalidade, pré-requisito dos itens 1 e 3)
- Registrar quando um produto é clicado (provavelmente ao clicar no card/link de afiliado no site público) para alimentar o ranking de "mais clicados".

### 3. Dashboard — tela Products
- Adicionar coluna mostrando a contagem de cliques de cada produto (usando rastreio do item 2).
- Adicionar ordenação clicável diretamente nos títulos das colunas do grid (clicar no cabeçalho ordena por ela).
- Remover coluna "Desconto" da tabela.

### 4. Banco de dados
- Remover o campo de desconto (`discount_pct`) do banco **somente se** essa informação não existir de fato para o Mercado Livre.
- **Investigar no refinamento**: confirmar se Amazon e/ou Shopee ainda fornecem desconto real (Issue #208 isentou Mercado Livre por falta do dado, mas outras plataformas podem ter dados reais).
- Não remover a coluna se isso quebrar dados reais de outras plataformas — decisão final é do Arquiteto/PM no refinamento.

## Investigação necessária (trabalho de refinamento)

- [ ] Confirmar com Gerente a ordem de prioridade e status de bloqueio pela #230.
- [ ] Definir lógica de "sugestão por categoria" (quantos produtos, critério de ordenação — ex. AI Score, mais recentes).
- [ ] Arquiteto: decidir onde persistir contagem de cliques (novo campo no `Product` ou tabela separada de eventos de clique).
- [ ] Verificar dados reais de Amazon/Shopee antes de remover coluna `discount_pct`.

## Rota: Backlog

Aguardando priorização do Gerente. Bloqueada por Issue #230.

---

_Criado: 2026-08-19 — Coordenador_
