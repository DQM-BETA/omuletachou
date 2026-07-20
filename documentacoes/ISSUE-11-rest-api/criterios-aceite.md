# Critérios de aceite — ISSUE-11: REST API (Dashboard + Endpoints Públicos)

Organizados por sub-issue (A–E), conforme fatiamento definido pelo Gerente no Gate 1. O LT usará esta numeração para criar as 5 sub-issues reais no GitHub.

---

## Sub-A — Autenticação (JWT, login, middleware)

**CA-A1 — Login com credenciais válidas**
Given um usuário existente na tabela `users` com senha armazenada como hash bcrypt
When `POST /api/auth/login` é chamado com o email e a senha em texto plano correspondentes
Then a API retorna 200 com um JWT válido, contendo expiração de 24h a partir do momento da emissão.

**CA-A2 — Login com senha incorreta**
Given um usuário existente na tabela `users`
When `POST /api/auth/login` é chamado com o email correto e senha incorreta
Then a API retorna 401, sem indicar especificamente se o erro foi no email ou na senha.

**CA-A3 — Login com email inexistente**
Given nenhum usuário cadastrado com o email informado
When `POST /api/auth/login` é chamado
Then a API retorna 401, com a mesma mensagem genérica usada em CA-A2 (evitar enumeração de usuários).

**CA-A4 — Seed do usuário via variável de ambiente**
Given um ambiente de deploy sem nenhum usuário cadastrado na tabela `users`
When a aplicação inicializa e a variável de ambiente de seed (email/senha do operador) está definida
Then um único usuário é criado com a senha já armazenada como hash bcrypt (nunca em texto plano).

**CA-A5 — Senha nunca armazenada em texto plano**
Given o processo de criação/seed do usuário
When a senha é persistida na tabela `users`
Then o valor armazenado é sempre um hash bcrypt, nunca a senha original.

**CA-A6 — Middleware `[Authorize]` bloqueia acesso sem token**
Given qualquer endpoint protegido (ex.: `GET /api/products`)
When a requisição é feita sem header `Authorization`
Then a API retorna 401 antes de a requisição alcançar a lógica do controller.

**CA-A7 — Middleware `[Authorize]` bloqueia token expirado ou inválido**
Given um JWT expirado (mais de 24h desde a emissão) ou com assinatura inválida
When a requisição é feita a um endpoint protegido com esse token
Then a API retorna 401.

**CA-A8 — Middleware `[Authorize]` aceita token válido**
Given um JWT válido, não expirado, emitido pelo próprio `POST /api/auth/login`
When a requisição é feita a um endpoint protegido com esse token no header `Authorization: Bearer`
Then a requisição prossegue normalmente para a lógica do controller.

**CA-A9 — Endpoints públicos e login não exigem autenticação**
Given os endpoints `POST /api/auth/login` e qualquer rota sob `/api/public/*`
When chamados sem header `Authorization`
Then a requisição é processada normalmente, sem retornar 401 por ausência de token.

**CA-A10 — Chave de assinatura JWT fora do código-fonte**
Given a configuração de emissão/validação do JWT
When o código é revisado
Then a chave de assinatura é lida de variável de ambiente ou `appsettings` (não versionado com secret real), nunca hardcoded no código-fonte.

---

## Sub-B — ProductsController + QueueController

**CA-B1 — Listagem paginada de produtos**
Given produtos cadastrados no banco
When `GET /api/products` é chamado com token válido, sem filtros
Then a API retorna 200 com envelope `{ items, page, pageSize, totalItems, totalPages }`, `page=1` e `pageSize=20` (padrões).

**CA-B2 — Filtros de produtos por status e platform**
Given produtos com diferentes valores de `status` e `platform`
When `GET /api/products?status=pending&platform=amazon` é chamado
Then apenas produtos que atendem a ambos os filtros são retornados em `items`.

**CA-B3 — Detalhe de produto inclui ai_score e ai_reason**
Given um produto existente com `AiScore` e `AiReason` preenchidos
When `GET /api/products/{id}` é chamado com token válido
Then a resposta 200 inclui os campos `ai_score` e `ai_reason` do produto.

**CA-B4 — Detalhe de produto inexistente**
Given um `id` que não corresponde a nenhum produto
When `GET /api/products/{id}` é chamado
Then a API retorna 404.

**CA-B5 — Atualização de status do produto (valor válido)**
Given um produto existente
When `PATCH /api/products/{id}/status` é chamado com body `{ "status": "rejected" }`
Then o status do produto é atualizado e a API retorna 200 (ou 204).

**CA-B6 — Atualização de status com valor inválido**
Given um produto existente
When `PATCH /api/products/{id}/status` é chamado com um valor de `status` fora do enum permitido (`rejected`/`pending`)
Then a API retorna 400 com mensagem de validação, sem alterar o produto.

**CA-B7 — Listagem paginada da fila de publicação**
Given itens cadastrados em `PublicationQueue`
When `GET /api/queue` é chamado com token válido, com filtros opcionais `status`/`network`
Then a API retorna 200 com o envelope de paginação padrão, filtrado conforme os parâmetros informados.

