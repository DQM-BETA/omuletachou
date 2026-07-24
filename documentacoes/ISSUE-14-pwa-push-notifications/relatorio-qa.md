# Relatório QA — Issue #14 (PWA + Push Notifications)

**Status: APROVADO** (reexecução após fix do achado de upsert)

## Rodada 1 (REPROVADA) — histórico preservado

PR validado: #120 (`desenv` → `homolog`), commit `de37c66` confirmado em `homolog` via
`git log --oneline -5` após `git fetch origin && git checkout homolog && git pull origin homolog`.

### Suítes automatizadas (a partir de `homolog`)
| Suíte | Resultado |
|---|---|
| `dotnet test` (backend) | 305/305 passando (100%), 24s |
| `npm test -- --ci` (frontend `website/`) | 79/79 passando (100%), 14 suites, 3.26s |

### Ambiente integrado
`docker compose up -d --build db api website` a partir de `homolog` — subiu sem exceção:
migrations aplicadas (incl. `SeedPushVapidKeys`), Hangfire iniciado, Next.js
`Ready in 78ms`. Ambiente derrubado ao final com `docker compose down -v`.

Não há suíte Playwright / `test:visual` neste repositório (confirmado: nenhum script
`test:visual` em `website/package.json`, nenhum `playwright.config.*` no repo) —
**E2E/screenshots: N/A** (critério de decisão: ausência do script `test:visual`, não
julgamento de plataforma). Gate visual de screenshots não se aplica.

### Critérios de aceite — veredito (rodada 1)

