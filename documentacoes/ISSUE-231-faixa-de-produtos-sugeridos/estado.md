---
issue: 231
titulo: feat: faixa de produtos sugeridos (site) + rastreio de cliques + melhorias no grid de Products (dashboard)
etapa_atual: Refinamento de Negócio — levantamento Fase 1 postado, aguardando respostas do Gerente
ultimo_agente: pm-analista-negocios
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
blockers: ~ (Issue #230 concluída — desbloqueada)
status_comment_id: ~
---

## Descrição

Pedido do Gerente. Estava bloqueada pela Issue #230 (filtros do site público) — **#230 concluída, #231 desbloqueada em 2026-08-21**.

### 1. Faixa de produtos sugeridos inteligente (site público, tela de produtos)
- Adicionar uma faixa/linha de "produtos sugeridos" na tela de listagem de produtos do site, baseada na categoria dos produtos atualmente filtrados pelo usuário.
- Se o filtro atual não retornar nenhum produto (lista vazia), a faixa de sugeridos deve mostrar os produtos **mais clicados** em vez de sugestões por categoria.
- **Detalhe UI (complemento 2026-08-19):** a faixa de produtos sugeridos deve ser um **carrossel horizontal com navegação por seta para a direita e seta para a esquerda**, permitindo ao usuário navegar entre os produtos sugeridos.

### 2. Rastreio de cliques (nova funcionalidade, pré-requisito dos itens 1 e 3)
- Registrar quando um produto é clicado (provavelmente ao clicar no card/link de afiliado no site público) para alimentar o ranking de "mais clicados".

### 3. Dashboard — tela Products
- Adicionar coluna mostrando a contagem de cliques de cada produto (usando rastreio do item 2).
- Adicionar ordenação clicável diretamente nos títulos das colunas do grid (clicar no cabeçalho ordena por ela).
- Remover coluna "Desconto" da tabela.

### 4. Banco de dados
- Remover o campo de desconto (`discount_pct`) do banco **somente se** essa informação não existir de fato para o Mercado Livre.
- **Investigar no refinamento**: confirmar se Amazon e/ou Shopee ainda fornecem desconto real (Issue #208 isentou Mercado Livre por falta do dado, mas outras plataformas podem ter dados reais). Investigação técnica (query real ao banco) delegada ao Arquiteto/LT — fora do escopo de acesso do PM.
- Não remover a coluna se isso quebrar dados reais de outras plataformas — decisão final é do Arquiteto/PM no refinamento.

## Levantamento Fase 1 (postado na Issue 2026-08-21)

Perguntas postadas cobrindo:
- [ ] Escopo: 1 issue-pai só, ou split (Issue A: rastreio de cliques + faixa de sugeridos; Issue B: melhorias de grid do dashboard)?
- [ ] Faixa de sugeridos: quantidade de produtos no carrossel, critério de ordenação dentro da categoria, regra do fallback "mais clicados", quando a faixa aparece.
- [ ] Rastreio de cliques: destino do clique (link de afiliado direto vs. página de detalhe primeiro), confirmação de que é evento anônimo (sem dado pessoal/sessão de usuário), se cliques dentro do carrossel de sugeridos contam junto com cliques da listagem normal.
- [ ] discount_pct: investigação técnica (query real Amazon/Shopee/Mercado Livre) delegada ao Arquiteto/LT no refinamento técnico.
- [ ] Rota: mantém `backlog` ou muda para `normal` (pipeline completo), agora que está desbloqueada?

## Investigação necessária (trabalho de refinamento)

- [ ] Confirmar respostas do Gerente às perguntas acima.
- [ ] Definir lógica de "sugestão por categoria" (quantos produtos, critério de ordenação — ex. AI Score, mais recentes).
- [ ] Arquiteto: decidir onde persistir contagem de cliques (novo campo no `Product` ou tabela separada de eventos de clique).
- [ ] Arquiteto/LT: verificar dados reais de Amazon/Shopee antes de remover coluna `discount_pct`.

## Rota

Nasceu como `backlog` (bloqueada por #230). Pergunta ao Gerente se muda para `normal` agora que desbloqueada (ver Levantamento Fase 1 acima).

---

_Criado: 2026-08-19 — Coordenador_
_Atualizado: 2026-08-19 — Coordenador (complemento UI: detalhe de navegação do carrossel)_
_Atualizado: 2026-08-21 — PM (levantamento Fase 1 postado na Issue, blocker #230 removido)_
