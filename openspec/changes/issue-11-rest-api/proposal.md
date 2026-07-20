# Proposal — ISSUE-11: REST API (Dashboard + Endpoints Públicos)

## Objetivo
Implementar a camada HTTP (ASP.NET Core 8 Web API) que expõe os dados do domínio já existente (Issues #2, #6) para dois tipos de consumidor: o Dashboard Angular interno (protegido, autenticação JWT) e o site Next.js/PWA públicos (sem autenticação). Inclui autenticação própria (login + JWT), CRUD/consulta dos recursos administrativos (Products, Queue, Settings, Jobs, Reports), endpoints públicos com subconjunto restrito de campos, rate limiting nativo do .NET 8, CORS explícito por lista de origins e mascaramento obrigatório de credenciais sensíveis em `Settings`.

Esta issue-pai atua como guarda-chuva; a implementação é fatiada em 5 sub-issues (ver "Escopo — fatiamento em sub-issues").

## Usuários afetados
- Operador único do sistema (usuário do Dashboard Angular) — autentica via JWT, gerencia produtos, fila de publicação, configurações e dispara jobs manualmente.
- Visitantes do site Next.js e da PWA — consomem `/api/public/deals*` e se inscrevem em push notifications, sem autenticação.
- Dashboard Angular (Issue #13, futura) e site Next.js (Issue #12, futura) — consumidores diretos desta API; decisões de contrato aqui impactam ambos.
- Indiretamente, segurança/compliance do sistema — primeira introdução de autenticação, hash de senha e exposição controlada de secrets nesta API.

## Casos de uso principais
1. Operador chama `POST /api/auth/login` com email/senha → API valida hash bcrypt contra a tabela `users` → retorna JWT válido por 24h → operador usa o token (`Authorization: Bearer`) em todas as chamadas subsequentes aos controllers protegidos.
2. Operador lista/filtra produtos (`GET /api/products?status=&platform=&page=&pageSize=`), vê detalhe com `ai_score`/`ai_reason` (`GET /api/products/{id}`), e aprova/rejeita (`PATCH /api/products/{id}/status`).
3. Operador lista/filtra a fila de publicação (`GET /api/queue`), vê itens pendentes de aprovação manual do Facebook (`GET /api/queue/manual`) e reprocessa itens com falha (`POST /api/queue/{id}/retry`).
4. Operador consulta configurações do sistema (`GET /api/settings`, secrets sempre mascarados) e atualiza uma configuração (`PUT /api/settings/{key}`).
5. Operador dispara manualmente collector/processor/publisher (`POST /api/jobs/{job}/trigger`) e consulta o resumo de publicações dos últimos 7 dias (`GET /api/reports/summary`).
6. Visitante do site/PWA consome `GET /api/public/deals` (paginado, ordenado por `ai_score` desc), `GET /api/public/deals/{slug}` e `GET /api/public/deals/category/{category}` sem autenticação, recebendo apenas o subconjunto de campos autorizado.
7. Visitante se inscreve/desinscreve de push notifications (`POST /api/public/push/subscribe`, `DELETE /api/public/push/unsubscribe`) sem autenticação, sujeito a rate limit mais restritivo (endpoint de escrita).

## Casos de exceção
1. **Login com credenciais inválidas**: `POST /api/auth/login` retorna 401, sem detalhar se o erro foi email ou senha (evitar enumeração de usuário).
2. **Chamada a controller protegido sem token / token expirado / token inválido**: middleware `[Authorize]` retorna 401 antes de a requisição chegar ao controller.
3. **`PATCH /api/products/{id}/status` com valor de status fora do enum permitido** (`rejected`/`pending`): retorna 400 com mensagem de validação.
4. **`PUT /api/settings/{key}` para chave inexistente**: retorna 404 (não cria chave nova implicitamente).
5. **Rate limit excedido** (60 req/min/IP em leitura pública, 10 req/min/IP em `push/subscribe`): retorna 429, sem processar a requisição.
6. **Requisição a `/api/public/*` de origin fora da lista CORS explícita**: navegador bloqueia via CORS (preflight falha); chamadas server-to-server sem `Origin` não são afetadas pela política CORS (limitação conhecida do padrão CORS, mitigada pelo rate limiting).
7. **`page`/`pageSize` fora dos limites**: `page < 1` tratado como 1; `pageSize > 100` truncado para 100; `pageSize < 1` tratado como o padrão (20).
8. **`GET /api/settings` com chave sensível vazia/não configurada**: retorna indicador de "não configurado" (não aplica máscara sobre string vazia).

## Regras de negócio
- **Autenticação**: JWT assinado (chave de assinatura em variável de ambiente/`appsettings`, nunca hardcoded), expiração fixa de 24h, sem refresh token nesta fase (reautenticação manual após expirar). Usuário único (operador), sem papéis/multi-tenant. Seed do usuário via variável de ambiente no primeiro deploy; senha sempre armazenada como hash bcrypt, nunca em texto plano.
- **Autorização**: todos os controllers do dashboard (`Products`, `Queue`, `Settings`, `Jobs`, `Reports`) exigem `[Authorize]`. Exceções explícitas: `POST /api/auth/login` e todo o namespace `/api/public/*` (incluindo push).
- **Campos públicos permitidos** em `/api/public/deals*`: `Title`, `SalePrice`, `OriginalPrice`, `DiscountPct`, `AffiliateLink`, `MediaUrl`, `MediaLocalPath` (exposto como URL pública), `Slug`, `Category`, `CollectedAt`, opcionalmente `Platform`. **Nunca expor**: `ExternalId`, `AiScore`, `AiReason`, nem qualquer campo de `app_settings`.
- **Rate limiting** via `RateLimiter` nativo do .NET 8 (`AddRateLimiter`/`UseRateLimiter`), particionado por IP: 60 req/min em endpoints de leitura pública, 10 req/min em `push/subscribe` (escrita).
- **Paginação padrão** em todos os endpoints de listagem (`/api/products`, `/api/queue`, `/api/public/deals*`): `page` (padrão 1), `pageSize` (padrão 20, máximo 100, valores acima truncados). Envelope de resposta padronizado: `{ items, page, pageSize, totalItems, totalPages }`.
- **Mascaramento de credenciais**: em `GET /api/settings`, qualquer chave cujo nome termine em `_key`, `_secret`, `_token` ou `_password` nunca retorna o valor puro — exibe apenas os últimos 4 caracteres no formato `****************a1b2`. `PUT /api/settings/{key}` sempre sobrescreve o valor completo; a API nunca devolve o valor completo de volta em nenhuma resposta.
- **Sem versionamento de API** nesta fase (`/api/...` sem prefixo `/v1/`) — decisão explícita do Gerente, revisitar quando houver necessidade real de compatibilidade com cliente antigo.
- **CORS**: lista explícita de 5 origins (`https://omuletachou.com.br`, `https://www.omuletachou.com.br`, `https://dashboard.omuletachou.com.br`, `http://localhost:3000`, `http://localhost:4200`), configurável por ambiente via `appsettings.json`. Nunca usar `AllowAnyOrigin`, nem em desenvolvimento.

## Escopo — fatiamento em sub-issues
Definido pelo Gerente no Gate 1. Cada sub-issue tem CAs próprios em `criterios-aceite.md`:
- **Sub-A — Autenticação**: tabela `users`, hash bcrypt, seed via env var, `POST /api/auth/login`, geração/validação de JWT, middleware `[Authorize]` aplicado aos demais controllers.
- **Sub-B — Products + Queue**: `ProductsController` (list/detail/patch status), `QueueController` (list/manual/retry).
- **Sub-C — Settings + Jobs**: `SettingsController` (list mascarado/update), `JobsController` (trigger collector/processor/publisher).
- **Sub-D — Public + CORS + RateLimit**: `PublicController` (deals/slug/category), política de CORS explícita, configuração de `RateLimiter`.
- **Sub-E — Push + Reports**: `PushController` (subscribe/unsubscribe), `ReportsController` (summary).

Dependência de ordem: Sub-A deve ser concluída (ou pelo menos o middleware `[Authorize]` disponível) antes de Sub-B/Sub-C serem consideradas "prontas" em ambiente protegido — LT deve avaliar se sub-issues podem rodar em paralelo com stub de autenticação ou se há dependência sequencial real.

## Integrações externas
Nenhuma integração de rede externa nova. Esta issue consome exclusivamente o domínio/EF Core já existente (Issues #2, #6). Sem chamadas a APIs de terceiros.

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Stack fixa: ASP.NET Core 8 Web API, EF Core 8, PostgreSQL 16 (sem ambiguidade de stack).
- Primeira introdução de autenticação/autorização no sistema — tratar com rigor de segurança (chave de assinatura JWT fora de código-fonte, hash bcrypt, nunca logar secrets).
- Avaliação de ambiguidade arquitetural feita pelo PM (ver `estado.md`): decisão sobre escalar ou não ao Arquiteto documentada na Fase 2.

## Definição de pronto
- Todos os controllers das 5 sub-issues implementados e registrados no DI, com `[Authorize]` aplicado corretamente (exceto `/api/auth/login` e `/api/public/*`).
- Testes de integração com `WebApplicationFactory` cobrindo os endpoints principais (autenticados e públicos), incluindo caso de 401 sem token e caso de sucesso com token válido.
- Mascaramento de credenciais coberto por teste (chave sensível nunca retorna valor puro).
- Rate limiting coberto por teste (limite excedido retorna 429).
- CORS coberto por teste ou validação manual (origin fora da lista é bloqueada; origins da lista funcionam).
- Paginação coberta por teste (truncamento de `pageSize`, envelope de resposta correto).
- `dotnet test` verde, sem chamadas reais a serviços externos (não aplicável aqui — sem integrações externas nesta issue).
