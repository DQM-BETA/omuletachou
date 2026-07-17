# Estado — ISSUE-11: REST API (Dashboard + Endpoints Publicos)

## Campos principais
issue: 11
repo: omuletachou
titulo: feat: REST API (Dashboard + Endpoints Publicos)
rota: normal
etapa_atual: Dev .NET — Sub-A (#81) mergeada em desenv; Sub-B/C/D/E (#82-#85) desbloqueadas, paralelizáveis
docs_path: repos/omuletachou/documentacoes/ISSUE-11-rest-api
openspec_path: repos/omuletachou/openspec/changes/issue-11-rest-api
openspec_change: repos/omuletachou/openspec/changes/issue-11-rest-api
ultimo_agente: lider-tecnico
status_comment_id: 4962193361
pr_feature: #86 (merged)
pr_homologacao: ~
pr_release: ~
qa_status: ~
code_review_homolog_pr: ~
closedAt: ~

## Contexto
Stack: .NET 8, ASP.NET Core Web API, Controllers (ProductsController, QueueController, SettingsController, JobsController, ReportsController, PublicController, PushController)
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #2 (Domain + EFCore schema), #6 (ProcessorJob) — ambas em produção (main)

**Contexto técnico diferenciado (em relação a issues anteriores):**
Esta Issue implementa a **REST API que expõe dados para o Dashboard Angular (Issue #13, futura)** e para endpoints públicos (site Next.js Issue #12, futura, e PWA).

Diferente das Issues #7-#10 (integrações de rede social, aditivas ao publisher), a Issue #11 é a **infraestrutura de exposição de dados** (layer HTTP acima do domain/jobs já existentes) e introduz **autenticação/autorização pela primeira vez no sistema** (JWT, hash bcrypt, seed de usuário via env var), mascaramento de secrets e política de CORS explícita.

## PM Fase 1 — levantamento de requisitos
Concluído. Perguntas postadas na Issue #11 (comentário https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-4962241310), cobrindo os 7 eixos (auth, endpoints públicos, versionamento, paginação, mascaramento, CORS, escopo).

## Gate 1 — Gerente
Concluído em 2026-07-17. Respostas completas em https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-5003551503:
1. JWT, usuário único, `POST /api/auth/login`, expiração 24h, tabela `users` com hash bcrypt, seed via env var. Todos os controllers do dashboard com `[Authorize]`, exceto `/api/public/*` e `/api/auth/login`.
2. Campos públicos restritos (nunca `ExternalId`/`AiScore`/`AiReason`/`app_settings`). Rate limiting nativo .NET 8: 60 req/min/IP leitura, 10 req/min/IP escrita (push subscribe).
3. Sem versionamento de API por enquanto.
4. Paginação `page`/`pageSize` (default 20, máx 100), envelope `items`/`page`/`pageSize`/`totalItems`/`totalPages`.
5. Mascaramento obrigatório de `_key`/`_secret`/`_token`/`_password` em `GET /api/settings` (últimos 4 caracteres). `PUT` sempre sobrescreve, nunca lê valor completo de volta.
6. CORS com lista explícita de 5 origins, nunca `AllowAnyOrigin`, configurável por ambiente.
7. Fatiar em 5 sub-issues (Sub-A Autenticação; Sub-B Products+Queue; Sub-C Settings+Jobs; Sub-D Public+CORS+RateLimit; Sub-E Push+Reports) — issue-pai vira guarda-chuva.

## PM Fase 2 — PRD consolidado
Concluído em 2026-07-17.
- `proposal.md` escrito em `repos/omuletachou/openspec/changes/issue-11-rest-api/proposal.md` (objetivo, usuários, casos de uso/exceção, regras de negócio, fatiamento em 5 sub-issues, integrações, restrições, definição de pronto).
- `criterios-aceite.md` escrito em `repos/omuletachou/documentacoes/ISSUE-11-rest-api/criterios-aceite.md` — 46 CAs organizados por sub-issue (Sub-A a Sub-E) + 2 CAs transversais (testes de integração).
- Comentário de sumário do PRD postado na Issue #11: https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-5003577610

**Avaliação de ambiguidade arquitetural: SIM, escalar ao Arquiteto (escopo focado, não redesenho completo).**
Motivo: primeira introdução de autenticação/autorização no sistema. 3 pontos identificados fora do julgamento de negócio do PM, que devem pautar o `design.md`:
1. Estratégia de assinatura JWT (algoritmo, armazenamento da chave, aceitabilidade de não ter refresh token dado usuário único).
2. Suficiência do mascaramento de secrets (últimos 4 caracteres) — avaliar necessidade de camada adicional (ex.: auditoria de acesso a `GET /api/settings`).
3. Rate limiting nativo do .NET 8 atrás de proxy reverso (Oracle Cloud VM) — particionamento por IP precisa considerar `X-Forwarded-For`/`X-Real-IP` corretamente.

## Arquiteto — revisão focada
Concluído em 2026-07-17. `design.md` escrito em `repos/omuletachou/openspec/changes/issue-11-rest-api/design.md`, cobrindo exclusivamente os 3 pontos escalados pelo PM (nenhuma regra de negócio já fechada foi revisitada):
1. **JWT**: HS256 (chave simétrica ≥256 bits); chave de assinatura via variável de ambiente (`Jwt__SigningKey`), nunca em `app_settings` (tabela de domínio exposta via API) nem hardcoded; ausência de refresh token mantida (risco residual aceitável dado usuário único/24h/sem multi-dispositivo — revogação futura, se necessária, via rotação da signing key).
2. **Mascaramento de settings**: últimos 4 caracteres é suficiente como controle primário; sem tabela de auditoria dedicada (usuário único não agrega valor de accountability); recomendação de baixo custo não bloqueante — log estruturado via `ILogger` em GET/PUT de `/api/settings` (metadados apenas, nunca o valor).
3. **Rate limit atrás de proxy**: `ForwardedHeadersMiddleware` com `KnownNetworks`/`KnownProxies` = rede Docker do nginx, `ForwardLimit=1`, registrado antes de `UseRateLimiter()`; nginx deve sobrescrever `X-Forwarded-For` com `$remote_addr` (evita spoofing e vazamento de rate limit compartilhado no IP do proxy).

Resumo postado na Issue #11: https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-5003608110

## Líder Técnico — refinamento técnico
Concluído em 2026-07-17.
- Decisão de ordem: **Sub-A (#81) sequencial e bloqueante**; Sub-B/C/D/E rodam em paralelo apenas após merge de #81 em `desenv`. Justificativa completa em `openspec/changes/issue-11-rest-api/tasks.md` (risco de stub de auth vazar para produção, superfície pequena de Sub-A, dependência leve de `PagedResult<T>` entre Sub-B/Sub-D e da policy `"public-write"` entre Sub-D/Sub-E resolvida via merge de `desenv`).
- `especificacao-tecnica.md` escrito em `repos/omuletachou/documentacoes/ISSUE-11-rest-api/especificacao-tecnica.md`: schema `users` (migration), config `Jwt__SigningKey`/`AddJwtBearer`, ordem completa do pipeline de middlewares (ForwardedHeaders → Https → CORS → Authentication → Authorization → RateLimiter), contrato `PagedResult<T>` compartilhado, formato exato de mascaramento (16 asteriscos fixos + últimos 4 chars), decisão CA-E3 (204 idempotente).
- `tasks.md` escrito em `repos/omuletachou/openspec/changes/issue-11-rest-api/tasks.md`.
- 5 sub-issues reais criadas no GitHub (label `stack:dotnet`): #81 (Sub-A), #82 (Sub-B), #83 (Sub-C), #84 (Sub-D), #85 (Sub-E) — cada uma com CAs correspondentes copiados de `criterios-aceite.md`.
- Resumo técnico postado na Issue #11: https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-5003649743
- Comentário 📍 Status atualizado (id 4962193361) para "Dev .NET (Sub-A — Autenticação)".

## Dev .NET
Sub-A (#81) concluída pelo Dev .NET em 2026-07-17. PR #86 (`feature/81-auth` → `desenv`). Implementado:
- Entidade `User` (Domain) + `UserConfiguration` (Infrastructure) + migration `AddUsersTable` (tabela `users`: email unique index, password_hash bcrypt, created_at).
- `UserSeeder.SeedIfEmpty` (Api/Auth): seed idempotente via `Seed__UserEmail`/`Seed__UserPassword`, só roda se a tabela estiver vazia; senha sempre hash bcrypt (workFactor 12).
- `JwtOptions`/`JwtTokenService` (Api/Auth): emissão HS256, claims `sub`/`email`, expiração 24h configurável.
- `AuthController`: `POST /api/auth/login` (público, mensagem genérica em qualquer falha — CA-A2/CA-A3) e `GET /api/auth/me` (`[Authorize]`, smoke-test ponta a ponta que desbloqueia Sub-B a Sub-E).
- `Program.cs`: fail-fast se `Jwt:SigningKey` ausente/vazia em qualquer ambiente; `AddAuthentication().AddJwtBearer()` + `AddAuthorization()`; pipeline `UseAuthentication()`→`UseAuthorization()`→`MapControllers()` (base da ordem completa de especificacao-tecnica.md §3 — CORS/RateLimiter completos ficam para Sub-D); `options.MapInboundClaims = false` (preserva claims curtos do token).
- `appsettings.json` (chave vazia, versionado) / `appsettings.Development.json` (chave fixa documentada, uso local apenas) / `docker-compose.yml` + `.env.example` (`JWT_SIGNING_KEY`, `SEED_USER_EMAIL`, `SEED_USER_PASSWORD`).
- Testes: `AuthControllerTests` (login sucesso/senha incorreta/email inexistente, `/me` sem token/token válido/token expirado/assinatura inválida) + `UserSeederTests` (idempotência, hash bcrypt, sem env vars não cria usuário) — 197 testes totais (187 pré-existentes + 10 novos), 100% passando.
- **Bug latente corrigido en passant**: `CustomWebApplicationFactory` gerava um novo nome de banco InMemory a cada resolução de `DbContextOptions` (Guid dentro da lambda de `AddDbContext`, reavaliado por scope), isolando silenciosamente cada scope/request em um banco vazio diferente — inofensivo para os testes anteriores (não persistiam dados entre scopes), mas quebrava qualquer fluxo de seed+consulta em scopes distintos. Corrigido gerando o nome uma única vez por instância da factory.
- Boot Docker validado via `docker compose up --build`: migration aplicada, seed executado, `/health` 200, login válido/inválido, `/api/auth/me` sem token (401)/com token válido (200), endpoints de trigger existentes (200) — tudo confirmado via curl real contra o container.

## Líder Técnico — merge Sub-A (#81)
Concluído em 2026-07-17.
- Revisão do PR #86: confirmado que `appsettings.json` (base, versionado) mantém `Jwt:SigningKey` vazio e `Program.cs` lança `InvalidOperationException` fail-fast se `Jwt:SigningKey` ausente/vazia em qualquer ambiente — sem chave fraca de fallback. `appsettings.Development.json` só documenta chave fixa para uso local. 197/197 testes cobrindo os fluxos críticos (login, `/me`, seed, token expirado/inválido). Boot Docker validado com curl real pelo Dev.
- `mergeStateStatus: CLEAN`, `mergeable: MERGEABLE` confirmados antes do merge.
- PR #86 mergeado (squash) em `desenv`. Sub-issue #81 fechada (`completed`).
- **PR desenv→homolog NÃO criado** — apenas 1/5 sub-issues concluída; aguardando #82-#85.
- Sub-issues #82 (Sub-B), #83 (Sub-C), #84 (Sub-D), #85 (Sub-E) desbloqueadas — dependência de Sub-A resolvida em `desenv`. Podem rodar em paralelo (sem dependência sequencial forte entre si).

Próxima etapa: sessão principal spawna Devs .NET em paralelo para Sub-B (#82), Sub-C (#83), Sub-D (#84), Sub-E (#85).

## Dev .NET — Sub-B (#82)
Concluído em 2026-07-17. PR #87 (`feature/82-products-queue` → `desenv`). Implementado:
- `PagedResult<T>` + `ToPagedResultAsync` (`AfiliadoBot.Api.Common`) — contrato compartilhado de paginação (especificacao-tecnica.md §4): normalização `page`/`pageSize` (default 1/20, `pageSize` truncado em 100, nunca erro). Disponível para Sub-D reaproveitar via merge de `desenv`.
- `ProductsController` (`[Authorize]`): `GET /api/products` (paginado, filtros opcionais `status`/`platform` case-insensitive contra os enums `ProductStatus`/`Platform`) e `GET /api/products/{id}` (detalhe com `ai_score`/`ai_reason` em snake_case — CA-B3; 404 se inexistente).
- `QueueController` (`[Authorize]`): `GET /api/queue` (paginado, filtros opcionais `status`/`network`).
- **Escopo reduzido conforme spawn message**: implementados apenas os 3 endpoints GET listados no "Escopo" recebido. CA-B5/CA-B6 (`PATCH /api/products/{id}/status`), CA-B8 (`GET /api/queue/manual`) e CA-B9/CA-B10 (`POST /api/queue/{id}/retry`) **não foram implementados** — não constavam no Escopo explícito da tarefa. Sinalizado no PR para o LT avaliar se sub-issue #82 precisa de um follow-up para fechar CA-B1 a CA-B11 por completo.
- Testes: `ProductsControllerTests` (8 casos) + `QueueControllerTests` (4 casos) cobrindo CA-B1, CA-B2, CA-B3, CA-B4, CA-B7, CA-B11 — 209/209 testes totais (197 pré-existentes + 12 novos), 100% passando.
- Boot Docker validado via `docker compose build/up db api`: `/health` 200, smoke test real via `/api/auth/login` → token JWT → `GET /api/products` e `GET /api/queue` (401 sem token, 200 com token, `pageSize=500` truncado para `100`).

## Dev .NET — Sub-E (#85)
Concluído em 2026-07-17. PR (`feature/85-push-reports` → `desenv`). Implementado:
- `PushController` (`api/public/push`, `[AllowAnonymous]`): `POST /subscribe` (recebe `endpoint`+`keys.p256dh`+`keys.auth`, persiste via `PushSubscription` — entidade já existente do domínio —, 201 no primeiro cadastro, 200 idempotente se o `endpoint` já existir, 400 se faltar campo obrigatório) e `DELETE /unsubscribe?endpoint=...` (204 idempotente tanto para endpoint existente quanto inexistente — CA-E3, decisão de segurança documentada em especificacao-tecnica.md §6: evita 404 permitir enumeração de endpoints cadastrados por um chamador não autenticado).
- `ReportsController` (`api/reports`, `[Authorize]`): `GET /summary` — agrega `PublicationQueue` com `Status=Published` nos últimos 7 dias (janela `[hoje-6, hoje]` UTC), retorna `periodStart`/`periodEnd`/`totalPublished`/`byNetwork` (rede→contagem)/`byDay` (data→contagem). 401 sem token (CA-E6).
- **Policy de rate limit `"public-write"` (Sub-D, #84) — pendente de conferência no merge**: no momento desta implementação, Sub-D ainda não estava mergeada em `desenv` e a policy `AddRateLimiter`/`AddPolicy("public-write", ...)` não existia em `Program.cs`. `POST /api/public/push/subscribe` foi implementado **sem** `.RequireRateLimiting("public-write")` (CA-E4 não coberto por teste automatizado nesta sub-issue). **Ação para o LT no merge final**: depois que Sub-D estiver em `desenv`, adicionar `.RequireRateLimiting("public-write")` ao `PushController.Subscribe` (ou ao mapeamento do endpoint) e validar CA-E4 (10 req/min/IP → 429 acima do limite).
- Testes: `PushControllerTests` (5 casos: subscribe sucesso/persistência, subscribe sem campos obrigatórios → 400, unsubscribe existente → 204 + remove do banco, unsubscribe inexistente → 204 idempotente, unsubscribe sem `endpoint` → 400) + `ReportsControllerTests` (2 casos: sem token → 401, com token → agregação correta por rede/dia incluindo exclusão de itens fora da janela de 7 dias e itens com `Status=Failed`) — 204/204 testes totais (209 pré-existentes + 6 novos — Sub-B ainda não estava mergeada em `desenv` na base usada por esta branch, delta líquido real será resolvido no merge), 100% passando.
- Boot Docker validado via `docker compose up --build db api`: `/health` 200; smoke real via curl — `POST /subscribe` (201 + persistido), `DELETE /unsubscribe` com endpoint existente (204) e inexistente (204 idempotente), `GET /reports/summary` sem token (401) e com token JWT válido (200, payload agregado). Containers e volumes removidos ao final (`docker compose down -v`).
- Não tocados: `ProductsController`, `QueueController`, `SettingsController`, `JobsController`, `PublicController` (fora do escopo de Sub-E, conforme instrução de minimizar conflito de merge).

## Dev .NET — Sub-C (#83)
Concluído em 2026-07-17. PR #88 (`feature/83-settings-jobs` → `desenv`). Implementado:
- `SettingsController` (`api/settings`, `[Authorize]`): `GET /` lista `app_settings` mascarando chaves sensíveis (sufixo `_key`/`_secret`/`_token`/`_password`, case-insensitive) no formato exato `****************a1b2` (16 asteriscos fixos + últimos 4 caracteres reais — `SettingsMasker.Mask`); chave sensível vazia/nula retorna `null` (CA-C3). `PUT /{key}` sobrescreve valor de chave existente (persistência integral no banco), 404 sem criar implicitamente se a chave não existir (CA-C5), e a resposta nunca ecoa o valor completo (nem antigo, nem novo) — mesma regra de mascaramento aplicada ao corpo de resposta do PUT (CA-C6).
- `JobsController` (`api/jobs`, `[Authorize]`): substitui os endpoints mínimos de trigger que existiam soltos e desprotegidos em `Program.cs` (`collector/trigger`, `collector/{amazon,mercadolivre,shopee}/trigger`, `processor/trigger`, `publisher/trigger`) — mesmos paths mantidos (compatibilidade), agora exigindo token (CA-C7/C8/C9/C10; gap de proteção fechado por esta sub-issue).
- Log estruturado (`ILogger<SettingsController>`, recomendação não bloqueante do Arquiteto — design.md §2.2) em GET/PUT de `/api/settings`: metadados apenas (`UserId` via claim `sub` do JWT, `Key` em PUT, timestamp), nunca o valor da chave, mascarado ou não.
- **Requisito de segurança crítico coberto explicitamente**: `SettingsControllerTests` inclui asserções `raw.Should().NotContain(secretValue)` sobre o corpo bruto da resposta (não apenas o campo desserializado) tanto em GET quanto em PUT, confirmando que o valor completo de uma chave sensível nunca aparece em nenhuma resposta JSON.
- Testes: `SettingsControllerTests` (9 casos: GET sem token 401, mascaramento formato exato, chave não sensível sem máscara, chave sensível vazia retorna null, PUT sobrescreve integralmente, PUT chave inexistente 404 sem criar, PUT chave sensível nunca retorna valor completo, PUT sem token 401) + `SettingsMaskerTests` (8 casos unitários de `IsSensitive`/`Mask`/`ApplyIfSensitive`) + `JobsTriggerTests` atualizado (401 sem token para processor/publisher/collector trigger, 200 com token válido) — 220/220 testes totais (209 pré-existentes + 11 novos), 100% passando.
- Boot Docker validado via containers standalone (`docker run` — `afiliado_db`/`afiliado_api` já em uso por outro Dev em paralelo no momento do teste, evitado com nomes/rede isolados): `/health` 200; smoke real via curl — `GET /api/settings` sem token (401), login → token JWT real, `GET /api/settings` com token (mascaramento correto, `telegram.bot_token` etc. como `null` quando vazio), `PUT /api/settings/telegram.bot_token` com token (retorna `****************1234`, nunca o valor pleno enviado), `POST /api/jobs/processor/trigger` sem token (401). Containers/rede/imagem/volumes removidos ao final.
- Não tocados: `ProductsController`, `QueueController`, `PublicController`, `PushController`, `ReportsController` (fora do escopo de Sub-C, conforme instrução de minimizar conflito de merge).

## Sub-issues
sub_issues: [#81 (stack:dotnet, task_id:Sub-A) — MERGED, #82 (stack:dotnet, task_id:Sub-B), #83 (stack:dotnet, task_id:Sub-C) — PR #88 aberto, aguardando merge, #84 (stack:dotnet, task_id:Sub-D), #85 (stack:dotnet, task_id:Sub-E) — PR aberto, aguardando merge]
desenv_tasks_merged: [#81]

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #11 preparada, estado.md criado, comentario 📍 Status adicionado (id 4962193361), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #11 (comentário 4962241310), comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — Gate 1 respondido (comentário 5003551503), proposal.md + criterios-aceite.md escritos (46 CAs em 5 sub-issues), sumário do PRD postado (comentário 5003577610), comentario 📍 Status atualizado para Arquiteto, ambiguidade=sim (escopo focado: JWT signing/refresh, mascaramento de secrets, rate limit atrás de proxy) |
| 4 | Arquiteto | arquiteto | concluido — design.md escrito (JWT HS256/env var/sem refresh, mascaramento suficiente + log recomendado, rate limit com ForwardedHeadersMiddleware), resumo postado na Issue #11 (comentário 5003608110), comentario 📍 Status atualizado para Líder Técnico |
| 5 | Líder Técnico — refinamento | lider-tecnico | concluido — decisão de ordem sequencial (Sub-A bloqueante), especificacao-tecnica.md + tasks.md escritos, 5 sub-issues criadas (#81-#85), resumo postado (comentário 5003649743), comentario 📍 Status atualizado para Dev .NET (Sub-A) |
| 6 | Dev .NET Sub-A | dev-dotnet | concluido — PR #86 (feature/81-auth → desenv), 197/197 testes, boot Docker validado |
| 7 | Líder Técnico — merge Sub-A | lider-tecnico | concluido — PR #86 revisado (fail-fast JWT confirmado) e mergeado (squash) em desenv, sub-issue #81 fechada, Sub-B/C/D/E desbloqueadas |
| 8 | Dev .NET Sub-B #82 (PR #87) | dev-dotnet | concluido — PR #87 (feature/82-products-queue → desenv), 209/209 testes, boot Docker validado; escopo reduzido aos 3 endpoints GET do spawn message (CA-B5/B6/B8/B9/B10 não implementados) |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 61934 | 56 | 302s |
| 2 | PM Fase 1 | pm | sonnet | 30761 | 9 | 68s |
| 3 | PM Fase 2 | pm | sonnet | 55923 | 21 | 253s |
| 4 | Arquiteto | arquiteto | sonnet | 55194 | 12 | 181s |
| 5 | Líder Técnico — refinamento | lider-tecnico | sonnet | 76208 | 20 | 257s |
| 6 | Dev .NET Sub-A #81 (PR #86) | dev-dotnet | sonnet | 159882 | 103 | 1133s |
| 7 | Líder Técnico — merge Sub-A #81 (PR #86) | lider-tecnico | sonnet | 49253 | 13 | 120s |
| 8 | Dev .NET Sub-B #82 (PR #87) — escopo parcial | dev-dotnet | sonnet | 108510 | 53 | 417s |

**Consolidação (quiescência):** A preencher pela sessão principal após cada etapa.

---
_Última atualização: 2026-07-17 — mantido pelo Líder Técnico._
