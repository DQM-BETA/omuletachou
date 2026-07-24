# Task breakdown — ISSUE-14 PWA + Push Notifications

Sequenciamento: **paralelo** (Sub-A e Sub-B não se bloqueiam — contrato do endpoint de
chave pública já especificado em especificacao-tecnica.md §4/§7; ver design.md §"Contrato
de integração").

## Sub-A (#116) — Backend .NET (stack:dotnet)
Task ID: T-01

Critérios de aceite:
1. **Given** a migration `AddPushSubscriptions` é aplicada, **When** o schema é inspecionado,
   **Then** existe a tabela `push_subscriptions` (id UUID PK, endpoint TEXT UNIQUE NOT NULL,
   p256dh TEXT NOT NULL, auth TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL), migration
   única (não fatiada).
2. **Given** as VAPID keys ainda não existem, **When** a seed migration roda, **Then**
   `push.vapid_public_key`/`push.vapid_private_key` existem em `app_settings` com valor
   vazio (cadastro manual pelo operador via dashboard depois).
3. **Given** o `PublisherJob` publica com sucesso exatamente 1 produto no Telegram no
   ciclo, **When** a publicação é confirmada, **Then** é enviada 1 push individual (título,
   corpo com produto/preço/desconto, `icon`, `image` = MediaUrl/MediaLocalPath,
   `data.url` da oferta).
4. **Given** o `PublisherJob` publica com sucesso >1 produto no Telegram no mesmo ciclo,
   **When** as publicações são confirmadas, **Then** é enviada 1 única push consolidada
   (nunca uma por produto).
5. **Given** um endpoint de subscription retorna 410 Gone, **When** o envio falha,
   **Then** a subscription é removida do banco automaticamente, sem interromper o envio
   às demais subscriptions do lote.
6. **Given** o endpoint `GET /api/public/push/vapid-public-key`, **When** chamado,
   **Then** retorna `{ "publicKey": "<valor cru>" }` (ou `null` se ainda não cadastrada),
   sem passar pelo `SettingsMasker`, `AllowAnonymous`, rate-limit `public-read`.
7. Cobertura de testes ≥ 80% (padrão do repo), incluindo os 3 cenários de throttling
   (0/1/>1 produtos) no `PublisherJob` e o tratamento de 410 Gone.

Contexto técnico:
- docs: `documentacoes/ISSUE-14-pwa-push-notifications/especificacao-tecnica.md` §1-5, §8
- design: `openspec/changes/issue-14-pwa-push-notifications/design.md`
- Não recriar: `PushController.Subscribe/Unsubscribe`, entidade `PushSubscription`, EF
  config, rate-limit `public-write` (já mergeados na Issue #11/Sub-E #85/#89).
- stack: ASP.NET Core 8, EF Core, PostgreSQL, NuGet `WebPush`
- repo: DQM-BETA/omuletachou (branch base: desenv)

## Sub-B (#117) — Frontend Next.js (stack:nodejs)
Task ID: T-02

Critérios de aceite:
1. **Given** `npm run build` no projeto `website/`, **When** finaliza, **Then** gera
   `/public/sw.js` sem erros (`register: true`, `skipWaiting: true`), desabilitado em dev.
2. **Given** o manifest.json publicado, **When** inspecionado, **Then** contém
   `name`/`short_name`/`display: standalone`/`theme_color #e63946`/
   `background_color #ffffff` e ícones placeholder 192x192/512x512.
3. **Given** o site acessado via HTTPS ou localhost, **When** o usuário usa "Adicionar à
   tela inicial", **Then** o ícone aparece na tela inicial e abre em modo standalone.
4. **Given** o site acessado sem HTTPS fora de localhost, **When** a página carrega,
   **Then** o Service Worker não registra, sem quebrar a página nem exibir erros.
5. **Given** o usuário aceita a permissão de notificação, **When** o front-end busca a
   VAPID public key e chama `pushManager.subscribe` + `POST .../push/subscribe`, **Then**
   a subscription é criada com sucesso (`endpoint`, `keys.p256dh`, `keys.auth`).
6. **Given** um usuário inscrito que deseja parar, **When** o front-end chama
   `DELETE .../push/unsubscribe`, **Then** a subscription local e remota são removidas.
7. Testes Jest+RTL cobrindo os 3 fallbacks (sem Service Worker, sem PushManager, permissão
   negada) e o fluxo feliz de subscribe/unsubscribe.

Contexto técnico:
- docs: `documentacoes/ISSUE-14-pwa-push-notifications/especificacao-tecnica.md` §6-8
- design: `openspec/changes/issue-14-pwa-push-notifications/design.md`
- ATENÇÃO: `NEXT_PUBLIC_API_URL` (client-side), nunca `API_INTERNAL_URL`
- stack: Next.js 14 (App Router), `website/`
- repo: DQM-BETA/omuletachou (branch base: desenv)
