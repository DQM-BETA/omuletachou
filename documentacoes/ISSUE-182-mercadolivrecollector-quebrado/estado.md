---
issue: 182
titulo: fix: MercadoLivreCollector quebrado — endpoint /sites/MLB/search descontinuado pela API, reconstruir com Highlights API
etapa_atual: Em Desenvolvimento (aguardando PR homolog→main — Gate 2)
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
  - "#190 (stack:dotnet, task_id:Sub-D) — Fix isolamento de falha em GetJsonAsync (JsonDocument.Parse fora do try/catch), achado do /code-review estático no PR #189"
  - "#192 (stack:dotnet, task_id:Sub-E) — Isentar Mercado Livre do critério de desconto mínimo no scoring de IA (ClaudeAiService.ScoreProductAsync), Achado 2 do /code-review estático no PR #189, decisão de negócio do Gerente Opção A"
  - "#195 (stack:angular, task_id:Sub-F) — Fix: painel de skipped inacessível quando lista de pendentes esvazia (mercadolivre-links.component.html), Achado 1 (bloqueante) do QA 1ª rodada"
desenv_tasks_merged:
  - "#184"
  - "#183"
  - "#185"
  - "#190"
  - "#192"
  - "#195"
sub_issues_frontend:
  "#185": angular
  "#195": angular
