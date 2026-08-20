issue: 260
titulo: feat: busca textual inteligente (fonética/fuzzy) na tela de produtos do site público
rota: normal
etapa_atual: Refinamento Técnico — Arquiteto (design.md)
ultimo_agente: pm-analista-negocios
openspec_change: repos/omuletachou/openspec/changes/issue-260-busca-textual-inteligente
tech_stacks: []
repos:
  omuletachou: true
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-260-busca-textual-inteligente
openspec_path: repos/omuletachou/openspec/changes/issue-260-busca-textual-inteligente
status_comment_id: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tempo (s) |
|---|---|---|---|---|---|
| 1 | Preparar demanda | Coordenador | Haiku 4.5 | 1234 | 5 |

---

### Notas
- Item 4 da Issue #230, separado por decisão do Gerente no Gate 1 (2026-08-20)
- Restrição vinculante: técnica de BD (ex.: pg_trgm no Postgres), **NÃO** chamada à IA por requisição
- Referência: Issue #230 (itens 1-3, mesmo componente filter-bar)
- PM Fase 1 (2026-08-20): perguntas de levantamento postadas na Issue — eixos: localização na UI, escopo da busca (quais campos), comportamento de disparo (tempo real vs botão), exemplos concretos de sucesso da busca fonética/fuzzy, e confirmação de que a restrição "sem IA" é definitiva. Aguardando respostas do Gerente para Fase 2 (PRD).
- PM Fase 2 (2026-08-20): Gate 1 respondido pelo Gerente (postado como comentário na Issue para rastreabilidade) — campo novo na filter-bar; escopo = título+categoria+descrição com título priorizado; tempo real com resposta percebida como instantânea (alvo técnico <300-500ms a definir pelo Arquiteto/LT); meta qualitativa de cobertura máxima de erros de digitação/variação (sem exemplos concretos fornecidos); restrição "sem IA" confirmada como definitiva, não reabrir. PRD completo escrito em `proposal.md` + `criterios-aceite.md`. Ambiguidade arquitetural = **SIM** (técnica exata de fuzzy/similaridade dentro da restrição "sem IA": pg_trgm vs full-text vs combinação, estratégia de índice, threshold de similaridade, peso do título no ranking) — encaminhado ao **Arquiteto**.
