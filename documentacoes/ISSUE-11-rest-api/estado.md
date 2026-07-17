# Estado — ISSUE-11: REST API (Dashboard + Endpoints Publicos)

## Campos principais
issue: 11
repo: omuletachou
titulo: feat: REST API (Dashboard + Endpoints Publicos)
rota: normal
etapa_atual: Dev .NET — Sub-A (#81) bloqueante primeiro; Sub-B/C/D/E (#82-#85) paralelizáveis após merge de #81
docs_path: repos/omuletachou/documentacoes/ISSUE-11-rest-api
openspec_path: repos/omuletachou/openspec/changes/issue-11-rest-api
openspec_change: repos/omuletachou/openspec/changes/issue-11-rest-api
ultimo_agente: lider-tecnico
status_comment_id: 4962193361
pr_feature: ~
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
Sub-A (#81) concluída pelo Dev .NET em 2026-07-17. PR feature/81-auth → desenv aberto (aguardando número/link do `gh pr create`, ver HANDOFF). Implementado:
- Entidade `User` (Domain) + `UserConfiguration` (Infrastructure) + migration `AddUsersTable` (tabela `users`: email unique index, password_hash bcrypt, created_at).
- `UserSeeder.SeedIfEmpty` (Api/Auth): seed idempotente via `Seed__UserEmail`/`Seed__UserPassword`, só roda se a tabela estiver vazia; senha sempre hash bcrypt (workFactor 12).
- `JwtOptions`/`JwtTokenService` (Api/Auth): emissão HS256, claims `sub`/`email`, expiração 24h configurável.
- `AuthController`: `POST /api/auth/login` (público, mensagem genérica em qualquer falha — CA-A2/CA-A3) e `GET /api/auth/me` (`[Authorize]`, smoke-test ponta a ponta que desbloqueia Sub-B a Sub-E).
- `Program.cs`: fail-fast se `Jwt:SigningKey` ausente/vazia em qualquer ambiente; `AddAuthentication().AddJwtBearer()` + `AddAuthorization()`; pipeline `UseAuthentication()`→`UseAuthorization()`→`MapControllers()` (base da ordem completa de especificacao-tecnica.md §3 — CORS/RateLimiter completos ficam para Sub-D); `options.MapInboundClaims = false` (preserva claims curtos do token).
- `appsettings.json` (chave vazia, versionado) / `appsettings.Development.json` (chave fixa documentada, uso local apenas) / `docker-compose.yml` + `.env.example` (`JWT_SIGNING_KEY`, `SEED_USER_EMAIL`, `SEED_USER_PASSWORD`).
- Testes: `AuthControllerTests` (login sucesso/senha incorreta/email inexistente, `/me` sem token/token válido/token expirado/assinatura inválida) + `UserSeederTests` (idempotência, hash bcrypt, sem env vars não cria usuário) — 197 testes totais (187 pré-existentes + 10 novos), 100% passando.
- **Bug latente corrigido en passant**: `CustomWebApplicationFactory` gerava um novo nome de banco InMemory a cada resolução de `DbContextOptions` (Guid dentro da lambda de `AddDbContext`, reavaliado por scope), isolando silenciosamente cada scope/request em um banco vazio diferente — inofensivo para os testes anteriores (não persistiam dados entre scopes), mas quebrava qualquer fluxo de seed+consulta em scopes distintos. Corrigido gerando o nome uma única vez por instância da factory.
- Boot Docker validado via `docker compose up --build`: migration aplicada, seed executado, `/health` 200, login válido/inválido, `/api/auth/me` sem token (401)/com token válido (200), endpoints de trigger existentes (200) — tudo confirmado via curl real contra o container.

Próxima etapa: Líder Técnico — merge de #81 (feature/81-auth) em `desenv` ANTES de qualquer outra sub-issue. Após confirmado, spawnar Devs em paralelo para Sub-B (#82), Sub-C (#83), Sub-D (#84), Sub-E (#85) — ordem entre estas quatro é livre (sem dependência sequencial forte).

## Sub-issues
sub_issues: [#81 (stack:dotnet, task_id:Sub-A), #82 (stack:dotnet, task_id:Sub-B), #83 (stack:dotnet, task_id:Sub-C), #84 (stack:dotnet, task_id:Sub-D), #85 (stack:dotnet, task_id:Sub-E)]
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #11 preparada, estado.md criado, comentario 📍 Status adicionado (id 4962193361), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #11 (comentário 4962241310), comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — Gate 1 respondido (comentário 5003551503), proposal.md + criterios-aceite.md escritos (46 CAs em 5 sub-issues), sumário do PRD postado (comentário 5003577610), comentario 📍 Status atualizado para Arquiteto, ambiguidade=sim (escopo focado: JWT signing/refresh, mascaramento de secrets, rate limit atrás de proxy) |
| 4 | Arquiteto | arquiteto | concluido — design.md escrito (JWT HS256/env var/sem refresh, mascaramento suficiente + log recomendado, rate limit com ForwardedHeadersMiddleware), resumo postado na Issue #11 (comentário 5003608110), comentario 📍 Status atualizado para Líder Técnico |
| 5 | Líder Técnico — refinamento | lider-tecnico | concluido — decisão de ordem sequencial (Sub-A bloqueante), especificacao-tecnica.md + tasks.md escritos, 5 sub-issues criadas (#81-#85), resumo postado (comentário 5003649743), comentario 📍 Status atualizado para Dev .NET (Sub-A) |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 61934 | 56 | 302s |
| 2 | PM Fase 1 | pm | sonnet | 30761 | 9 | 68s |
| 3 | PM Fase 2 | pm | sonnet | 55923 | 21 | 253s |
| 4 | Arquiteto | arquiteto | sonnet | 55194 | 12 | 181s |
| 5 | Líder Técnico — refinamento | lider-tecnico | sonnet | 76208 | 20 | 257s |

**Consolidação (quiescência):** A preencher pela sessão principal após cada etapa.

---
_Última atualização: 2026-07-17 — mantido pelo Líder Técnico._
