# Especificação técnica — ISSUE-14 PWA + Push Notifications

## 1. Migration `AddPushSubscriptions` (Sub-A)
A entidade/EF config já existem (`PushSubscription.cs`,
`PushSubscriptionConfiguration.cs`, Issue #11/Sub-E) — só falta a migration. Gerar via
`dotnet ef migrations add AddPushSubscriptions` no projeto `AfiliadoBot.Infrastructure`
(startup project `AfiliadoBot.Api`). SQL equivalente esperado:

```sql
CREATE TABLE push_subscriptions (
    id UUID NOT NULL PRIMARY KEY,
    endpoint TEXT NOT NULL,
    p256dh TEXT NOT NULL,
    auth TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);
CREATE UNIQUE INDEX "IX_push_subscriptions_endpoint" ON push_subscriptions (endpoint);
```
Migration única, não fatiada (conforme critério de aceite).

## 2. Seed das VAPID keys em `app_settings` (Sub-A)
Nova migration de seed (padrão `SeedTikTokCredentials.cs`), próximos IDs livres da tabela
`app_settings` (último usado: 46, ver `20260713152839_SeedTikTokCredentials.cs`):

```csharp
migrationBuilder.InsertData(
    table: "app_settings",
    columns: new[] { "id", "key", "updated_at", "value" },
    values: new object[,]
    {
        { 47, "push.vapid_public_key", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
        { 48, "push.vapid_private_key", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
    });
```
Valores vazios — cadastrados manualmente pelo operador via dashboard (`PUT /api/settings/{key}`,
já implementado na Issue #11/#13) após rodar `webpush generate-vapid-keys` uma única vez.
`push.vapid_private_key` é mascarada no dashboard pelo `SettingsMasker` já existente (sufixo
`_key`). `push.vapid_public_key` também é mascarada lá (mesmo sufixo) — aceito, ver design.md;
o browser nunca lê essa chave via `SettingsController`, e sim pelo endpoint público novo
(item 4).

## 3. `PushNotificationService` (Sub-A)
NuGet `WebPush` (pacote `WebPush`, mantenedor `web-push-libs`) adicionado ao
`AfiliadoBot.Infrastructure.csproj` (mesmo projeto do `DbContext`/repositórios).

```csharp
namespace AfiliadoBot.Infrastructure.Push;

public interface IPushNotificationService
{
    Task SendIndividualAsync(Product product, CancellationToken ct = default);
    Task SendConsolidatedAsync(int count, CancellationToken ct = default);
}

public class PushNotificationService : IPushNotificationService
{
    // Construtor: AfiliadoBotDbContext + ILogger<PushNotificationService>.
    // VAPID keys lidas de app_settings (push.vapid_public_key/push.vapid_private_key) a
    // cada chamada (nao cachear em memoria — permite rotacionar sem reiniciar o processo).
    // VAPID subject: "mailto:contato@omuletachou.com.br" (fixo, nao configuravel por ora).

    // SendToAllAsync: busca todas as PushSubscription, monta VapidDetails +
    // WebPushClient.SendNotificationAsync(PushSubscription, payloadJson, vapidDetails) POR
    // subscription (sem broadcast nativo da lib). Cada envio em try/catch isolado:
    //   - WebPushException com StatusCode == HttpStatusCode.Gone -> remove a subscription
    //     do banco (_db.PushSubscriptions.Remove(...)), loga Information, CONTINUA o loop.
    //   - Qualquer outra excecao -> loga Warning, CONTINUA o loop (nao interrompe os demais
    //     envios do lote).
    // SaveChangesAsync uma vez ao final (apos processar todas as subscriptions).
}
```

Payload individual (1 produto publicado no ciclo):
```json
{
  "title": "Nova oferta do Mulet 🔥",
  "body": "{ProductTitle} — R$ {SalePrice} ({DiscountPct}% OFF)",
  "icon": "/icon-192x192.png",
  "image": "{MediaUrl}",
  "data": { "url": "https://omuletachou.com.br/oferta/{Slug}" }
}
```
`{SalePrice}` formatado com 2 casas decimais (`0.00`), `{DiscountPct}` como inteiro.
`image` usa `Product.MediaUrl` (fallback `Product.MediaLocalPath` se `MediaUrl` for null —
mesma prioridade usada em `PublicDealDto.FromProduct`, conferir se já existe helper lá antes
de duplicar lógica).

Payload consolidado (>1 produto no ciclo, `count` = quantidade):
```json
{
  "title": "Nova oferta do Mulet 🔥",
  "body": "{count} novas ofertas hoje! Confira no site 👀",
  "icon": "/icon-192x192.png",
  "data": { "url": "https://omuletachou.com.br" }
}
```
Sem `image` (sem produto específico).

## 4. Novo endpoint público — chave pública VAPID (Sub-A)
Acrescentar ao `PushController` existente (`backend/src/AfiliadoBot.Api/Controllers/PushController.cs`):

```
GET /api/public/push/vapid-public-key
```
- `[AllowAnonymous]` (já é o padrão do controller), `[EnableRateLimiting(RateLimiterConfigurator.PublicReadPolicy)]`
  (60 req/min/IP — é leitura, reaproveita a policy do `PublicController`, não a
  `public-write` do subscribe).
- Lê `app_settings["push.vapid_public_key"]` (valor cru, SEM passar pelo `SettingsMasker` —
  esse endpoint não usa o `SettingsController`).
- Resposta 200: `{ "publicKey": "<valor cru>" }`. Se a chave ainda não foi cadastrada
  (string vazia) → 200 `{ "publicKey": null }` (frontend trata como "push indisponível",
  não quebra a página).

## 5. Endpoints já existentes (Issue #11/Sub-E, sem mudança nesta issue)
Confirmar contrato ao integrar o frontend — já implementado e mergeado em `desenv`:
- `POST /api/public/push/subscribe` — body `{ "endpoint": "...", "keys": { "p256dh": "...",
  "auth": "..." } }` → 200 `{ id }` (endpoint já cadastrado, idempotente) ou 201 `{ id }`
  (novo). Rate-limit `public-write` (10 req/min/IP) já aplicado.
- `DELETE /api/public/push/unsubscribe?endpoint=...` → 204 sempre (idempotente, nunca 404).

## 6. `next-pwa` (Sub-B)
`website/next.config.mjs` (projeto já usa `.mjs`):
```js
import withPWA from 'next-pwa';

const pwaConfig = withPWA({
  dest: 'public',
  register: true,
  skipWaiting: true,
  disable: process.env.NODE_ENV === 'development',
});

export default pwaConfig({
  // config existente do Next.js aqui dentro
});
```
`public/manifest.json`:
```json
{
  "name": "O Mulet Achou",
  "short_name": "Mulet Achou",
  "display": "standalone",
  "theme_color": "#e63946",
  "background_color": "#ffffff",
  "icons": [
    { "src": "/icon-192x192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icon-512x512.png", "sizes": "512x512", "type": "image/png" }
  ]
}
```
Ícones placeholder: gerar/versionar `public/icon-192x192.png` e `public/icon-512x512.png`
(iniciais "OM" ou tag genérica, fundo `#e63946`, fora do escopo de design refinado).
`app/layout.tsx`: adicionar `<link rel="manifest" href="/manifest.json" />` e
`<meta name="theme-color" content="#e63946" />` no `<head>`.

## 7. Subscription no browser (Sub-B)
Novo módulo client-side (ex. `website/lib/push.ts` + client component que o invoca a partir
de `app/layout.tsx` ou de um componente dedicado montado no root):
```ts
'use client';
const API_URL = process.env.NEXT_PUBLIC_API_URL; // nunca API_INTERNAL_URL (client-side)

export async function subscribeToPush(): Promise<void> {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) return; // fallback gracioso

  const registration = await navigator.serviceWorker.ready;
  const { publicKey } = await fetch(`${API_URL}/api/public/push/vapid-public-key`).then((r) => r.json());
  if (!publicKey) return; // VAPID ainda não cadastrada — não tenta subscribe

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') return;

  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(publicKey), // helper padrão da spec Web Push
  });

  const { endpoint, keys } = subscription.toJSON();
  await fetch(`${API_URL}/api/public/push/subscribe`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ endpoint, keys: { p256dh: keys!.p256dh, auth: keys!.auth } }),
  });
}

export async function unsubscribeFromPush(): Promise<void> {
  if (!('serviceWorker' in navigator)) return;
  const registration = await navigator.serviceWorker.getRegistration();
  const subscription = await registration?.pushManager.getSubscription();
  if (!subscription) return;

  const endpoint = subscription.endpoint;
  await subscription.unsubscribe();
  await fetch(`${API_URL}/api/public/push/unsubscribe?endpoint=${encodeURIComponent(endpoint)}`, {
    method: 'DELETE',
  });
}
```
`urlBase64ToUint8Array`: helper padrão da spec Web Push para converter a VAPID public key
(base64url) em `Uint8Array` — implementar como função utilitária local, sem dependência
externa nova.

## 8. Testes obrigatórios
- Backend: `AfiliadoBot.Tests` — migration aplicada (teste de schema, se já houver padrão
  similar no repo) ou teste de integração do `PushNotificationService` (mock de
  `WebPushClient`/wrapper testável — ver se a lib expõe interface mockável; senão, extrair
  uma abstração fina `IWebPushSender` para permitir teste unitário do 410 Gone e do
  agrupamento individual/consolidado), teste do `PublisherJob` cobrindo 0/1/>1 produtos
  publicados no Telegram no ciclo (seguir padrão de `PublisherJobTests.cs` já existente),
  teste do endpoint `GET /api/public/push/vapid-public-key` (chave cadastrada e vazia).
  Cobertura mínima 80% (padrão do repo).
- Frontend: Jest + RTL para o módulo de subscription (mock de `navigator.serviceWorker`,
  `PushManager`, `Notification.requestPermission`) cobrindo os 3 fallbacks (sem SW, sem
  PushManager, permissão negada) e o fluxo feliz. `npm run test:coverage`.
