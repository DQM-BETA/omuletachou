---
issue: 182
titulo: fix: MercadoLivreCollector quebrado — endpoint /sites/MLB/search descontinuado pela API, reconstruir com Highlights API
etapa_atual: Em Desenvolvimento
rota: normal
ultimo_agente: lider-tecnico
openspec_change: repos/omuletachou/openspec/changes/issue-182-mercadolivrecollector-quebrado
tech_stacks:
  - dotnet
  - angular
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-182-mercadolivrecollector-quebrado
openspec_path: repos/omuletachou/openspec/changes/issue-182-mercadolivrecollector-quebrado
sub_issues:
  - "#183 (stack:dotnet, task_id:Sub-A) — MercadoLivreCollector: reconstrução com Highlights API"
  - "#184 (stack:dotnet, task_id:Sub-B) — Fluxo semi-manual de link de afiliado (domínio + API de importação)"
  - "#185 (stack:angular, task_id:Sub-C) — Dashboard: tela de importação de links de afiliado"
desenv_tasks_merged:
  - "#184"
sub_issues_frontend:
  "#185": angular
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

  RESOLUCAO GATE 1.5 (Gerente, 2026-08-17) — investigacao ao vivo (sessao principal + Gerente):
  - Endpoint POST /affiliate-tools/links (api.mercadolibre.com) NAO tem equivalente publico/OAuth.
    O link de afiliado real e gerado por POST https://www.mercadolivre.com.br/affiliate-program/api/v2/affiliates/createLink
    — endpoint INTERNO do site (nao do Devcenter), autenticado por sessao de navegador logada
    (cookies + x-csrf-token rotativo) + protegido por fingerprint anti-bot (_snoopy, nsa_rotok).
    Confirmado via captura de rede real (DevTools) pelo Gerente. NAO ha caminho de automacao
    server-to-server legitimo — replay de sessao seria fragil e arriscaria a conta.
  - DECISAO: fluxo semi-manual para geracao de link de afiliado. O coletor (100% automatico) monta
    a lista diaria de URLs de produto; o Gerente cola essa lista na ferramenta oficial "Gerador de
    produtos recomendados" (mercadolivre.com.br/afiliados/linkbuilder), copia os links gerados de
    volta, e cola numa tela de importacao no dashboard. Pareamento por ORDEM/LINHA (confirmado:
    a ferramenta preserva a ordem input->output 1:1). LT deve desenhar essa tela de importacao
    como parte da especificacao tecnica.
  - Blocker do permalink (GET /items 403) tem contorno: GET /products/{catalog_product_id} devolve
    name e picture normalmente (so o campo permalink vem vazio); o padrao
    https://www.mercadolivre.com.br/p/{catalog_product_id} e a URL curta oficial de produto de
    catalogo. LT deve CONFIRMAR AO VIVO (navegador real — curl direto leva a bloqueio anti-bot do
    site publico) antes de fechar a especificacao.

  RESOLUCAO REFINAMENTO LT (2026-08-17) — especificacao-tecnica.md + tasks.md escritos, sub-issues criadas:
  - Confirmacao ao vivo do permalink (item acima): tentada via curl com headers de navegador real
    E via Chromium headless real (Playwright, ja instalado no projeto em website/) — AMBOS
    bloqueados pelo gate anti-bot (redirect para /gz/account-verification), inclusive o Chromium
    real nao diferenciou um catalog_product_id real de um inventado (mesmo resultado para os dois).
    Inconclusivo por ferramental (mesmo bloqueio anti-bot ja documentado na Secao 10 do design.md
    para affiliate-tools/links, agora tambem no proprio permalink). Decisao: usar
    https://www.mercadolivre.com.br/p/{catalog_product_id} mesmo assim (padrao publicamente
    documentado do ML) com mitigacao de risco: essa mesma URL e a que o operador cola na
    ferramenta oficial do ML no fluxo semi-manual — falha do padrao seria visivel/segura ali
    (operador ve erro na ferramenta), nao silenciosa. Detalhe completo em
    especificacao-tecnica.md secao 1.
  - especificacao-tecnica.md e tasks.md escritos em docs_path/openspec_path (repo ja existia,
    sem staging). 3 sub-issues criadas: #183 (Sub-A, MercadoLivreCollector, stack:dotnet), #184
    (Sub-B, fluxo semi-manual dominio+API, stack:dotnet), #185 (Sub-C, dashboard tela de
    importacao, stack:angular). Sub-C tem componente de UI nova (tela de importacao) — proximo
    passo e UX/UI antes dos devs, por definir layout/composicao visual (contrato funcional ja
    especificado em especificacao-tecnica.md secao 3.6).
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
| 6 | Líder Técnico (refinamento, retomada pós Gate 1.5) | lider-tecnico | sonnet | 167247 | 60 | 857s | Gate 1.5 resolvido pelo Gerente (fluxo semi-manual de link de afiliado + contorno do permalink via `/products/{id}`). Confirmação ao vivo do padrão `/p/{catalog_product_id}` tentada via curl e via Chromium headless real (Playwright) — inconclusiva por bloqueio anti-bot (mesmo padrão da Seção 10), decisão documentada com mitigação (checkpoint humano no fluxo semi-manual). Escritos `especificacao-tecnica.md` (fluxo revisado do collector — `/products/{id}`+`/products/{id}/items` no lugar do multi-get bloqueado — e desenho completo do fluxo semi-manual de link de afiliado) e `tasks.md`. 3 sub-issues criadas: #183 (MercadoLivreCollector, stack:dotnet), #184 (fluxo semi-manual domínio+API, stack:dotnet), #185 (dashboard tela de importação, stack:angular). `tech_stacks` ganhou `angular` (nova tela de dashboard). Resumo técnico postado na Issue #182. |
| 7 | Coordenador (bookkeeping) | coordenador | haiku | 23018 | 3 | 26s | Comentário 📍 Status da Issue #182 atualizado para etapa Dev/UX-UI, refletindo as 3 sub-issues em paralelo. |
| 8 | UX/UI (Sub-C #185) | ux-ui | sonnet | 89710 | 14 | 243s | `ux-ui-spec.md` escrito — layout de página única (Angular Material, precedente `facebook-manual`), pareamento visual por ordem com validação client-side pré-POST, todos os estados (loading/vazio/erro/sucesso total-parcial/disabled/readonly) e responsividade em 3 breakpoints especificados. Resumo postado como comentário na Issue #185. Aguardando contrato de API da Sub-B (#184, ainda em execução) antes do Dev Angular iniciar. |
| 9 | Dev .NET (Sub-B #184) | dev-dotnet | sonnet | 146042 | 64 | 486s | `ProductStatus.AwaitingAffiliateLink` + `Product.MarkAsAwaitingAffiliateLink/ResolveAffiliateLink` + `ProcessorJob.EnsureAffiliateLinkAsync` reescrito (sem HTTP, endpoint morto removido) + `POST api/products/affiliate-links/import` + `SourceUrl` em `ProductListItemDto`. 426/426 testes passando, boot real contra Postgres confirmado (sem migration necessária). PR #186 feature→desenv aberto. |
| 10 | Líder Técnico (merge Sub-B #184) | lider-tecnico | sonnet | ~ | ~ | ~ | PR #186 (feature/ISSUE-184-fluxo-semi-manual-link-afiliado → desenv) verificado (426/426 testes, sem CI configurado no repo) e mesclado via squash + delete-branch. Sub-issue #184 fechada. `desenv_tasks_merged` = ["#184"]. Sub-A (#183) e Sub-C (#185) ainda pendentes — PR desenv→homolog NÃO criado. |

## Próximo passo

Sub-B (#184) mesclada em `desenv`. Faltam: **Dev .NET** para Sub-A (#183, MercadoLivreCollector) e
**Dev Angular** para Sub-C (#185, dashboard — já tem `ux-ui-spec.md` e agora também o contrato de
API da Sub-B disponível em `desenv`). PR `desenv→homolog` só deve ser criado quando as 3 sub-issues
estiverem em `desenv_tasks_merged`.
