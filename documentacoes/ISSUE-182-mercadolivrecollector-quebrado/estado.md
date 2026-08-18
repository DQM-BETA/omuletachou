---
issue: 182
titulo: fix: MercadoLivreCollector quebrado — endpoint /sites/MLB/search descontinuado pela API, reconstruir com Highlights API
etapa_atual: Em Desenvolvimento
rota: normal
ultimo_agente: pm-analista-negocios
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
desenv_tasks_merged:
  - "#184"
  - "#183"
  - "#185"
sub_issues_frontend:
  "#185": angular
pr_homologacao: 189
pr_release: ~
code_review_homolog_pr: 189
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
  - ACHADO 2 (ESCALADO, nao decidido pelo LT) — `MercadoLivreCollector.UpsertProductAsync` seta
    `OriginalPrice = SalePrice` / `DiscountPct = 0` para todo produto ML (Highlights API nao expoe
    preco original/desconto em `/products/{id}` nem `/products/{id}/items` — fallback documentado
    no codigo, nao bug de implementacao). Esse `DiscountPct = 0` alimenta o prompt fixo de
    `ClaudeAiService.ScoreProductAsync` ("Desconto real minimo de 15%; precos inflados
    penalizam", linha ~39) — 0% e indistinguivel para a IA de "produto sem desconto real",
    entao o esperado e reprovacao sistematica (ou score bem abaixo de minScore=6) de TODO
    produto Mercado Livre, zerando na pratica a aprovacao desse canal, mesmo com o collector
    funcionando mecanicamente. Nunca foi decidido deliberadamente em nenhuma fase anterior (PRD,
    design.md, Gate 1.5) — efeito colateral descoberto agora pela revisao estatica.
    ANALISE DO LT: avaliada alternativa tecnica (tornar DiscountPct nullable e omitir a linha do
    prompt quando o dado nao esta disponivel, em vez de mandar 0% como se fosse valor verificado)
    — mas toda alternativa avaliada produz o MESMO efeito pratico: isentar o canal Mercado Livre
    do criterio de desconto minimo de 15% que se aplica as demais plataformas (Amazon/Shopee).
    Isso e literalmente a decisao de negocio "produtos ML nao precisam de desconto minimo" citada
    como exemplo de linha vermelha do mandato do LT — nao um bug a corrigir dentro do mandato
    tecnico. NAO IMPLEMENTADO. Opcoes levantadas para o Gerente (via PM Fase 2, se precisar
    refinar criterios-aceite.md):
      (A) excluir ML do criterio de desconto minimo — enviar DiscountPct=null (omitir a linha do
          prompt) so quando a plataforma nao tem esse dado, mantendo os outros 4 criterios de
          scoring (categoria/titulo/preco/prazo) sem penalizar por ausencia de sinal;
      (B) manter como esta (produtos ML sistematicamente reprovados/baixo score) ate existir fonte
          de preco original para ML — pausa de fato esse canal;
      (C) investigar fonte alternativa de preco de tabela/historico para ML (fora do escopo desta
          issue, trabalho adicional nao estimado).
    Analise completa postada como comentario na Issue #182:
    https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5319915601
    SEM sub-issue criada para o Achado 2 ate a decisao do Gerente.

  RESOLUCAO ACHADO 2 (Gerente, 2026-08-17, comentario
  https://github.com/DQM-BETA/omuletachou/issues/182#issuecomment-5319915601) — Opcao A escolhida:
  isentar o Mercado Livre do criterio de desconto minimo no scoring de IA. Formalizado como adendo
  (Secao 9, cenarios 9.1-9.5) em documentacoes/ISSUE-182-mercadolivrecollector-quebrado/criterios-aceite.md
  pelo PM (Fase 2, revisao de requisitos): o prompt de ClaudeAiService.ScoreProductAsync nao deve
  enviar DiscountPct=0 como sinal real para produtos de Mercado Livre (omitir o dado quando
  indisponivel, nao fingir 0%); a auxencia de desconto nao reprova nem penaliza o produto; os
  outros 4 criterios (categoria, titulo, preco final, prazo de entrega) continuam aplicados
  normalmente ao ML; Amazon/Shopee mantêm o criterio de desconto minimo de 15% sem nenhuma mudanca
  (nao regressao, cenario 9.4). Sem ambiguidade arquitetural (mudanca pontual, ja compreendida em
  ClaudeAiService.cs) — encaminhado direto ao LT para mapear como sub-issue/tarefa (TDD obrigatorio,
  mesmo padrao do Achado 1/#190), sem passar pelo Arquiteto.
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

## Próximo passo

Dois caminhos, ambos prontos para o Líder Técnico:
1. **Achado 1 (sub-issue #190, stack:dotnet):** pronta para Dev .NET — fix isolado, TDD obrigatório,
   branch `feature/ISSUE-190-json-parse-try-catch` base `desenv`. Após merge, LT abre novo PR
   `desenv→homolog` trazendo só esta correção.
2. **Achado 2 (DiscountPct=0 × critério de desconto mínimo do scoring de IA) — decidido:** Gerente
   escolheu a Opção A (isentar Mercado Livre do critério de desconto mínimo). Critérios de aceite
   formalizados na Seção 9 de `criterios-aceite.md` (cenários 9.1-9.5). Sem ambiguidade arquitetural
   — LT deve mapear a mudança em `ClaudeAiService.cs` (montagem do prompt de `ScoreProductAsync`)
   como sub-issue nova (TDD obrigatório, mesmo padrão do #190) e decidir se entra no mesmo PR
   `desenv→homolog` do #190 ou em PR separado.

QA continua pausado até as duas correções (Achado 1 + Achado 2) estarem mescladas em `homolog`.