**CA-B8 — Itens pendentes de aprovação manual (Facebook)**
Given itens em `PublicationQueue` com `Status = manual_pending`
When `GET /api/queue/manual` é chamado
Then apenas esses itens são retornados.

**CA-B9 — Retry de item com falha**
Given um item de `PublicationQueue` com status de falha
When `POST /api/queue/{id}/retry` é chamado
Then o item é atualizado para `Status = Scheduled` e `ScheduledAt = now`, e a API retorna 200 (ou 204).

**CA-B10 — Retry de item inexistente**
Given um `id` que não corresponde a nenhum item de `PublicationQueue`
When `POST /api/queue/{id}/retry` é chamado
Then a API retorna 404.

**CA-B11 — Endpoints protegidos sem token**
Given os endpoints de `ProductsController` e `QueueController`
When chamados sem token de autenticação válido
Then a API retorna 401 (reforça CA-A6 no contexto destes controllers específicos).

---

## Sub-C — SettingsController + JobsController

**CA-C1 — Listagem de settings mascara valores sensíveis**
Given uma configuração com chave terminada em `_key`, `_secret`, `_token` ou `_password` (ex.: `telegram.bot_token`)
When `GET /api/settings` é chamado com token válido
Then o valor retornado para essa chave mostra apenas os últimos 4 caracteres, no formato `****************a1b2`, nunca o valor puro.

**CA-C2 — Listagem de settings não mascara chaves não sensíveis**
Given uma configuração cuja chave não termina em `_key`/`_secret`/`_token`/`_password`
When `GET /api/settings` é chamado
Then o valor é retornado normalmente, sem mascaramento.

**CA-C3 — Settings sensível não configurada não aplica máscara sobre string vazia**
Given uma chave sensível sem valor configurado (vazio/nulo)
When `GET /api/settings` é chamado
Then a resposta indica "não configurado" (ex.: `null` ou flag específica), sem aplicar o formato de máscara sobre uma string vazia.

**CA-C4 — Atualização de configuração existente**
Given uma chave de configuração já existente (ex.: `telegram.bot_token`)
When `PUT /api/settings/{key}` é chamado com body `{ "value": "novo-valor" }`
Then o novo valor é persistido integralmente, sobrescrevendo o anterior.

**CA-C5 — Atualização de configuração inexistente**
Given uma chave de configuração que não existe na base
When `PUT /api/settings/{key}` é chamado
Then a API retorna 404, sem criar a chave implicitamente.

**CA-C6 — PUT nunca retorna o valor completo de volta**
Given qualquer chamada bem-sucedida a `PUT /api/settings/{key}` para uma chave sensível
When a resposta é inspecionada
Then o corpo da resposta não contém o valor completo em texto plano (nem o novo, nem o antigo) — no máximo confirmação de sucesso ou o valor já mascarado.

**CA-C7 — Disparo manual do job collector**
Given o sistema com o `CollectorJob` registrado no Hangfire
When `POST /api/jobs/collector/trigger` é chamado com token válido
Then o job é enfileirado para execução imediata e a API retorna 200 (ou 202).

**CA-C8 — Disparo manual do job processor**
Given o sistema com o `ProcessorJob` registrado
When `POST /api/jobs/processor/trigger` é chamado com token válido
Then o job é enfileirado para execução imediata e a API retorna 200 (ou 202).

**CA-C9 — Disparo manual do job publisher**
Given o sistema com o `PublisherJob` registrado
When `POST /api/jobs/publisher/trigger` é chamado com token válido
Then o job é enfileirado para execução imediata e a API retorna 200 (ou 202).

**CA-C10 — Endpoints protegidos sem token**
Given os endpoints de `SettingsController` e `JobsController`
When chamados sem token de autenticação válido
Then a API retorna 401.

---

## Sub-D — PublicController + CORS + RateLimit

**CA-D1 — Listagem pública de deals sem autenticação**
Given produtos com `Status = published`
When `GET /api/public/deals` é chamado sem header `Authorization`
Then a API retorna 200 com a lista paginada, ordenada por `ai_score` desc internamente (sem expor o campo `ai_score` na resposta).

**CA-D2 — Campos expostos em /api/public/deals**
Given um produto `published` retornado em `/api/public/deals`
When a resposta é inspecionada
Then contém apenas os campos autorizados (`Title`, `SalePrice`, `OriginalPrice`, `DiscountPct`, `AffiliateLink`, `MediaUrl`, `MediaLocalPath` como URL pública, `Slug`, `Category`, `CollectedAt`, opcionalmente `Platform`) e nunca `ExternalId`, `AiScore`, `AiReason` ou qualquer campo de `app_settings`.

**CA-D3 — Detalhe por slug**
Given um produto `published` com `Slug` conhecido
When `GET /api/public/deals/{slug}` é chamado
Then a API retorna 200 com os dados desse produto (mesmo subconjunto de campos de CA-D2).

