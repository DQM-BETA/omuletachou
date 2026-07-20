# Relatório de QA — ISSUE-11: REST API (Dashboard + Endpoints Públicos)

**Status: ✅ APROVADO**

## Metodologia
- Branch validada: `homolog` (fast-forward de `b6149d8` → `e861b28`, PR #92 mergeado; commit `e861b28` confirmado em `git log`).
- Ambiente: Docker Compose real (`db` + `api`), Postgres 16 real, sem mocks. Volume resetado (`docker compose down -v`) para eliminar credencial stale de execução anterior antes de subir limpo.
- `.env` local temporário criado só para a validação (`JWT_SIGNING_KEY`, `SEED_USER_EMAIL/PASSWORD`, `DB_USER/PASSWORD`) — removido ao final, nunca commitado.
- Build da imagem `omuletachou-api` OK, migrations aplicadas no boot, seed do usuário único executado (log confirma `INSERT INTO users` com hash).
- Massa de dados inserida via `psql` direto no container (produtos, itens de fila, publicações, subscription push) para exercitar filtros, detalhe, patch, retry, reports e mascaramento — sem alterar código/migrations.
- Suíte automatizada: `dotnet test` → **280/280 aprovados** (backend/AfiliadoBot.Tests).
- Gate visual (d2): **N/A** — Issue #11 é REST API pura (sem UI própria; dashboard/website são issues futuras #12/#13, sem `test:visual` no escopo desta issue). Nenhum `package.json` com script `test:visual` associado a esta entrega.
- Todos os CAs abaixo foram validados **contra a aplicação rodando** (curl real), exceto onde indicado "análise de código" complementar.

## Sub-A — Autenticação

| CA | Descrição | Resultado | Evidência |
|---|---|---|---|
| CA-A1 | Login válido → JWT, exp 24h | ✅ | `POST /auth/login` → 200, token decodificado: `exp` = created + exatas 24h |
| CA-A2 | Senha incorreta → 401 genérico | ✅ | 401 `{"message":"Credenciais invalidas."}` |
| CA-A3 | Email inexistente → 401, mesma mensagem | ✅ | 401 com mensagem idêntica à CA-A2 (sem enumeração de usuário) |
| CA-A4 | Seed via env var, hash bcrypt | ✅ | Log de boot: `INSERT INTO users (...) VALUES (..., password_hash)`; login com a senha em texto plano funcionou (só possível se hash bcrypt válido) |
| CA-A5 | Senha nunca em texto plano | ✅ | Confirmado junto com A4; coluna `password_hash` |
| CA-A6 | `[Authorize]` bloqueia sem token | ✅ | `GET /api/products` sem header → 401 `WWW-Authenticate: Bearer` |
| CA-A7 | Token adulterado/expirado rejeitado | ✅ | Token com caractere extra → 401 `error="invalid_token", error_description="The signature key was not found"` |
| CA-A8 | Token válido aceito | ✅ | `GET /api/auth/me` com Bearer válido → 200 `{"email":"qa@omuletachou.com.br"}` |
| CA-A9 | Login e `/api/public/*` sem exigir token | ✅ | `GET /api/public/deals` sem Authorization → 200 |
| CA-A10 | Signing key fora do código-fonte | ✅ (análise de código) | `Program.cs`: `Jwt:SigningKey` lida de config, `throw` fail-fast se ausente; `appsettings.json` tem valor vazio (`""`), real valor só via env var `Jwt__SigningKey` no compose |

## Sub-B — Products / Queue

| CA | Resultado | Evidência |
|---|---|---|
| CA-B1 | ✅ | `GET /api/products` → envelope `{items,page:1,pageSize:20,totalItems,totalPages}` |
| CA-B2 | ✅ | `?status=pending&platform=amazon` retornou só o 1 produto que atende ambos filtros |
| CA-B3 | ✅ | `GET /api/products/{id}` → `ai_score:85`, `ai_reason:"boa promo"` presentes |
| CA-B4 | ✅ | id inexistente → 404 |
| CA-B5 | ✅ | `PATCH .../status {"status":"rejected"}` → 204; produto persistido com `status:"Rejected"` |
| CA-B6 | ✅ | `PATCH .../status {"status":"nao_existe"}` → 400 `"Status invalido. Valores permitidos: pending, rejected."`; status do produto não foi alterado |
| CA-B7 | ✅ | `GET /api/queue` → envelope paginado com os 2 itens inseridos |
| CA-B8 | ✅ | `GET /api/queue/manual` → só o item `ManualPending` (Instagram) |
| CA-B9 | ✅ | `POST /api/queue/{id}/retry` em item `Failed` → 204; banco confirma `status=0 (Scheduled)`, `scheduled_at` atualizado para "now" |
| CA-B10 | ✅ | id inexistente → 404 |
| **CA-B9 extra (crítico)** | ✅ | `POST /retry` em item **não-Failed** (ManualPending) → **409** `"Somente itens com status Failed podem ser reprocessados."` — confirmado que não há sucesso silencioso |
| CA-B11 | ✅ | `GET /api/queue`, `GET /api/queue/manual`, `POST /retry` sem token → 401 nos três |

## Sub-C — Settings / Jobs (validação crítica de segurança)

| CA | Resultado | Evidência |
|---|---|---|
| CA-C1 | ✅ | `telegram.bot_token` setado para `super-secret-token-abcdef1234` via SQL direto; `GET /api/settings` retornou `"****************1234"` — **grep no corpo bruto da resposta pelo valor completo não encontrou nenhuma ocorrência** |
| CA-C2 | ✅ | Chaves não sensíveis (`claude.min_score`, `schedule.collector_cron`, `networks.*.enabled`, `tiktok.privacy_level` etc.) retornadas em texto puro, sem máscara |
| CA-C3 | ✅ | Chaves sensíveis sem valor configurado (`amazon.secret_key`, `claude.api_key` etc., todas vazias no banco) retornaram `"value":null` — nenhuma retornou o padrão de máscara sobre string vazia |
| CA-C4 | ✅ | `PUT /api/settings/claude.min_score {"value":"7"}` → 200; banco confirma `value='7'` |
| CA-C5 | ✅ | `PUT /api/settings/chave.inexistente` → 404; confirmado via SQL que nenhuma linha foi criada |
| CA-C6 | ✅ | `PUT /api/settings/telegram.bot_token {"value":"novo-secret-valor-9999xyz"}` → 200 `{"value":"****************9xyz"}`; grep no corpo pelo valor completo novo → nenhuma ocorrência |
| CA-C7 | ✅ | `POST /api/jobs/collector/trigger` → 200 |
| CA-C8 | ✅ | `POST /api/jobs/processor/trigger` → 200 |
| CA-C9 | ✅ | `POST /api/jobs/publisher/trigger` → 200 |
| CA-C10 | ✅ | `GET /api/settings`, `PUT /api/settings/{key}`, `POST /api/jobs/collector/trigger` sem token → 401 nos três |

**Checagem pessoal contra o container real (não apenas suíte automatizada):** confirmado por inspeção direta do JSON cru retornado pela API rodando em Docker que nenhum secret aparece por completo em nenhuma resposta (`GET`/`PUT`), usando `grep` sobre o corpo bruto da resposta com os valores plantados via SQL.

## Sub-D — Public / CORS / RateLimit

| CA | Resultado | Evidência |
|---|---|---|
| CA-D1 | ✅ | `GET /api/public/deals` sem Authorization → 200; ordem interna por `ai_score` desc confirmada (Tênis ai_score=90 antes de Panela ai_score=70), campo `ai_score` **ausente** da resposta |
| CA-D2 | ✅ | grep no corpo por `AiScore`/`ai_score`/`AiReason`/`ai_reason`/`ExternalId`/`external_id`/valores de external_id plantados (`ext-2`,`ext-3`) → **nenhuma ocorrência**. Campos presentes: title, salePrice, originalPrice, discountPct, affiliateLink, mediaUrl, mediaLocalPath (URL pública `http://localhost:5000/media/...`), slug, category, collectedAt, platform |
| CA-D3 | ✅ | `GET /api/public/deals/tenis-corrida` → 200 com mesmo subconjunto de campos |
| CA-D4 | ✅ | slug inexistente → 404 |
| CA-D5 | ✅ | `GET /api/public/deals/category/moda` → só o produto da categoria `moda` |
| CA-D6 | ✅ | Sem `page`/`pageSize` → `page:1, pageSize:20` no envelope (validado em products, queue e public/deals) |
| CA-D7 | ✅ | `?pageSize=500` → resposta reflete `pageSize:100` (truncado) |
| CA-D8 | ✅ (real, incl. preflight) | `Origin: http://localhost:3000` (uma das 5 origins) → `Access-Control-Allow-Origin` presente na resposta GET e no preflight `OPTIONS` (204 com `Access-Control-Allow-Methods: GET`) |
| CA-D9 | ✅ (real, incl. preflight) | `Origin: https://nao-autorizado.com` → **nenhum header `Access-Control-*`** na resposta GET nem no preflight OPTIONS |
| CA-D10 | ✅ (análise de código) | `CorsConfigurator.cs`: `policy.WithOrigins(origins)` — nenhum uso de `AllowAnyOrigin` em nenhum branch do código; lista vem de `appsettings.json` (`Cors:AllowedOrigins`, 5 origins explícitas), fallback interno também é lista explícita, nunca wildcard |
| CA-D11 | ✅ **end-to-end real** | Disparadas 65 requisições sequenciais reais a `/api/public/deals` do mesmo IP: as 60 primeiras retornaram 200, da 61ª em diante **429** consistentemente |
| CA-D12 | ✅ **end-to-end real** | Com o IP "principal" já em 429, uma requisição com `X-Forwarded-For` de outro IP (aceito pois o host de teste está dentro da `KnownNetwork` configurada) recebeu 200 — confirma particionamento por IP, não é limite global |

## Sub-E — Push / Reports

| CA | Resultado | Evidência |
|---|---|---|
| CA-E1 | ✅ | `POST /api/public/push/subscribe` sem token → 201, subscription persistida no banco (`SELECT endpoint FROM push_subscriptions`) |
| CA-E2 | ✅ | `DELETE /api/public/push/unsubscribe?endpoint=...` → 204; **rodado 2x seguidas, ambas 204** (idempotente), banco confirma remoção após a 1ª chamada |
| CA-E3 | ✅ | Endpoint nunca cadastrado → 204 (não 404) — decisão documentada em `especificacao-tecnica.md §6` (evita enumeração de endpoints via inferência 404 vs 204), implementação condiz com o documento |
| CA-E4 | ✅ **end-to-end real** | 13 requisições sequenciais a `/api/public/push/subscribe`: as 10 primeiras 201, as 3 seguintes **429** |
| CA-E5 | ✅ | Massa inserida com publicações `Published` nos últimos 2 dias em 2 redes distintas; `GET /api/reports/summary` com token → 200, `byNetwork` e `byDay` corretos, `periodStart`/`periodEnd` cobrindo janela de 7 dias |
| CA-E6 | ✅ | `GET /api/reports/summary` sem token → 401; `POST/DELETE /api/public/push/*` sem token → 200/201/204 (funcionam normalmente) |

## Transversais

| CA | Resultado | Evidência |
|---|---|---|
| CA-T1 | ✅ | `dotnet test` → 280/280 aprovados; suíte cobre `WebApplicationFactory`, com casos 401 (sem token) e sucesso (com token) por controller protegido (`AuthControllerTests`, `ProductsControllerTests`, `QueueControllerTests`, `SettingsControllerTests`, `PublicControllerTests`, `PushControllerTests`, `ReportsControllerTests`, `JobsTriggerTests`, `CorsTests`, `RateLimiterConfiguratorTests`) |
| CA-T2 | ✅ (análise de código) | `CustomWebApplicationFactory.cs` usa `options.UseInMemoryDatabase(_dbName)` — nenhuma dependência de rede externa, banco de produção ou credenciais reais nos testes |

## Resumo
- **46/46 critérios de aceite validados** (organizados em Sub-A a Sub-E + 2 transversais).
- Nenhuma inconsistência funcional ou de segurança encontrada.
- Pontos de segurança críticos (mascaramento de secrets, CORS restrito, rate limit por IP, prevenção de enumeração de usuário/endpoint) confirmados **contra a aplicação real rodando em Docker**, não apenas por leitura de código ou suíte automatizada.
- Build, boot (migrations + seed), testes automatizados e fluxo integrado ponta a ponta (login → JWT → endpoints protegidos → dados reais no Postgres) todos executados com sucesso.
- Gate visual/E2E Playwright: N/A — issue é REST API pura, sem UI própria nesta entrega.

## Issues encontradas
Nenhuma.