| # | Critério (Given/When/Then resumido) | Veredito | Evidência |
|---|---|---|---|
| 1 | manifest.json instalável (name, short_name, display, theme_color, background_color, ícones) | ✅ OK | `curl http://localhost:3000/manifest.json` → 200, todos os campos conferem exatamente (`O Mulet Achou`, `Mulet Achou`, `standalone`, `#e63946`, `#ffffff`, ícones 192/512 200) |
| 2 | `npm run build` gera `/public/sw.js` sem erros | ✅ OK | Build Docker do `website` (que roda `npm run build`) sucesso; `curl http://localhost:3000/sw.js` → 200 |
| 3 | Fallback sem HTTPS: página funciona, SW não registra | ✅ OK (por inspeção + teste unitário, não ao vivo — conforme instrução da tarefa) | `website/lib/push.ts` `isPushSupported()` checa `window.isSecureContext`, retorna `null` gracioso; coberto por `push.test.ts` (incluído nos 79/79) |
| 4 | Subscribe cria registro em `push_subscriptions` | ✅ OK | `POST /api/public/push/subscribe` com payload real → 201, linha real persistida no Postgres (confirmado via `psql`) |
| 5 | **Resubscribe do mesmo endpoint: upsert silencioso, `created_at` renovado, nunca 409** | ❌ **REPROVADO** | 2ª chamada ao mesmo `endpoint` com `p256dh`/`auth` diferentes retorna 200 (não 409, essa parte OK) mas **não atualiza** `p256dh`/`auth`/`created_at` no banco — linha permanece com os valores da 1ª chamada. Ver detalhe abaixo |
| 6 | Rate-limit 429 acima de 10 req/min/IP em `/subscribe` | ✅ OK | 15 requisições em sequência rápida → 429 retornado após exceder o limite |
| 7 | Unsubscribe remove o registro | ✅ OK | `DELETE /api/public/push/unsubscribe?endpoint=...` → 204, `count(*)` no Postgres = 0 após a chamada; idempotente (204 também para endpoint inexistente) |
| 8 | Push individual (1 produto): título/corpo/icon/image/data.url | ✅ OK (testes automatizados + inspeção; sem Telegram real disponível neste ambiente) | `PublisherJobTests` (5 casos, EF InMemory real) + inspeção de `PushNotificationService.SendIndividualAsync` — payload confere exatamente com o critério |
| 9 | Push consolidada (>1 produto): 1 notificação única | ✅ OK (idem acima) | `PublisherJobTests` + `PushNotificationService.SendConsolidatedAsync` |
| 10 | 410 Gone remove subscription automaticamente, sem interromper o lote | ✅ OK | `PushNotificationServiceTests` (9 casos incluindo 410 Gone) + inspeção de `SendToAllAsync` (linhas 112-118) — `catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)` remove e segue o loop |
| 11 | `GET vapid-public-key` retorna chave em claro | ✅ OK | Par de VAPID keys real gerado via `webpush generate-vapid-keys`, cadastrado via SQL em `app_settings`; `GET /api/public/push/vapid-public-key` → chave em claro |
| 12 | `GET /api/settings` autenticado retorna a mesma chave mascarada (sem vazamento) | ✅ OK | Login com usuário seed de teste, `GET /api/settings` → `push.vapid_public_key` e `push.vapid_private_key` retornam mascarados (`****...`), mesma chave do item 11 |
| 13 | Migration `push_subscriptions` (schema) | ✅ OK (já validado em Code Review; confirmado por herança — schema já existia via `InitialSchema` #56) | Registro em `estado.md` (achado do Dev Sub-A), sem necessidade de nova validação — inalterado desde o Code Review |

### Achado que reprovou a rodada 1

**`POST /api/public/push/subscribe` não atualizava o registro existente no resubscribe.**

Código à época: `backend/src/AfiliadoBot.Api/Controllers/PushController.cs`, linhas 46-54 —
early-return puro no branch `existing is not null`, nunca tocava
`P256dh`/`Auth`/`CreatedAt`, nunca chamava `SaveChangesAsync` nesse branch.

Reproduzido ao vivo contra Postgres real (ambiente Docker subido pelo QA):
1. `POST /subscribe` com `endpoint=X`, `p256dh=testp256dh-v1`, `auth=testauth-v1` → 201,
   linha criada com esses valores.
2. `POST /subscribe` novamente com o mesmo `endpoint=X`, mas `p256dh=testp256dh-v2-NEW`,
   `auth=testauth-v2-NEW` → 200 (correto, não retorna 409).
3. Consulta direta no Postgres: a linha permanecia com `p256dh=testp256dh-v1`,
   `auth=testauth-v1` e `created_at` original — os novos valores da 2ª chamada nunca
   eram persistidos.

Detalhes completos do impacto funcional e da nota de contexto: ver `estado.md`, seção
"QA — homolog" (histórico).

---

## Rodada 2 — REEXECUÇÃO (APROVADA)

PR validado: #122 (`desenv` → `homolog`), merge commit `35e7bd1abd7f11e2370c020b7d39d93528191915`
confirmado como HEAD de `homolog` via `git fetch origin && git checkout homolog && git pull
origin homolog` + `git log --oneline -5`.

### Suítes automatizadas (a partir de `homolog`, commit `35e7bd1`)
| Suíte | Resultado |
|---|---|
| `dotnet test` (backend) | **306/306 passando** (100%), 24s — o teste novo de regressão eleva o total de 305 para 306 |
| `npm test -- --ci` (frontend `website/`) | **79/79 passando** (100%), 14 suites, 3.63s — sem regressão colateral do fix backend (esperado, sem tocar frontend) |

### Fix reexecutado — leitura de código
`backend/src/AfiliadoBot.Api/Controllers/PushController.cs`, método `Subscribe`, branch
`existing is not null` (linhas 49-59): agora chama `existing.Renew(request.Keys.P256dh,
request.Keys.Auth)` seguido de `await _db.SaveChangesAsync(ct)` antes do `return Ok(new {
id = existing.Id })`. `PushSubscription.Renew(string p256dh, string auth)` (novo método de
domínio) atualiza `P256dh`/`Auth`/`CreatedAt = DateTime.UtcNow`, setters seguem `private`.

### Validação ao vivo — critério #5 (upsert), boot Docker real + `psql`
`docker compose up -d --build db api` a partir de `homolog` (commit `35e7bd1`) — build sem
erros, `afiliado_db`/`afiliado_api` subiram, migrations aplicadas, Hangfire iniciado,
`Application started`, `Now listening on: http://[::]:8080` (mapeado para `localhost:5000`
no host).

Reprodução exata do cenário que reprovou a rodada 1, contra Postgres real (endpoint
`https://fcm.googleapis.com/fcm/send/qa-reexec-1784902129`):
1. `POST /api/public/push/subscribe` (endpoint novo, `p256dh=qa-p256dh-v1`,
   `auth=qa-auth-v1`) → **201**, `{"id":"8e742537-0d53-4c6e-8f37-f2627750c2be"}`.
2. `POST /api/public/push/subscribe` no MESMO endpoint, `p256dh=qa-p256dh-v2-NEW`,
   `auth=qa-auth-v2-NEW` → **200** (nunca 409), **mesmo `id`** retornado
   (`8e742537-0d53-4c6e-8f37-f2627750c2be`) — confirma que não houve tentativa de criar
   linha duplicada.
3. `psql` direto no container `afiliado_db` (`SELECT endpoint, p256dh, auth, created_at FROM
   push_subscriptions WHERE endpoint = '...'`) → **exatamente 1 linha** (`count(*) = 1`) para
   o endpoint, com `p256dh=qa-p256dh-v2-NEW`, `auth=qa-auth-v2-NEW` (valores da 2ª chamada,
   não da 1ª) e `created_at = 2026-07-24 14:08:50.344886+00` (renovado, no instante da 2ª
   chamada — não o timestamp da 1ª chamada). **Comportamento agora bate exatamente com
   `criterios-aceite.md`.**

### Revalidação colateral (regressão) — demais endpoints do `PushController`
Executada no mesmo ambiente Docker acima, contra Postgres real:
| Endpoint | Resultado | Evidência |
|---|---|---|
| `GET /api/public/push/vapid-public-key` | ✅ OK | `200`, `{"publicKey":null}` — esperado: seed da migration deixa a chave vazia por padrão (cadastro manual do operador, per PRD); comportamento em claro/cadastrado já validado ao vivo na rodada 1 e no Code Review do PR #120/#122 (sem regressão, endpoint inalterado neste fix) |
| `DELETE /api/public/push/unsubscribe` (endpoint existente) | ✅ OK | `204`, linha removida — `count(*) = 0` confirmado via `psql` após a chamada |
| `DELETE /api/public/push/unsubscribe` (mesmo endpoint, 2ª vez) | ✅ OK (idempotente) | `204` novamente, sem erro — nunca 404 |
| `PublisherJob` (push individual/consolidada/throttling) | ✅ OK | Coberto por `dotnet test` acima (`PublisherJobTests`, 5 casos, EF InMemory real, inalterado neste fix — nenhum arquivo do job tocado pelo PR #121/#122) |
| 410 Gone (remoção automática) | ✅ OK | Coberto por `dotnet test` acima (`PushNotificationServiceTests`, 9 casos incluindo 410 Gone, inalterado neste fix) |

Ambiente derrubado ao final: `docker compose down -v` (containers, network e volumes
removidos). Repo devolvido à branch `desenv` (`git checkout desenv`), `git status
--porcelain` limpo (salvo `.worktrees/` pré-existente, sem relação com esta issue).

### Critérios de aceite — veredito final (13/13)

| # | Critério | Veredito |
|---|---|---|
| 1 | manifest.json instalável | ✅ OK (inalterado, rodada 1) |
| 2 | `npm run build` gera `/public/sw.js` | ✅ OK (inalterado, rodada 1) |
| 3 | Fallback sem HTTPS | ✅ OK (inalterado, rodada 1) |
| 4 | Subscribe cria registro | ✅ OK (inalterado, rodada 1) |
| 5 | **Resubscribe: upsert silencioso, `created_at` renovado, nunca 409** | ✅ **CORRIGIDO E VALIDADO** (rodada 2, ver acima) |
| 6 | Rate-limit 429 | ✅ OK (inalterado, rodada 1) |
| 7 | Unsubscribe remove registro | ✅ OK (revalidado ao vivo, rodada 2) |
| 8 | Push individual | ✅ OK (revalidado via `dotnet test`, rodada 2) |
| 9 | Push consolidada | ✅ OK (revalidado via `dotnet test`, rodada 2) |
| 10 | 410 Gone remove automaticamente | ✅ OK (revalidado via `dotnet test`, rodada 2) |
| 11 | `GET vapid-public-key` chave em claro | ✅ OK (inalterado, rodada 1; comportamento default revalidado rodada 2) |
| 12 | `GET /api/settings` mascarado (sem vazamento) | ✅ OK (inalterado, rodada 1) |
| 13 | Migration `push_subscriptions` | ✅ OK (inalterado, herdado #56) |

## Conclusão

**13 de 13 critérios de aceite (100%) aprovados.** O achado que reprovou a rodada 1 (upsert
silencioso não persistia `p256dh`/`auth`/`created_at`) foi corrigido, revalidado ao vivo
contra Postgres real com reprodução exata do cenário reprovado, e nenhuma regressão foi
encontrada nos demais critérios (306/306 backend + 79/79 frontend, revalidação colateral dos
endpoints vizinhos do mesmo controller). **QA APROVADO.**
