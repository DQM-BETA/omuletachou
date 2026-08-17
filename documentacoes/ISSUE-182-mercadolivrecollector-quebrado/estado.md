---
issue: 182
titulo: fix: MercadoLivreCollector quebrado — endpoint /sites/MLB/search descontinuado pela API, reconstruir com Highlights API
etapa_atual: Refinamento Técnico — Líder Técnico (confirmar ao vivo os valores marcados [LT CONFIRMAR AO VIVO] no design.md antes de escrever a especificacao-tecnica.md)
rota: normal
ultimo_agente: arquiteto-engenheiro
openspec_change: repos/omuletachou/openspec/changes/issue-182-mercadolivrecollector-quebrado
tech_stacks:
  - dotnet
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-182-mercadolivrecollector-quebrado
openspec_path: repos/omuletachou/openspec/changes/issue-182-mercadolivrecollector-quebrado
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
createdAt: 2026-08-17
status_comment_id: 5317813321
---

## Histórico de etapas

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Notas |
|---|---|---|---|---|---|---|---|
| 1 | Preparação | coordenador | haiku | 26277 | 14 | 108s | Issue criada, estado.md inicializado |
| 2 | PM Fase 1 | pm-analista-negocios | sonnet | 27587 | 9 | 54s | Levantamento postado (perguntas de categorias/volume/frequência/fallback/dedupe), aguardando respostas do Gerente |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 58209 | 26 | 307s | PRD (proposal.md) + criterios-aceite.md escritos incorporando decisões do Gate 1; ambiguidade arquitetural identificada (mapeamento de category IDs, limite/batching do multi-get, rate limit dentro do ciclo, cache da árvore de categorias) → encaminhado ao Arquiteto |
| 4 | Arquiteto | arquiteto-engenheiro | sonnet | 92036 | 13 | 332s | `design.md` com 4 decisões técnicas: (1) mapeamento categoria→ID de dicionário estático hardcoded; (2) batching do multi-get alinhado à fronteira de categoria (1 lote = 1 categoria, máx. 10 IDs); (3) sem rate limiter dedicado (volume ~16-20 chamadas/dia irrelevante) + delay defensivo de 300ms; (4) checklist objetivo de 5 itens para validação do link de afiliado. Valores concretos (IDs `MLB####` reais, limite real do multi-get) marcados `[LT CONFIRMAR AO VIVO]` — Arquiteto não tem Bash livre (só `gh`), não pôde testar contra a API real; LT deve confirmar antes de escrever a especificação técnica. |

## Próximo passo

Líder Técnico: rodar as chamadas reais (`GET /sites/MLB/categories`, `GET /items?ids=...` com lote real de 10 IDs, inspecionar headers de rate limit) usando as credenciais já configuradas no `.env`/`app_settings` locais, confirmar/ajustar os valores marcados `[LT CONFIRMAR AO VIVO]` em `openspec/changes/issue-182-mercadolivrecollector-quebrado/design.md`, e então escrever a especificação técnica + task breakdown + sub-issue(s).
