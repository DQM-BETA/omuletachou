# Design técnico — ISSUE-14 PWA + Push Notifications

## Visão geral
Duas frentes independentes, mas com um contrato de integração pequeno entre elas (chave
pública VAPID):
1. **Backend (.NET, `backend/`)**: completar a infraestrutura de push que a Issue #11/Sub-E
   já deixou parcialmente pronta (entidade `PushSubscription`, EF config, endpoints
   `POST /api/public/push/subscribe` e `DELETE /api/public/push/unsubscribe`, rate-limit
   `public-write`) — falta a **migration** (a tabela ainda não existe no banco), o
   **envio efetivo** do push (`WebPush` NuGet), as **VAPID keys** em `app_settings`, um
   **endpoint público para expor a chave pública** ao browser, e o **gatilho de throttling**
   dentro do `PublisherJob` (Issue #7).
2. **Frontend (Next.js, `website/`)**: tornar o site instalável (manifest + Service Worker
   via `next-pwa`) e implementar a lógica de subscribe/unsubscribe no browser.

## Achado importante do levantamento (evita retrabalho)
Inspeção de `backend/src/AfiliadoBot.Api/Controllers/PushController.cs`,
`backend/src/AfiliadoBot.Domain/Entities/PushSubscription.cs` e
`backend/src/AfiliadoBot.Infrastructure/Data/Configurations/PushSubscriptionConfiguration.cs`
confirma que a **Issue #11/Sub-E (#85/#89) já implementou**:
- Entidade `PushSubscription` (Id, Endpoint, P256dh, Auth, CreatedAt) + EF Core config
  (tabela `push_subscriptions`, `Endpoint` UNIQUE via índice).
- `PushController` com `POST /api/public/push/subscribe` (idempotente — endpoint já
  cadastrado retorna 200 sem duplicar) e `DELETE /api/public/push/unsubscribe` (204
  idempotente, nunca 404).
- Rate-limit `public-write` (10 req/min/IP) já aplicado ao subscribe via
  `RateLimiterConfigurator` (`[EnableRateLimiting]`).

**O que falta e é o escopo real da Sub-A desta issue:**
- Migration `AddPushSubscriptions` (a tabela `push_subscriptions` nunca foi criada no
  banco — só existe no modelo EF; sem ela, o `PushController` já mergeado quebra em
  runtime).
- `PushNotificationService` (NuGet `WebPush`) — o envio efetivo ainda não existe.
- VAPID keys em `app_settings` (seed migration).
- Novo endpoint público `GET /api/public/push/vapid-public-key` (não existe; o
  `PushController` atual só tem subscribe/unsubscribe).
- Gatilho de push dentro do `PublisherJob`.

## Ponto de integração do throttling no `PublisherJob` (nota técnica, não arquitetural)
`backend/src/AfiliadoBot.Application/Jobs/PublisherJob.cs` — `ExecuteAsync` itera
`PublicationQueue` items e publica via `ISocialPublisher` resolvido por `item.SocialNetwork`.
Conforme `proposal.md` ("Integrações externas"), o push é disparado **após a publicação bem-
sucedida no Telegram** (primeira rede publicada no ciclo) — não a cada rede.

Estratégia de implementação (dentro do loop existente, sem alterar o fluxo de publicação):
1. Antes do loop, declarar `var publishedProducts = new List<Product>();`.
2. Dentro do bloco `if (success)` (linha ~65-69), **após** `item.RegisterAttempt(true)`:
   `if (item.SocialNetwork == SocialNetwork.Telegram) publishedProducts.Add(item.Product);`
3. **Após o loop** (depois do `foreach`, antes do fim do método): se
   `publishedProducts.Count == 1` → `PushNotificationService.SendIndividualAsync(product)`;
   se `publishedProducts.Count > 1` → `SendConsolidatedAsync(publishedProducts.Count)`; se
   `== 0` → não envia nada. Chamada via nova dependência `IPushNotificationService` injetada
   no construtor do `PublisherJob` (registrada no DI, escopo scoped, como o `_dbContext`).
4. Falha no envio de push (ex.: todos os endpoints 410, ou nenhuma subscription cadastrada)
   **não deve** afetar o retorno/registro de sucesso da publicação em si — é fire-and-forget
   com try/catch interno no service, logado como warning, nunca propagado ao `PublisherJob`.
5. Cada subscription é enviada individualmente (`WebPushClient.SendNotificationAsync` não
   suporta broadcast nativo); 410 Gone em uma subscription específica não interrompe o envio
   às demais (loop com try/catch por subscription dentro do `SendToAllAsync`).

## Contrato de integração frontend↔backend (chave pública VAPID)
O frontend roda no browser (client-side, para poder chamar
`serviceWorker.pushManager.subscribe`), então usa `NEXT_PUBLIC_API_URL` (já existe em
`docker-compose.yml`, ex. `http://localhost:5000`) — **nunca** `API_INTERNAL_URL`
(server-only, usado só em Server Components, ver `website/lib/api.ts`).
- `GET {NEXT_PUBLIC_API_URL}/api/public/push/vapid-public-key` → `{ "publicKey": "BEx..." }`
  (chave pública, não sensível — não passa pelo `SettingsMasker`; endpoint dedicado,
  `AllowAnonymous`, rate-limit `public-read` reaproveitado do `RateLimiterConfigurator`).
- Nota sobre mascaramento no dashboard: como `push.vapid_public_key` termina em `_key`,
  `SettingsMasker.IsSensitive` a classifica como sensível e o `GET /api/settings` (dashboard
  autenticado) sempre a mascara — comportamento aceito (é um campo de configuração
  write-once; o operador não precisa reler o valor completo pelo dashboard; o browser lê o
  valor real pelo endpoint público dedicado, não pelo `SettingsController`).

**Sequenciamento das sub-issues:** NÃO bloqueante entre si — o contrato acima está
totalmente especificado (endpoint, payload, env var) em `especificacao-tecnica.md`, então
os dois times podem implementar em paralelo. A Sub-B (frontend) pode desenvolver contra o
contrato documentado (mock/stub local do endpoint durante dev, se necessário) sem esperar o
merge da Sub-A; a integração real acontece naturalmente quando ambas estiverem em `desenv`.

## Decisão UX/UI
Esta issue **não aciona o agente UX/UI da squad**. Não há tela nova: manifest.json e ícones
são artefatos de infraestrutura de browser (metadados + assets estáticos), já com conteúdo
e paleta definidos no PRD (`name`, `theme_color #e63946`, `background_color #ffffff`,
placeholders com iniciais "OM"/tag genérica). Não há fluxo de interação visual novo — o
prompt de instalação e o de permissão de notificação são UI nativa do browser, fora do
controle do app. Ícones finais ficam para iteração futura de design (fora do escopo técnico
desta entrega, conforme PRD).

## Componentes por stack
**Backend (.NET) — Sub-A:**
- Migration `AddPushSubscriptions` (cria tabela `push_subscriptions`) — a única faltante,
  já que entidade/config já existem.
- Seed migration para `push.vapid_public_key`/`push.vapid_private_key` em `app_settings`
  (valores vazios por padrão — cadastro manual pelo operador via dashboard, conforme
  PRD/regra de negócio; não gerar chaves automaticamente na migration).
- NuGet `WebPush` no `AfiliadoBot.Infrastructure.csproj` (ou novo projeto, ver
  especificacao-tecnica.md).
- `IPushNotificationService`/`PushNotificationService`: `SendToAllAsync(payload)` (usado
  por `SendIndividualAsync`/`SendConsolidatedAsync`), tratamento de `WebPushException` com
  `StatusCode == HttpStatusCode.Gone` → remove a subscription do banco.
- Novo endpoint `GET /api/public/push/vapid-public-key` no `PushController` existente.
- Integração no `PublisherJob` (ver seção acima).

**Frontend (Next.js) — Sub-B:**
- `next-pwa` configurado em `next.config.mjs` (o projeto já usa `.mjs`, não `.js`),
  `register: true`, `skipWaiting: true`, desabilitado em dev (`disable: process.env.NODE_ENV
  === 'development'`).
- `public/manifest.json` + ícones placeholder 192x192/512x512 em `public/`.
- `<link rel="manifest">` e meta tags de theme-color no layout raiz (`app/layout.tsx`).
- Lógica de subscription (client component): registra Service Worker, pede permissão,
  busca a VAPID public key via `GET {NEXT_PUBLIC_API_URL}/api/public/push/vapid-public-key`,
  chama `pushManager.subscribe({ userVisibleOnly: true, applicationServerKey })`, envia
  `POST {NEXT_PUBLIC_API_URL}/api/public/push/subscribe` com `{ endpoint, keys: { p256dh,
  auth } }` (formato já implementado pelo `PushController` existente — ver
  `PushSubscribeRequest`/`PushKeys` em `PushController.cs`).
- Fallback gracioso: `if (!('serviceWorker' in navigator) || !('PushManager' in window))`
  → não renderiza/não tenta nada, sem erros visíveis.
