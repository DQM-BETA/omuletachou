---
issue: 182
titulo: fix: MercadoLivreCollector quebrado — endpoint /sites/MLB/search descontinuado pela API, reconstruir com Highlights API
etapa_atual: Bloqueado — aguardando decisão do Gerente sobre acesso da aplicação Mercado Livre (Gate 1.5)
rota: normal
ultimo_agente: lider-tecnico
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
blockers: |
  BLOQUEIO CRÍTICO confirmado ao vivo pelo LT em 2026-08-17 (ver design.md secao 10):
  - GET /items/{item_id} (individual e batch) retorna HTTP 403 access_denied com o
    access_token/app atuais (mercadolivre.client_id), mesmo usando item_id real resolvido
    corretamente via /products/{id}/items. Bloqueia obtencao de permalink/titulo/imagem por
    item.
  - POST /affiliate-tools/links (endpoint ja implementado em ProcessorJob.EnsureAffiliateLinkAsync,
    usado para validar CA 7, requisito critico bloqueante da Definicao de Pronto) retorna HTTP 404
    "resource not found" com qualquer payload/URL/metodo testado — rota nao reconhecida para esta
    aplicacao.
  - Hipotese de causa raiz: app registrada com sandbox_mode=true, certification_status=
    not_certified, allow_flow=[client_credentials] (sem authorization_code), sem escopo de
    afiliados — GET /applications/{client_id} confirma.
  - Decisoes 1 (mapeamento de categorias) e 3 (rate limit) fecharam normalmente com valores reais.
    Decisao 2 (batching) confirmou o limite tecnico (>=18 IDs por chamada aceitos), mas o CONTEUDO
    de cada item fica bloqueado independente do lote.
  - Reportado ao Gerente via comentario na Issue #182 (Gate 1.5) com 4 opcoes de encaminhamento.
    Nao ha mandato do LT para escolher entre elas (decisao de acesso a conta externa/produto).
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
| 5 | Líder Técnico (refinamento) | lider-tecnico | sonnet | ~ | ~ | ~ | Subiu stack Docker local (`omuletachou-local`, db+api), confirmou Decisões 1 e 3 do `design.md` com valores reais (categorias mapeadas, sem N:1 necessário; nenhum header de rate limit exposto). Decisão 2 confirmou o limite técnico do multi-get (≥18 IDs/chamada) mas revelou que os IDs do Highlights são `catalog_product_id` (não `item_id`) e que `GET /items/{item_id}` (mesmo com ID real resolvido) retorna HTTP 403. Decisão 4: `POST /affiliate-tools/links` retorna HTTP 404 para qualquer payload — endpoint inalcançável. `design.md` atualizado (seção 10) com evidência completa; achado postado na Issue (comentário Gate 1.5); **task breakdown/sub-issues NÃO criadas** — bloqueio de acesso externo requer decisão do Gerente antes de especificar/implementar (evita retrabalho). |

## Próximo passo

**GATE 1.5 — Gerente**: decidir entre as 4 opções levantadas no comentário da Issue #182 (certificar
a aplicação Mercado Livre / trocar fluxo OAuth; buscar a rota real do programa de afiliados;
descope parcial sem link de afiliado; pausar a issue) antes do LT prosseguir com
`especificacao-tecnica.md`, `tasks.md` e sub-issues.