pr_homologacao: 197
pr_homologacao_anterior: 194
pr_release: ~
code_review_homolog_pr: 197
qa_status: aprovado (2a rodada) — ver etapa 28 do histórico
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

  RISCO HERDADO DO DEV .NET — Sub-A #183 (registrado pelo LT no merge, 2026-08-17):
  - O parsing do schema de resposta de GET /highlights foi escrito defensivamente (aceita tanto
    `content[]` quanto `results[]`) porque o Dev nao conseguiu reconfirmar ao vivo contra a API
    real do MercadoLivre — o sandbox de execucao do Dev bloqueia `api.mercadolibre.com`. Nao e
    bloqueador do merge (415/415 testes verdes, boot real via Docker confirmado com `/health`
    200), mas PRECISA ser validado ao vivo (schema real de producao) pelo Code Review e/ou QA
    antes do release para `main` — se o schema real divergir do assumido, o parsing defensivo
    pode mascarar um retorno vazio silenciosamente em vez de falhar visivelmente.

  RISCO SECUNDARIO — permalink do produto (nao reconfirmado por automacao, ver secao acima):
  - O padrao https://www.mercadolivre.com.br/p/{catalog_product_id} usado como permalink de
    produto de catalogo nunca foi confirmado ao vivo por ferramenta automatizada (curl e
    Chromium headless bloqueados por anti-bot). Mitigado pelo checkpoint humano do fluxo
    semi-manual (operador cola a URL na ferramenta oficial do ML e veria erro se o padrao
    estivesse errado), mas vale reconferir manualmente no navegador durante o Code Review/QA.

  RESOLUCAO CODE REVIEW (2026-08-17) — PR #189 APROVADO e mesclado (desenv->homolog, merge commit
  393134d5f869ec1b16769cfd32a26cb155025bea):
  - Build real (dotnet build Release) + suite completa (dotnet test, 427/427) + boot real via
    Docker (rebuild sem cache, db+api+dashboard healthy, /health 200) + suite dashboard
    (120/120, cobertura 92.32% statements medida ao vivo) + build producao dashboard — tudo
    executado e confirmado, nao so lido.
  - Integracao real testada end-to-end (nao mock-only): produto de teste inserido direto no
    Postgres do container em AwaitingAffiliateLink -> GET /api/products?status=... confirmou
    sourceUrl no payload -> POST /api/products/affiliate-links/import (1 item valido + 1
    productId inexistente) confirmou isolamento de falha por item ao vivo -> banco reconferido
    (status=Queued, affiliate_link preenchido) -> nova listagem confirmou produto sumiu ->
    dashboard (rota /mercadolivre-links + proxy /api/ do nginx) confirmado servindo via
    container real.
  - Os DOIS riscos herdados (schema /highlights e permalink /p/{id}) foram RETENTADOS pelo CR
    com acesso de rede real: `curl https://api.mercadolibre.com/sites/MLB/categories` (direto e
    de dentro do container afiliado_api) e `curl https://www.mercadolivre.com.br/p/MLB16855791`
    -> AMBOS HTTP 403 `PA_UNAUTHORIZED_RESULT_FROM_POLICIES` (bloqueio de politica de rede do
    proprio ambiente de execucao do agente, mesma classe de restricao que ja impediu o Dev/LT —
    nao e resposta do Mercado Livre). Continua NAO CONFIRMADO ao vivo por nenhum agente ate
    agora. Risco aceito para o merge (mesmos motivos do Gate 1.5: parsing defensivo com
    degradacao visivel — highlights vazio nao lanca excecao, so zera a categoria daquele ciclo,
    caminho ja testado via isolamento de falha; permalink com checkpoint humano no fluxo
    semi-manual). RECOMENDACAO EXPLICITA AO QA: tentar validar de novo em ambiente com internet
    real (ex.: deploy homolog na VM Oracle, se tiver rota de rede diferente do sandbox de
    ferramentas dos agentes); se tambem bloqueado, o gate final passa a ser monitorar o primeiro
    ciclo real do CollectorJob em producao (contagem de produtos coletados / logs Hangfire).
  - Achado nao-bloqueante registrado no PR (nao corrigido, nao impede merge): dashboard usa
    `pageSize=200` em `listAwaitingAffiliateLink()` assumindo que cobre o pior caso (ver
    especificacao-tecnica.md §3.6), mas `PaginationExtensions.MaxPageSize=100` (pre-existente,
    Issue #11, fora do escopo deste PR) trunca silenciosamente acima de 100 — sem perda de dado
    (proxima carga mostra o resto), mas sem aviso na tela de que ha mais pendencias alem das 100
    carregadas se o operador acumular >100 produtos aguardando (~80/dia no pior caso, ~1-2 dias
    sem processar). Sugestao de follow-up em `.claude/melhorias/`, nao é regressao deste PR.

  CORRECAO PRE-QA — achados do `/code-review` estatico no PR #189 (LT, 2026-08-17), Gerente pediu
  correcao dos 2 achados (https://github.com/DQM-BETA/omuletachou/pull/189#issuecomment-5319794063)
  antes de acionar o QA:
  - ACHADO 1 (fix tecnico, sem ambiguidade) — `JsonDocument.Parse(body)` em `GetJsonAsync`
    (MercadoLivreCollector.cs linha ~409) roda fora do try/catch que envolve o resto do metodo.
    JsonException de corpo malformado nao vira MercadoLivreApiException (unico tipo capturado
    pelos chamadores), propaga para fora de CollectAsync ANTES do SaveChangesAsync (chamado uma
    unica vez ao final do loop duplo de categorias) — perde TODOS os produtos ja resolvidos no
    ciclo, nao so a categoria/produto com problema. Quebra o isolamento de falha que e premissa
    central do PR. Sub-issue #190 criada (stack:dotnet), TDD obrigatorio, branch
    feature/ISSUE-190-json-parse-try-catch base desenv (fluxo padrao feature->desenv->homolog,
    apesar do codigo ja estar em homolog via PR #189 — LT abrira novo PR desenv->homolog so com
    esta correcao apos o merge do #190).
    **RESOLVIDO 2026-08-18**: Dev implementou (TDD, 3 testes novos, 430/430 suite completa). PR
    #191 mesclado via squash em `desenv` (commit `83e28241248000dfd859c03d170bf0dc017b732e`).
    Sub-issue #190 fechada.
  - ACHADO 2 (ESCALADO ao Gerente em 2026-08-17, DECIDIDO em 2026-08-17, mapeado como sub-issue
    #192 em 2026-08-18) — `MercadoLivreCollector.UpsertProductAsync` seta `OriginalPrice =
    SalePrice` / `DiscountPct = 0` para todo produto ML (Highlights API nao expoe preco
    original/desconto em `/products/{id}` nem `/products/{id}/items` — fallback documentado no
    codigo, nao bug de implementacao). Esse `DiscountPct = 0` alimenta o prompt fixo de
    `ClaudeAiService.ScoreProductAsync` ("Desconto real minimo de 15%; precos inflados
    penalizam", linha ~39) — 0% e indistinguivel para a IA de "produto sem desconto real",
    entao o esperado e reprovacao sistematica (ou score bem abaixo de minScore=6) de TODO
    produto Mercado Livre, zerando na pratica a aprovacao desse canal, mesmo com o collector
    funcionando mecanicamente. Nunca foi decidido deliberadamente em nenhuma fase anterior (PRD,
    design.md, Gate 1.5) — efeito colateral descoberto pela revisao estatica.
    DECISAO DO GERENTE (comentario
    https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5319915601): Opcao A —
    isentar o Mercado Livre do criterio de desconto minimo no scoring de IA (enviar
    DiscountPct=null/omitir a linha do prompt so quando a plataforma nao tem esse dado,
    mantendo os outros 4 criterios de scoring sem penalizar por ausencia de sinal).
    Formalizado pelo PM (Fase 2) como adendo Secao 9 (cenarios 9.1-9.5) em criterios-aceite.md,
    commit `92854c3`, ja em `desenv`.
    **MAPEADO 2026-08-18 (LT)**: sub-issue #192 criada (stack:dotnet, task_id:Sub-E), TDD
    obrigatorio, branch `feature/ISSUE-192-scoring-ml-desconto` base `desenv`. tasks.md
    atualizado com secao Sub-E completa (contexto tecnico: ClaudeAiService.ScoreProductAsync
    monta systemPrompt/userMessage condicionalmente a product.Platform — omite criterio/dado de
    desconto so para MercadoLivre, Amazon/Shopee inalterados). Pronta para Dev .NET.
    **RESOLVIDO 2026-08-18**: Dev implementou (TDD, 5 testes novos cobrindo cenarios 9.1-9.4,
    incluindo `[Theory]` de nao-regressao Amazon/Shopee). Suite 433/436 (3 falhas pre-existentes
    de `ClaudeBudgetServiceIntegrationTests` por falta de Docker/Testcontainers, reproduzidas
    tambem na baseline `desenv` sem a mudanca, nao relacionadas a este PR). PR #193 mesclado via
    squash em `desenv` (commit `d37a16435c266c8e2cf1f03543582b2715c799c1`). Sub-issue #192
    fechada.

  PM FASE 2 (2026-08-17/18) — Agente falhou por limite de gasto mensal antes de devolver o
  HANDOFF final; trabalho ja feito (adendo Secao 9 de criterios-aceite.md) recuperado do working
  tree e commitado pela sessao principal (commit 92854c3), sem custo real registrado no ledger
  para esta invocacao especifica.

  REAVALIACAO PRE-QA (2026-08-18) — as duas correcoes (#190 + #192) agora estao mescladas em
  `desenv`. PR #194 (`desenv->homolog`, merge commit) aberto trazendo os 6 commits a frente,
  substituindo o PR #189 (ja mesclado anteriormente) como PR de homologacao ATUAL. Corpo do PR
  #194 descreve as duas correcoes e referencia o PR #189 original e o comentario de Code Review
  estatico (https://github.com/DQM-BETA/omuletachou/pull/189#issuecomment-5319794063).
  `pr_homologacao` = 194 (PR #189 preservado em `pr_homologacao_anterior` para historico — ja
  estava mesclado, nao precisa ser refeito). QA segue pausado ate o Code Review reavaliar o PR
  #194.

  RESOLUCAO CODE REVIEW — PR #194 APROVADO e mesclado (2026-08-18, desenv->homolog, merge commit
  cabc2f96387ea547ca2e9a3ab68656df6354cc7b):
  - Build real (`dotnet build -c Release`) OK. `dotnet test -c Release --no-build` rodado 2x:
    433/436 sem Docker (3 falhas de Testcontainers, igual ao relatado pelo Dev/LT) e **436/436
    com Docker Desktop disponivel** (precisou ser iniciado manualmente no host, estava parado no
    inicio da sessao), confirmando ao vivo os 3 testes de integracao
    `ClaudeBudgetServiceIntegrationTests` contra Postgres real via Testcontainers.
  - Boot real via Docker: `docker compose build --no-cache api` + `docker compose up -d db api`
    -> ambos `healthy` -> `GET /health` 200 -> logs confirmam migrations aplicadas e Hangfire
    iniciado sem erros. Containers parados ao final da verificacao (`docker compose stop`).
  - Achado 1 (#190) validado: `JsonDocument.Parse` dentro de try/catch proprio em `GetJsonAsync`,
    convertendo `JsonException` -> `MercadoLivreApiException` (mesmo tipo capturado pelos
    chamadores). 3 testes novos exercitam `CollectAsync` completo (HttpMessageHandler mockado +
    DbContext in-memory), incluindo o cenario de JSON malformado no MEIO do ciclo confirmando que
    `SaveChangesAsync` (unico, ao final) preserva os produtos validos.
  - Achado 2 (#192) validado com rigor extra pedido pela sessao principal: comparei o branch
    `else` (Amazon/Shopee) do novo `ScoreProductAsync` contra
    `git show 393134d5f869ec1b16769cfd32a26cb155025bea:.../ClaudeAiService.cs` (estado do PR #189
    ANTES desta mudanca) — texto de conteudo dos raw string literals (`systemPrompt` e
    `userMessage`) identico palavra por palavra; diferenca de indentacao visual no source nao
    afeta o conteudo stripado pelo C# (determinado pela coluna do delimitador de fechamento
    `"""`). Prompt do Mercado Livre confirmado omitindo a linha de desconto + instrucao explicita
    de nao penalizar ausencia do dado (cenarios 9.1-9.3). Cenario 9.4 (nao-regressao Amazon/
    Shopee) confirmado por teste `[Theory]` dedicado, alem da comparacao manual acima.
  - Riscos herdados (schema `/highlights`, permalink `/p/{catalog_product_id}`) RETENTADOS mais
    uma vez com acesso de rede real deste ambiente: `curl api.mercadolibre.com/sites/MLB/categories`
    (direto e de dentro do container `afiliado_api`) e `curl mercadolivre.com.br/p/MLB16855791`
    -> AMBOS 403 `PA_UNAUTHORIZED_RESULT_FROM_POLICIES`. Mesmo bloqueio de politica de rede ja
    documentado pelo Dev/LT/CR anteriores (nao e resposta do Mercado Livre, nao e achado novo).
    Continua nao confirmado ao vivo por nenhum agente. Recomendacao mantida ao QA: retentar em
    ambiente com internet real (ex. VM Oracle) e, se tambem bloqueado, tratar o primeiro ciclo
    real do `CollectorJob` em producao como gate final.
  - Nenhum comentario do `/code-review` estatico foi postado no PR #194 (verificado via
    `gh api repos/DQM-BETA/omuletachou/issues/194/comments` — vazio); nao houve achado adicional a
    incorporar.
  - Evidencia completa postada como comentario no PR:
    https://github.com/DQM-BETA/omuletachou/pull/194#issuecomment-5327837114
  - PR #194 mesclado `desenv->homolog` via merge commit `cabc2f96387ea547ca2e9a3ab68656df6354cc7b`.
    `code_review_homolog_pr` = 194, `etapa_atual` = QA.

  QA REPROVOU (1a rodada, 2026-08-18) — ver etapa 23 do historico. Bug: painel de itens "skipped"
  em mercadolivre-links.component.html fica inacessivel quando a lista de pendentes esvazia apos
  o import (aninhado dentro do *ngIf de products.length > 0 do card pai). Relatorio completo em
  relatorio-qa.md, screenshots em screenshots/. Nao aciona a trava anti-loop (1a reprovacao).

  MAPEAMENTO LT (2026-08-18) — sub-issue #195 criada (stack:angular, task_id:Sub-F), TDD
  obrigatorio, branch `feature/ISSUE-195-skipped-panel-visibilidade` base `desenv`. tasks.md
  atualizado com secao Sub-F completa (contexto tecnico: desacoplar a visibilidade do
  mat-expansion-panel de skipped da condicao products.length > 0 do import-card pai). Pronta para
  Dev Angular.

  MERGE SUB-F #195 + PR HOMOLOGACAO (LT, 2026-08-18):
  - PR #196 (feature/ISSUE-195-skipped-panel-visibilidade -> desenv) verificado (124/124 testes
    reportados pelo Dev, build producao OK, sem CI configurado no repo) e mesclado via squash +
    delete-branch (commit `cf89cdcea5c2996e52724bbcaa1395a54da493c5`). Sub-issue #195 fechada.
    `desenv_tasks_merged` = ["#184","#183","#185","#190","#192","#195"] — todas as sub-issues e
    correcoes (incluindo a do QA 1a rodada) agora em `desenv`.
  - Novo PR #197 (`desenv->homolog`, merge commit) aberto trazendo essa ultima correcao. Corpo do
    PR descreve o bug encontrado pelo QA (painel de skipped inacessivel), referencia
    `relatorio-qa.md` e o comentario do QA na Issue #182
    (https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5328061163).
  - `pr_homologacao` = 197 (PR #194 preservado em `pr_homologacao_anterior`). `code_review_homolog_pr`
    resetado (~) ate a nova rodada de Code Review avaliar o PR #197. `etapa_atual` = Code Review.
  - Riscos herdados ainda pendentes de validacao ao vivo (schema `/highlights`, permalink
    `/p/{catalog_product_id}`) permanecem documentados acima — recomendacao mantida ao QA de
    retentar em ambiente com internet real (ex. VM Oracle); se tambem bloqueado, tratar o primeiro
    ciclo real do `CollectorJob` em producao como gate final de validacao desses dois riscos.
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
| 10 | Líder Técnico (merge Sub-B #184) | lider-tecnico | sonnet | 42710 | 14 | 113s | PR #186 (feature/ISSUE-184-fluxo-semi-manual-link-afiliado → desenv) verificado (426/426 testes, sem CI configurado no repo) e mesclado via squash + delete-branch. Sub-issue #184 fechada. `desenv_tasks_merged` = ["#184"]. Sub-A (#183) e Sub-C (#185) ainda pendentes — PR desenv→homolog NÃO criado. |
| 11 | Dev .NET (Sub-A #183) | dev-dotnet | sonnet | 150764 | 49 | 698s | `MercadoLivreCollector` reconstruído com Highlights API. 415/415 testes passando, boot real via Docker (`/health` 200) confirmado. Nota de risco: confirmação ao vivo do schema `/highlights` não pôde ser reexecutada (sandbox do Dev bloqueia `api.mercadolibre.com`) — parsing escrito defensivamente (aceita `content[]` ou `results[]`), documentado no código/PR; QA deve validar ao vivo. PR #187 feature→desenv aberto. |
| 12 | Líder Técnico (merge Sub-A #183) | lider-tecnico | sonnet | 42128 | 9 | 94s | PR #187 (feature/ISSUE-183-mercadolivrecollector-highlights → desenv) mesclado via squash + delete-branch (commit `533e4020`). Sub-issue #183 fechada. `desenv_tasks_merged` = ["#184","#183"]. Risco herdado do parsing defensivo de `/highlights` registrado em `blockers` para validação do CR/QA. Falta só Sub-C (#185) para abrir PR desenv→homolog. |
| 13 | Dev Angular (Sub-C #185) | dev-angular | sonnet | 220863 | 114 | 1245s | Tela "Links de Afiliado — Mercado Livre" implementada (standalone component, estados loading/vazio/erro, validação client-side de contagem pré-import, painel de skipped, retry preservando texto colado, responsivo). 120/120 testes (92,3% statements), build produção OK, `ng serve` validado (chunk lazy 200). Achado técnico: spy de `MatSnackBar` falha silenciosamente em standalone components que importam `MatSnackBarModule` diretamente (injector local sombreia o singleton do TestBed) — contornado testando o DOM real via `OverlayContainer`. PR #188 feature→desenv aberto. |
| 14 | Líder Técnico (merge Sub-C #185 + PR homologação) | lider-tecnico | sonnet | 55614 | 20 | 182s | PR #188 (feature/ISSUE-185-links-afiliado-ml → desenv) verificado (120/120 testes, build produção e `ng serve` já validados pelo Dev, sem CI configurado no repo) e mesclado via squash + delete-branch (commit `e3104ec`). Sub-issue #185 fechada. `desenv_tasks_merged` = ["#184","#183","#185"] — as 3 sub-issues da Issue-pai #182 agora estão em `desenv`. PR #189 (`desenv→homolog`, merge commit) aberto cobrindo a Issue-pai #182 completa: reconstrução do MercadoLivreCollector via Highlights API, fluxo semi-manual de link de afiliado (Gate 1.5), nova tela do dashboard. Corpo do PR inclui os dois riscos pendentes de validação ao vivo (parsing defensivo do schema `/highlights` e o permalink `/p/{catalog_product_id}`) para Code Review/QA. `pr_homologacao` = 189, `etapa_atual` = Code Review. |
| 15b | `/code-review` (sessão principal, camada estática) | orquestrador (multi-agente) | sonnet+haiku | 1070466 | 137 | 1526s | Análise multi-agente do PR #189 (16 sub-invocações: elegibilidade, CLAUDE.md, resumo, 5 agentes de auditoria paralela, 7 de scoring de confiança, recheck final). 2 achados ≥80 de confiança postados como comentário no PR: `JsonDocument.Parse` sem try/catch em `GetJsonAsync` (risco de perder o ciclo de coleta inteiro) e `DiscountPct=0` colidindo com o critério de scoring da IA (risco de zerar aprovação do canal ML). 5 achados descartados por baixa confiança (&lt;80). Comentário: https://github.com/DQM-BETA/omuletachou/pull/189#issuecomment-5319794063 |
| 15 | Code Review (PR #189, homologação) | code-review | sonnet | 209304 | 89 | 912s | **APROVADO.** Build real (`dotnet build`/`dotnet test` 427/427) + boot real via Docker (rebuild sem cache, db+api+dashboard healthy) + suíte dashboard (120/120, cobertura 92.32% medida ao vivo) + build produção. Integração real testada end-to-end (produto de teste inserido no Postgres → `GET /api/products?status=AwaitingAffiliateLink` confirmou `sourceUrl` → `POST /api/products/affiliate-links/import` confirmou isolamento de falha por item → banco reconferido `Queued`+`affiliate_link` → dashboard/nginx proxy confirmado). Diff conferido linha a linha contra `especificacao-tecnica.md`/`design.md` — conformidade total. Os 2 riscos herdados (schema `/highlights`, permalink `/p/{id}`) foram retentados com acesso de rede real e continuam bloqueados pela mesma política de rede do ambiente de execução (`PolicyAgent` 403, não resposta do Mercado Livre) — não é achado novo, é reconfirmação independente do mesmo bloqueio já documentado pelo Dev/LT; recomendação explícita ao QA para retentar em ambiente com internet real e, se ainda bloqueado, tratar o primeiro ciclo real do `CollectorJob` em produção como gate final. Achado não-bloqueante registrado (cap `MaxPageSize=100` pré-existente conflita com a suposição `pageSize=200` da especificação técnica §3.6 — sem perda de dado, só sem aviso de mais pendências além de 100). PR #189 mesclado `desenv→homolog` via merge commit `393134d5f869ec1b16769cfd32a26cb155025bea`. `code_review_homolog_pr` = 189, `etapa_atual` = QA. |
| 16 | Líder Técnico (correção pré-QA, achados `/code-review` PR #189) | lider-tecnico | sonnet | 93207 | 16 | 343s | Gerente pediu correção dos 2 achados antes do QA. Achado 1 (JsonDocument.Parse fora do try/catch em GetJsonAsync, quebra isolamento de falha): sub-issue #190 criada (stack:dotnet), tasks.md atualizado (Sub-D), TDD obrigatório, branch feature/ISSUE-190-json-parse-try-catch base desenv. Achado 2 (DiscountPct=0 fixo para ML colide com critério "desconto mínimo 15%" do scoring de IA, reprova sistematicamente o canal ML): analisado, nenhuma correção técnica avaliada evita o efeito de isentar ML do critério — é decisão de negócio, não implementado. Análise completa postada na Issue #182 (comentário) e escalada, sugerindo PM Fase 2. `etapa_atual` = Em Desenvolvimento (sub-issue #190 pronta para Dev; Achado 2 aguardando decisão do Gerente). |
| 17 | Dev .NET (Sub-D #190) | dev-dotnet | sonnet | 79794 | 25 | 296s | `GetJsonAsync` corrigido — `JsonDocument.Parse` movido para dentro de try/catch próprio, convertendo `JsonException` em `MercadoLivreApiException` (mesmo padrão das demais falhas do método). TDD: 3 testes novos confirmando isolamento por categoria/produto mesmo com JSON malformado no meio do ciclo (RED→GREEN confirmado). Suite completa 430/430. PR #191 feature→desenv aberto. |
| 18 | PM (Fase 2 — revisão de requisitos, Achado 2) | pm-analista-negocios | sonnet | ~ | ~ | ~ | **Agente falhou por limite de gasto mensal antes de devolver o HANDOFF final** (trabalho já feito recuperado do working tree e commitado pela sessão principal, sem custo real registrado). Gerente decidiu Opção A (isentar Mercado Livre do critério de desconto mínimo no scoring de IA, comentário https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5319915601). Formalizado como adendo Seção 9 (cenários 9.1-9.5) em `criterios-aceite.md`: prompt de `ClaudeAiService.ScoreProductAsync` não deve enviar `DiscountPct=0` como sinal real para ML (omitir quando indisponível), ausência de desconto não reprova/penaliza, demais 4 critérios inalterados, Amazon/Shopee sem regressão (cenário 9.4). Resumo postado como comentário na Issue #182. Sem ambiguidade arquitetural (mudança pontual, já compreendida em `ClaudeAiService.cs`) — encaminhado direto ao LT, sem Arquiteto. |
| 19 | Líder Técnico (merge Sub-D #190 + mapeamento Sub-E/#192) | lider-tecnico | sonnet | 108435 | 31 | 391s | **Tarefa 1**: PR #191 (feature/ISSUE-190-json-parse-try-catch → desenv) mesclado via squash (commit `83e28241248000dfd859c03d170bf0dc017b732e`, 430/430 testes reportados pelo Dev). Sub-issue #190 fechada. `desenv_tasks_merged` = ["#184","#183","#185","#190"]. **Tarefa 2**: sub-issue #192 criada (stack:dotnet, task_id:Sub-E) mapeando o Achado 2 (já decidido pelo Gerente, Opção A, formalizado pelo PM na Seção 9 de `criterios-aceite.md`, commit `92854c3`) — isentar Mercado Livre do critério de desconto mínimo em `ClaudeAiService.ScoreProductAsync`, TDD obrigatório, branch `feature/ISSUE-192-scoring-ml-desconto` base `desenv`. `tasks.md` atualizado com seção Sub-E completa (contexto técnico, CA 9.1-9.5). Sub-issue pronta para Dev, não spawnado (decisão da sessão principal). |
| 20 | Dev .NET (Sub-E #192) | dev-dotnet | sonnet | 76552 | 26 | 308s | `ClaudeAiService.ScoreProductAsync` ajustado — prompt condicional por `product.Platform`: Mercado Livre omite a linha de desconto mínimo (systemPrompt) e `Desconto: X%` (userMessage), com instrução explícita para não penalizar ausência de dado; Amazon/Shopee mantidos byte-a-byte idênticos (não-regressão, cenário 9.4). TDD: 5 testes novos (cenários 9.1-9.4, incluindo `[Theory]` de não-regressão p/ Amazon/Shopee), RED→GREEN confirmado. Suíte 433/436 (3 falhas pré-existentes de integração com Testcontainers/Docker, confirmadas também na baseline `desenv` sem a mudança). PR #193 feature→desenv aberto. |
| 21 | Líder Técnico (merge Sub-E #192 + PR homologação atualizado) | lider-tecnico | sonnet | 58384 | 11 | 190s | PR #193 (feature/ISSUE-192-scoring-ml-desconto → desenv) verificado (433/436 testes, 3 falhas pré-existentes de `ClaudeBudgetServiceIntegrationTests` por falta de Docker/Testcontainers, confirmadas na baseline `desenv`) e mesclado via squash + delete-branch (commit `d37a16435c266c8e2cf1f03543582b2715c799c1`). Sub-issue #192 fechada. `desenv_tasks_merged` = ["#184","#183","#185","#190","#192"] — todas as sub-issues e correções pré-QA agora em `desenv`. Novo PR #194 (`desenv→homolog`, merge commit) aberto, trazendo os Achados 1 (#190) e 2 (#192) do `/code-review` estático no PR #189, com corpo referenciando o PR #189 original e o comentário de Code Review estático. `pr_homologacao` = 194 (substitui 189, preservado em `pr_homologacao_anterior`), `code_review_homolog_pr` = 194, `etapa_atual` = Code Review. |
| 22 | Code Review (PR #194, reavaliação homologação) | code-review | sonnet | 126510 | 50 | 1372s | **APROVADO.** Build real (`dotnet build -c Release`) OK. `dotnet test -c Release --no-build` rodado 2x: 433/436 sem Docker (baseline), **436/436 com Docker Desktop disponível** (iniciado manualmente na sessão, incluindo os 3 testes de integração `ClaudeBudgetServiceIntegrationTests` via Testcontainers/Postgres real). Boot real via Docker (`docker compose build --no-cache api` + `up -d db api`, ambos `healthy`, `/health` 200, logs sem erro; containers parados ao final). Achado 1 (#190) validado: `JsonDocument.Parse` dentro de try/catch em `GetJsonAsync`, 3 testes novos cobrindo isolamento de falha inclusive no meio do ciclo. Achado 2 (#192) validado com rigor extra: comparação manual do branch Amazon/Shopee do novo prompt contra `git show 393134d5...:ClaudeAiService.cs` (estado pré-mudança) confirmou texto idêntico palavra por palavra nos raw string literals — não-regressão confirmada além dos testes automatizados. Riscos herdados (schema `/highlights`, permalink `/p/{id}`) retentados com rede real — 403 `PA_UNAUTHORIZED_RESULT_FROM_POLICIES`, mesmo bloqueio já documentado, não é achado novo. Nenhum comentário do `/code-review` estático no PR #194 (verificado, vazio). Evidência completa: https://github.com/DQM-BETA/omuletachou/pull/194#issuecomment-5327837114. PR #194 mesclado `desenv→homolog` via merge commit `cabc2f96387ea547ca2e9a3ab68656df6354cc7b`. `code_review_homolog_pr` = 194, `etapa_atual` = QA. |
| 23 | QA (1ª rodada, homolog) | qa | sonnet | 161556 | 95 | 1018s | **REPROVADO.** Ambiente `homolog` (commit `cabc2f96...`) real via Docker, 436/436 backend + 120/120 dashboard. Fluxo crítico do link de afiliado validado ponta a ponta na UI real (tela `mercadolivre-links` via Playwright ad-hoc — repo não tem `test:visual`, Gate Visual formal N/A) e confirmado direto no Postgres: `affiliate_link` persistido é distinto de `source_url`, com tag rastreável, pareado corretamente por ordem. Sem regressão Amazon/Shopee; Seção 9 (isenção de desconto ML) funcionando; isolamento de falha por categoria/produto confirmado ao vivo (ciclo real do CollectorJob, degradação graciosa em falha OAuth). **Bug encontrado**: painel de itens "skipped" em `mercadolivre-links.component.html` fica inacessível quando a lista de pendentes esvazia após o import — está aninhado dentro do `<mat-card>` controlado por `*ngIf="!loading && !errorMessage && products.length > 0"`, então some do DOM junto com o card mesmo com `skipped.length > 0` e o snackbar reportando itens pulados. Reproduzido 2x (screenshots `06`,`08`,`09`). Riscos herdados (schema `/highlights`, permalink `/p/{id}`) continuam bloqueados pela mesma política de rede (403 `PA_UNAUTHORIZED_RESULT_FROM_POLICIES`) — nuance nova: `POST /oauth/token` no mesmo host NÃO é bloqueado, sugerindo bloqueio anti-bot em endpoints de leitura específicos do ML, não bloqueio geral de sandbox. Relatório completo: `relatorio-qa.md`. Comentário: https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5328061163 |
| 24 | Líder Técnico (mapeamento falha QA 1ª rodada) | lider-tecnico | sonnet | 96642 | 15 | 319s | Mapeou o Achado 1 (bloqueante) do `relatorio-qa.md` (painel de skipped inacessível quando `products.length` chega a 0 após reload) para sub-issue nova `#195` (stack:angular, task_id:Sub-F), TDD obrigatório, branch `feature/ISSUE-195-skipped-panel-visibilidade` base `desenv`. `tasks.md` atualizado com seção Sub-F completa (5 cenários Given/When/Then, contexto técnico apontando `mercadolivre-links.component.html` linhas ~120/~163-166 e o `.ts` correspondente). `sub_issues`/`sub_issues_frontend` atualizados em `estado.md`. `desenv_tasks_merged` não alterado (sub-issue nova, não mesclada ainda). `etapa_atual` = Em Desenvolvimento. Pronta para Dev Angular. |
| 25 | Dev Angular (Sub-F #195) | dev-angular | sonnet | 84585 | 25 | 296s | Causa raiz confirmada: painel de skipped preso ao `*ngIf` do card de import. Fix: card agora renderiza com `products.length > 0 || (skipped && skipped.length > 0)`; conteúdo específico de pendentes isolado em `<ng-container *ngIf="products.length > 0">`; painel de skipped mantém `*ngIf="skipped && skipped.length > 0"` independente. TDD: 4 testes novos reproduzindo o bug exato (RED→GREEN confirmado). Suíte 124/124, build produção OK. PR #196 feature→desenv aberto. |
| 26 | Líder Técnico (merge Sub-F #195 + PR homologação) | lider-tecnico | sonnet | 68665 | 15 | 245s | PR #196 (feature/ISSUE-195-skipped-panel-visibilidade → desenv) verificado (124/124 testes reportados pelo Dev, build produção OK, sem CI configurado no repo) e mesclado via squash + delete-branch (commit `cf89cdcea5c2996e52724bbcaa1395a54da493c5`). Sub-issue #195 fechada. `desenv_tasks_merged` = ["#184","#183","#185","#190","#192","#195"] — todas as sub-issues e correções (incluindo a do QA 1ª rodada) agora em `desenv`. Novo PR #197 (`desenv→homolog`, merge commit) aberto trazendo essa última correção pendente; corpo do PR descreve o bug encontrado pelo QA (painel de skipped inacessível), referencia `relatorio-qa.md` e o comentário do QA na Issue #182. `pr_homologacao` = 197 (PR #194 preservado em `pr_homologacao_anterior`), `code_review_homolog_pr` resetado até nova avaliação, `etapa_atual` = Code Review. |
| 27 | Code Review (PR #197, reavaliação homologação — correção pós-QA #195) | code-review | sonnet | 129151 | 66 | 516s | **APROVADO.** Build real: `dotnet build -c Release` OK (backend intocado neste PR). `dotnet test -c Release --no-build`: **436/436** ✅ (Docker Desktop disponível, inclui os 3 testes de integração via Testcontainers/Postgres real). `npx ng test --watch=false --browsers=ChromeHeadless` (dashboard): **124/124** ✅ (inclui os 4 testes novos do bug #195). `npx ng build` produção OK. Boot real via Docker (`docker compose build --no-cache api dashboard` + `up -d db api dashboard`, todos `healthy`/`Up`, `/health` 200, dashboard 200). **Reprodução ao vivo do cenário exato do QA**: seed de 2 produtos `AwaitingAffiliateLink` no Postgres, login real, colei 2 links, simulei a mesma condição de corrida (1 produto virou `Pending` no banco antes do clique em Importar) — backend importou 1 (`Queued`, `affiliate_link` real e distinto do `source_url`, confirmado no Postgres) e pulou 1. Resultado no DOM: `import-card` renderizou (antes do fix seria removido do DOM), `skipped-panel` visível com o item e motivo corretos, expansão via clique funcionando. **Não-regressão do estado vazio**: reload com 0 pendentes/0 skipped em memória → `import-card` não renderiza, mensagem de vazio exibida normalmente (sem card quebrado). Screenshots em `cr197-04-after-import-dom.png`/`cr197-05-panel-expanded.png`/`cr197-06-fully-empty-state.png` (anexados ao comentário do PR, não commitados no repo). Sem achados do `/code-review` estático no PR #197 (0 comentários, 0 reviews — verificado). Sem `.first()`/`.nth()` em specs E2E (dashboard não tem Playwright/`test:visual`, N/A). Conformidade com `especificacao-tecnica.md` §3.6 preservada (fix é aditivo, não altera contrato funcional). Evidência completa: https://github.com/DQM-BETA/omuletachou/pull/197#issuecomment-5328328425. PR #197 mesclado `desenv→homolog` via merge commit `f4088a82daeec9a266ee00b78ac946e0e49643df`. `code_review_homolog_pr` = 197, `etapa_atual` = QA. |

| 28 | QA (2ª rodada, homolog) | qa | sonnet | 105284 | 59 | 550s | **APROVADO.** `homolog` sincronizado no commit `f4088a82...`. Suíte completa: 436/436 backend (Testcontainers/Docker real) + 124/124 dashboard + `tsc --noEmit` limpo. Reproduziu ao vivo o cenário exato do relatório da 1ª rodada (2 produtos ML seedados, condição de corrida forçando 1 skip, import esvaziando `products`) — confirmado: painel de skipped permanece visível e funcional, coexistindo com o estado vazio. Confirmado no Postgres: produto importado com `affiliate_link` real distinto do `source_url`; produto pulado preservado sem link. Revalidação rápida sem regressão: fluxo ponta a ponta do link de afiliado, isenção de desconto ML (Seção 9, arquivo fora do diff do PR #197), isolamento de falha por item, Amazon/Shopee intactos. Riscos herdados (schema `/highlights`, permalink `/p/{id}`) continuam bloqueados por `PA_UNAUTHORIZED_RESULT_FROM_POLICIES` — reafirmados como pendência de monitoramento em produção, não bloqueador. Relatório atualizado: `relatorio-qa.md`. Comentário: https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5328446549 |

## Próximo passo

**QA aprovou (2ª rodada).** Todas as sub-issues (#183, #184, #185, #190, #192, #195) mescladas e
validadas em `homolog`. Próximo passo: **Líder Técnico** abre o PR `homolog→main` (merge commit,
NUNCA squash) → **GATE 2: Gerente** (aprovação humana obrigatória antes do merge final).
- As correções dos Achados 1 (#190) e 2 (#192) do `/code-review` estático no PR #189 permanecem
  validadas e em `homolog`.
- Riscos herdados ainda pendentes de validação ao vivo (schema `/highlights`, permalink
  `/p/{catalog_product_id}`) permanecem documentados em `blockers` — recomendação mantida ao QA de
  retentar em ambiente com internet real (ex. VM Oracle); se também bloqueado, tratar o primeiro
  ciclo real do `CollectorJob` em produção como gate final.
</content>
