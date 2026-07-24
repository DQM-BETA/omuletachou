# ISSUE-14 — PWA + Push Notifications

## Objetivo
Transformar o site público (`website/`, Next.js) em app instalável no celular (PWA) e
disparar notificações push aos usuários inscritos quando o `PublisherJob` publicar novas
ofertas — mesmo com o site/app fechado.

## Usuários afetados
- **Visitantes do site público**: podem instalar o app na tela inicial e opcionalmente se
  inscrever para receber notificações de novas ofertas.
- **Operador do dashboard**: responsável por gerar e cadastrar as VAPID keys uma única vez,
  antes do primeiro deploy com push ativo.

## Casos de uso principais
1. Usuário acessa o site via HTTPS (ou `localhost` em dev) e usa "Adicionar à tela inicial"
   → ícone do app aparece na tela inicial do celular, abre em modo `standalone`.
2. Usuário aceita a permissão de notificação → browser gera subscription (endpoint, p256dh,
   auth) → front-end faz `POST /api/public/push/subscribe`.
3. `PublisherJob` publica com sucesso 1 produto no Telegram → dispara push individual
   detalhado (título, preço, desconto, imagem do produto, link da oferta).
4. `PublisherJob` publica mais de 1 produto no mesmo ciclo de execução → dispara UMA única
   push consolidada ("N novas ofertas hoje! Confira no site 👀"), não uma por produto.
5. Push enviado para um endpoint que retorna 410 Gone (subscription expirada/inválida) →
   subscription é removida do banco automaticamente, sem erro visível ao usuário.
6. Usuário limpa o cache do browser e refaz o subscribe com o mesmo endpoint → upsert
   silencioso (atualiza `created_at`), sem erro 409.
7. Usuário se desinscreve → `DELETE /api/public/push/unsubscribe` remove a subscription.

## Casos de exceção
- PWA acessado sem HTTPS e fora de `localhost` → Service Worker não registra, mas a página
  funciona normalmente (nenhuma quebra funcional).
- `POST /api/public/push/subscribe` acima de 10 req/min por IP → HTTP 429 (mesmo padrão de
  rate-limit da Issue #11).
- Envio de push falha por endpoint inválido → tratado (remoção da subscription), não deve
  interromper o envio para os demais endpoints do lote.

## Regras de negócio
- VAPID keys (`push.vapid_public_key`, `push.vapid_private_key`) vivem em `app_settings`,
  mesmo padrão das demais credenciais do sistema. Geradas UMA ÚNICA VEZ via
  `webpush generate-vapid-keys`, inseridas manualmente pelo operador no dashboard, com a
  mesma UX de mascaramento das credenciais sensíveis existentes aplicada à private key.
  NUNCA regerar após existirem subscriptions ativas (invalidaria todas).
- Tabela `push_subscriptions`: `endpoint` é UNIQUE. Reinserção do mesmo endpoint é
  upsert silencioso (atualiza `created_at`), nunca erro de conflito.
- Rate-limit de escrita pública: 10 req/min por IP via `RateLimiter` nativo do .NET 8,
  mesmo padrão já usado na Issue #11.
- Conteúdo da notificação individual (1 produto no ciclo):
  ```json
  {
    "title": "Nova oferta do Mulet 🔥",
    "body": "{ProductTitle} — R$ {SalePrice} ({DiscountPct}% OFF)",
    "icon": "/icon-192x192.png",
    "image": "{MediaUrl}",
    "data": { "url": "https://omuletachou.com.br/oferta/{slug}" }
  }
  ```
  `image` usa `MediaUrl`/`MediaLocalPath` do produto.
- Conteúdo da notificação consolidada (>1 produto no ciclo): título/corpo genérico
  ("Nova oferta do Mulet 🔥" / "N novas ofertas hoje! Confira no site 👀"), sem imagem de
  produto específico, `data.url` apontando para a home/listagem de ofertas.
- Ícones do manifest: placeholders (iniciais "OM" ou ícone de tag/oferta genérico em
  vermelho, alinhado ao `theme_color #e63946`), 192x192 e 512x512. Substituição dos
  arquivos finais não bloqueia a entrega técnica.
- Manifest: `name: "O Mulet Achou"`, `short_name: "Mulet Achou"`, `display: "standalone"`,
  `theme_color: "#e63946"`, `background_color: "#ffffff"`.
- HTTPS: obrigatório para PWA/push funcionarem em produção (dependência da Issue #15), mas
  `localhost` é contexto seguro reconhecido pela spec de Service Workers — dev/teste local
  do fluxo completo não é bloqueado.

## Integrações externas
- **Web Push Protocol** via NuGet `WebPush` (.NET) e API `PushManager`/`ServiceWorker` do
  browser (front-end).
- **`next-pwa`** (wrapper Workbox) para geração do Service Worker no build do Next.js.
- Dependência funcional com `PublisherJob` (Issue #7) — o disparo de push ocorre após a
  publicação bem-sucedida no Telegram (primeira rede publicada no ciclo).
- Endpoints REST novos acrescentados à API pública já existente (Issue #11):
  `POST /api/public/push/subscribe`, `DELETE /api/public/push/unsubscribe`.

## Restrições / prazo
- Depende das Issues #11 (API pública) e #12 (site público) já implementadas.
- HTTPS em produção depende da Issue #15 (deploy com SSL) — não bloqueia dev/teste local.
- Ícones finais do manifest ficam para uma iteração de design futura; placeholders liberam
  a entrega técnica agora.

## Definição de pronto
- `npm run build` gera Service Worker em `/public/sw.js` sem erros.
- "Adicionar à tela inicial" funciona via HTTPS (ou localhost) e abre o app em modo
  standalone com os ícones placeholder.
- Subscribe/unsubscribe funcionam fim a fim, com upsert silencioso e rate-limit de
  10 req/min por IP.
- `PublisherJob` dispara push individual (1 produto) ou consolidado (>1 produto) conforme
  regra de throttling, incluindo imagem do produto quando aplicável.
- Endpoint com 410 Gone é removido automaticamente do banco no próximo envio.
- Sem HTTPS (fora de localhost), a página funciona normalmente, sem Service Worker e sem
  erros visíveis ao usuário.
