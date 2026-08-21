---
issue: 231
titulo: feat: rastreio de cliques + faixa de produtos sugeridos (site público)
etapa_atual: Refinamento Técnico — aguardando Arquiteto (ambiguidade arquitetural identificada)
ultimo_agente: pm-analista-negocios
openspec_change: openspec/changes/issue-231-faixa-de-produtos-sugeridos
tech_stacks:
  - Backend (ASP.NET Core 8.0)
  - Frontend (Next.js 14+ — website público)
  - Banco (PostgreSQL 16)
repos:
  - omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-231-faixa-de-produtos-sugeridos
openspec_path: repos/omuletachou/openspec/changes/issue-231-faixa-de-produtos-sugeridos
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: ~
status_comment_id: ~
---

## Descrição

Pedido do Gerente. Escopo restrito aos itens 1-2 do pedido original (rastreio de cliques + faixa de produtos sugeridos) — o item 3 (grid do dashboard Products) virou a **Issue #275** (issue separada, independente).

### 1. Faixa de produtos sugeridos inteligente (site público, tela de produtos)
- Carrossel horizontal com setas de navegação, baseado na categoria dos produtos atualmente filtrados.
- Fallback "mais clicados em geral" quando o filtro atual não retorna produtos.

### 2. Rastreio de cliques (pré-requisito do item 1)
- Evento anônimo (produto + timestamp), disparado ao clicar em qualquer card de produto (listagem normal ou carrossel de sugeridos), sem alterar o destino atual do clique.

## Gate 1 — respostas do Gerente (2026-08-21, postadas na Issue)

1. Escopo confirmado: separar. Item 3 → Issue #275. Esta issue fica só com itens 1-2.
2. Critério de ordenação dentro da categoria: **mais clicados** (não AI Score, não mais recentes). Quantidade/fallback/mínimo: decisão de produto do PM (ver proposal.md).
3. Destino do clique **não muda**. Evento **anônimo** confirmado. Cliques no carrossel de sugeridos contam igual aos da listagem normal.
4. Investigação de `discount_pct` fica para Arquiteto/LT decidir, não é obrigatória nesta issue.
5. Rota: **`normal`**.

## Refinamento de Negócio (Fase 2 — concluído 2026-08-21)

- `proposal.md` e `criterios-aceite.md` escritos em `openspec/changes/issue-231-faixa-de-produtos-sugeridos/proposal.md` e `documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/criterios-aceite.md`.
- **Decisões de produto do PM** (não especificadas pelo Gerente, documentadas na proposal.md, sujeitas a ajuste no Code Review/QA):
  - Quantidade por carregamento do carrossel: 10 produtos.
  - Fallback "mais clicados": geral, sem corte por plataforma (apenas produtos ativos/disponíveis).
  - Mínimo de produtos para a faixa aparecer: 4.
  - Critério de desempate em 0 cliques/empate: delegado ao refinamento técnico (ex.: mais recentes primeiro).
- Sumário do PRD postado como comentário na Issue #231.

## Ambiguidade arquitetural — avaliação do PM

**Sim, há ambiguidade.** Pontos que exigem decisão do Arquiteto antes do refinamento do LT:
1. Onde persistir a contagem de cliques (campo agregado no `Product` vs. tabela de eventos `product_clicks`).
2. Estratégia de agregação de "mais clicados por categoria" com performance aceitável (query on-the-fly vs. contador desnormalizado/job).
3. Contrato do endpoint da faixa de sugeridos (payload, síncrono vs. fila/Hangfire para registro de clique).
4. Investigação de `discount_pct` (Amazon/Shopee vs. Mercado Livre) — não obrigatória, mas pode ser registrada se relevante à modelagem do `Product`.

## Próximos passos

- [ ] Arquiteto: completar `design.md` (decisões dos 4 pontos acima).
- [ ] Líder Técnico: refinamento técnico + task breakdown + sub-issues.

---

_Criado: 2026-08-19 — Coordenador_
_Atualizado: 2026-08-19 — Coordenador (complemento UI: detalhe de navegação do carrossel)_
_Atualizado: 2026-08-21 — PM (levantamento Fase 1 postado na Issue, blocker #230 removido)_
_Atualizado: 2026-08-21 — PM (Fase 2: PRD completo, escopo restrito aos itens 1-2 após split para Issue #275, ambiguidade arquitetural identificada, proximo: Arquiteto)_