**CA-D4 — Detalhe por slug inexistente**
Given um `slug` que não corresponde a nenhum produto publicado
When `GET /api/public/deals/{slug}` é chamado
Then a API retorna 404.

**CA-D5 — Filtro por categoria**
Given produtos publicados em diferentes categorias
When `GET /api/public/deals/category/{category}` é chamado
Then apenas produtos publicados da categoria informada são retornados, paginados.

**CA-D6 — Paginação: valores padrão**
Given nenhum parâmetro `page`/`pageSize` informado
When qualquer endpoint de listagem paginada (`/api/products`, `/api/queue`, `/api/public/deals*`) é chamado
Then `page=1` e `pageSize=20` são aplicados por padrão.

**CA-D7 — Paginação: truncamento de pageSize acima do máximo**
Given `pageSize=500` informado explicitamente
When um endpoint paginado é chamado
Then o valor é truncado para 100, e a resposta reflete `pageSize=100` no envelope.

**CA-D8 — CORS: origin autorizado**
Given uma requisição com header `Origin: https://omuletachou.com.br` (ou qualquer uma das 5 origins da lista) a `/api/public/deals`
When a chamada é feita (incluindo preflight `OPTIONS`)
Then a resposta inclui os headers CORS liberando esse origin.

**CA-D9 — CORS: origin não autorizado**
Given uma requisição com header `Origin` fora da lista explícita de 5 origins
When a chamada é feita a `/api/public/deals`
Then a resposta não inclui headers CORS liberando esse origin (o navegador bloqueia a leitura da resposta no cliente).

**CA-D10 — CORS: nunca AllowAnyOrigin**
Given a configuração de CORS da aplicação (qualquer ambiente, incluindo desenvolvimento)
When o código/configuração é revisado
Then não há uso de `AllowAnyOrigin` em nenhum ambiente; a lista de origins é explícita e configurável via `appsettings.json` por ambiente.

**CA-D11 — Rate limit em endpoints de leitura pública**
Given mais de 60 requisições no mesmo minuto a partir do mesmo IP a `/api/public/deals`
When o limite é excedido
Then requisições adicionais nesse mesmo minuto retornam 429, sem processar a lógica do endpoint.

**CA-D12 — Rate limit não afeta outros IPs**
Given um IP que excedeu o limite de 60 req/min
When outro IP distinto chama o mesmo endpoint
Then a requisição do segundo IP é processada normalmente (limite é por IP, não global).

---

## Sub-E — PushController + ReportsController

**CA-E1 — Inscrição push sem autenticação**
Given uma `PushSubscription` válida enviada no body
When `POST /api/public/push/subscribe` é chamado sem header `Authorization`
Then a API retorna 200/201 e persiste a subscription no banco.

**CA-E2 — Cancelamento de inscrição push por endpoint**
Given uma `PushSubscription` já cadastrada com um `endpoint` específico
When `DELETE /api/public/push/unsubscribe` é chamado com esse `endpoint`
Then a subscription correspondente é removida do banco e a API retorna 200/204.

**CA-E3 — Cancelamento de endpoint não cadastrado**
Given um `endpoint` que não corresponde a nenhuma subscription cadastrada
When `DELETE /api/public/push/unsubscribe` é chamado
Then a API retorna 404 (ou 204 idempotente — decisão do LT/Dev a documentar em `design.md`), sem erro 500.

**CA-E4 — Rate limit em push/subscribe (endpoint de escrita)**
Given mais de 10 requisições no mesmo minuto a partir do mesmo IP a `/api/public/push/subscribe`
When o limite é excedido
Then requisições adicionais nesse mesmo minuto retornam 429.

**CA-E5 — Resumo de publicações dos últimos 7 dias**
Given itens de `PublicationQueue` publicados com sucesso nos últimos 7 dias, distribuídos entre diferentes redes sociais
When `GET /api/reports/summary` é chamado com token válido
Then a API retorna 200 com o total publicado agrupado por rede e por dia, cobrindo a janela dos últimos 7 dias.

**CA-E6 — Reports protegido, Push público**
Given `ReportsController` (protegido) e `PushController` (público)
When os respectivos endpoints são chamados sem/com token
Then `GET /api/reports/summary` retorna 401 sem token, enquanto `POST/DELETE /api/public/push/*` funcionam normalmente sem token.

---

## Transversais (aplicam-se a todas as sub-issues)

**CA-T1 — Testes de integração com WebApplicationFactory**
Given a suíte de testes do projeto
When `dotnet test` é executado
Then os principais endpoints (autenticados e públicos) são cobertos por testes de integração usando `WebApplicationFactory`, incluindo pelo menos um caso de 401 (sem token) e um caso de sucesso (com token válido) por controller protegido.

**CA-T2 — Sem chamadas externas reais nos testes**
Given a suíte de testes
When executada em CI
Then nenhum teste depende de rede externa, banco de produção ou credenciais reais (usa banco de teste/in-memory ou testcontainers, conforme padrão já definido pelo LT no repo).
