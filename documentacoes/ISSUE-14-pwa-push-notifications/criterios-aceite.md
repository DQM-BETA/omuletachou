# Critérios de aceite — ISSUE-14 PWA + Push Notifications

## PWA — instalação e manifest

**Given** o site é acessado via HTTPS (ou `localhost` em ambiente de dev)
**When** o usuário clica em "Adicionar à tela inicial"
**Then** o ícone do app aparece na tela inicial do celular e o app abre em modo `standalone`
com `theme_color #e63946` e `background_color #ffffff`

**Given** o comando `npm run build` é executado no projeto `website/`
**When** o build finaliza
**Then** o Service Worker é gerado em `/public/sw.js` sem erros, com `register: true` e
`skipWaiting: true`, e desabilitado em ambiente de dev

**Given** o site é acessado sem HTTPS e fora de `localhost`
**When** a página carrega
**Then** o Service Worker não registra, mas a página funciona normalmente, sem erros
visíveis ao usuário

**Given** o manifest.json publicado
**When** inspecionado
**Then** contém `name: "O Mulet Achou"`, `short_name: "Mulet Achou"`, `display: "standalone"`
e ícones placeholder 192x192 e 512x512 (iniciais "OM" ou tag genérica em vermelho)

## Subscription — subscribe/unsubscribe

**Given** um usuário aceita a permissão de notificação no browser
**When** o front-end chama `serviceWorker.pushManager.subscribe` com a VAPID public key e
envia `POST /api/public/push/subscribe` com `{ endpoint, p256dh, auth }`
**Then** um registro é criado (ou atualizado) na tabela `push_subscriptions`

**Given** uma subscription já existe para um `endpoint` (usuário limpou cache e refez o
subscribe)
**When** `POST /api/public/push/subscribe` é chamado novamente com o mesmo `endpoint`
**Then** o registro existente é atualizado (upsert silencioso, `created_at` renovado),
retornando sucesso — nunca erro 409

**Given** mais de 10 requisições por minuto vindas do mesmo IP para
`/api/public/push/subscribe`
**When** o limite é excedido
**Then** a API responde 429, seguindo o mesmo padrão de rate-limit da Issue #11

**Given** um usuário inscrito que deseja parar de receber notificações
**When** o front-end chama `DELETE /api/public/push/unsubscribe` com `{ endpoint }`
**Then** a subscription correspondente é removida da tabela `push_subscriptions`

## Disparo de push pelo PublisherJob

**Given** o `PublisherJob` publica com sucesso exatamente 1 produto no Telegram em um ciclo
de execução
**When** a publicação é confirmada
**Then** uma notificação push individual é enviada a todos os inscritos, com título "Nova
oferta do Mulet 🔥", corpo "{ProductTitle} — R$ {SalePrice} ({DiscountPct}% OFF)", `icon`
`/icon-192x192.png`, `image` = `MediaUrl`/`MediaLocalPath` do produto, e `data.url` apontando
para a página da oferta

**Given** o `PublisherJob` publica com sucesso mais de 1 produto no mesmo ciclo de execução
**When** as publicações são confirmadas
**Then** é enviada UMA única notificação push consolidada (ex.: "3 novas ofertas hoje!
Confira no site 👀"), nunca uma notificação por produto

**Given** um endpoint de subscription que retorna HTTP 410 Gone ao receber um push
**When** o envio falha com esse status
**Then** a subscription correspondente é removida automaticamente da tabela
`push_subscriptions`, sem interromper o envio para os demais endpoints do lote

## VAPID keys e configuração

**Given** as VAPID keys ainda não existem
**When** o operador executa `webpush generate-vapid-keys` e cadastra manualmente
`push.vapid_public_key` e `push.vapid_private_key` em `app_settings` via dashboard
**Then** os valores ficam disponíveis para o backend, com a private key mascarada na UI
(mesmo padrão das demais credenciais sensíveis)

**Given** VAPID keys já cadastradas e subscriptions ativas
**When** qualquer operação do sistema roda normalmente
**Then** as VAPID keys nunca são regeradas automaticamente (regra de negócio, sem
verificação técnica automática — depende de disciplina operacional)

## Migration

**Given** a migration `AddPushSubscriptions` é aplicada
**When** o schema é inspecionado
**Then** existe a tabela `push_subscriptions` com colunas `id` (UUID PK), `endpoint`
(TEXT NOT NULL UNIQUE), `p256dh` (TEXT NOT NULL), `auth` (TEXT NOT NULL), `created_at`
(TIMESTAMPTZ NOT NULL DEFAULT NOW()), como migration única (não fatiada)
