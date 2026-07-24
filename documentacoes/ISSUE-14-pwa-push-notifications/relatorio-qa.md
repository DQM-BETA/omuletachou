# Relatório QA — Issue #14 (PWA + Push Notifications)

**Status: REPROVADO**

PR validado: #120 (`desenv` → `homolog`), commit `de37c66` confirmado em `homolog` via
`git log --oneline -5` após `git fetch origin && git checkout homolog && git pull origin homolog`.

## Suítes automatizadas (a partir de `homolog`)
| Suíte | Resultado |
|---|---|
| `dotnet test` (backend) | 305/305 passando (100%), 24s |
| `npm test -- --ci` (frontend `website/`) | 79/79 passando (100%), 14 suites, 3.26s |

## Ambiente integrado
`docker compose up -d --build db api website` a partir de `homolog` — subiu sem exceção:
migrations aplicadas (incl. `SeedPushVapidKeys`), Hangfire iniciado, Next.js
`Ready in 78ms`. Ambiente derrubado ao final com `docker compose down -v`.

Não há suíte Playwright / `test:visual` neste repositório (confirmado: nenhum script
`test:visual` em `website/package.json`, nenhum `playwright.config.*` no repo) —
**E2E/screenshots: N/A** (critério de decisão: ausência do script `test:visual`, não
julgamento de plataforma). Gate visual de screenshots não se aplica.

## Critérios de aceite — veredito

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

## Achado que reprova a entrega

**`POST /api/public/push/subscribe` não atualiza o registro existente no resubscribe.**

Código: `backend/src/AfiliadoBot.Api/Controllers/PushController.cs`, linhas 46-54:

```csharp
var existing = await _db.PushSubscriptions
    .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, ct);

if (existing is not null)
{
    // Endpoint ja cadastrado: subscribe e idempotente no sentido de nao duplicar
    // (mesmo endpoint = mesmo dispositivo/navegador). Retorna 200 sem criar linha nova.
    return Ok(new { id = existing.Id });
}
```

Reproduzido ao vivo contra Postgres real (ambiente Docker subido pelo QA):
1. `POST /subscribe` com `endpoint=X`, `p256dh=testp256dh-v1`, `auth=testauth-v1` → 201,
   linha criada com esses valores.
2. `POST /subscribe` novamente com o mesmo `endpoint=X`, mas `p256dh=testp256dh-v2-NEW`,
   `auth=testauth-v2-NEW` → 200 (correto, não retorna 409).
3. Consulta direta no Postgres: a linha permanece com `p256dh=testp256dh-v1`,
   `auth=testauth-v1` e `created_at` original — **os novos valores da 2ª chamada nunca
   foram persistidos**.

Isso contradiz diretamente `criterios-aceite.md` (seção "Subscription — subscribe/unsubscribe"):
> **Given** uma subscription já existe para um `endpoint` (usuário limpou cache e refez o
> subscribe) **When** `POST /api/public/push/subscribe` é chamado novamente com o mesmo
> `endpoint` **Then** o registro existente é atualizado (upsert silencioso, `created_at`
> renovado), retornando sucesso — nunca erro 409

**Impacto funcional real (não é só um detalhe formal do critério):** o `p256dh`/`auth` são
as chaves de criptografia do Web Push — quando o browser gera um novo par para o mesmo
`endpoint` (cenário citado explicitamente no critério: "usuário limpou cache e refez o
subscribe"), o backend continua enviando push cifrado com as chaves antigas. O provedor de
push (ex. FCM) normalmente aceita a entrega (o `endpoint` em si continua válido, não é 410
Gone), mas o browser do usuário não consegue decifrar o payload — a notificação nunca
aparece, silenciosamente, e a subscription nunca é removida automaticamente (não há erro
HTTP que dispare a lógica de limpeza por 410 Gone). Resultado: acúmulo de subscriptions
"zumbis" que nunca recebem push, sem qualquer sinalização de falha.

**Nota de contexto:** este endpoint (`PushController.Subscribe`) foi implementado
originalmente na Issue #11/Sub-E (#85/#89), antes desta issue existir — o Dev Sub-A #116
não tocou nesse método (apenas reaproveitou o endpoint já mergeado), então o bug já existia
em `desenv` antes do PR #120. Ainda assim, é parte do escopo formal de `criterios-aceite.md`
da Issue #14 (seção "Subscription"), portanto reprova esta entrega — o LT decide se a
correção é feita como fixup da Issue #14 ou tratada como uma tech-debt/bug separado da Issue
#11, mas o comportamento correto precisa existir antes do merge para `main`.

## Conclusão

11 de 12 critérios testáveis (12 de 13 linhas da tabela, considerando o de migration já
herdado) passam integralmente. O único achado é concreto, reproduzido ao vivo contra
Postgres real, com impacto funcional real (não cosmético) e contradiz o texto literal do
critério de aceite. QA não aprova parcialmente — **reprovado**.
