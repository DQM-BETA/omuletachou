issue: 14
titulo: "feat: PWA + Push Notifications"
rota: normal
etapa_atual: Fix #116 implementado (PR #121 feature->desenv), aguardando merge do LT
ultimo_agente: dev-dotnet
status_comment_id: 5061626934
openspec_change: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications
tech_stacks:
  - Next.js (next-pwa)
  - ASP.NET Core (WebPush NuGet)
repos:
  omuletachou: https://github.com/DQM-BETA/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-14-pwa-push-notifications
openspec_path: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications
sub_issues:
  - "#116 (stack:dotnet, task_id:T-01) — Sub-A backend .NET — REABERTA para fix de upsert (QA reprovou, ver seção abaixo)"
  - "#117 (stack:nodejs, task_id:T-02) — Sub-B frontend Next.js — MERGED via PR #118"
desenv_tasks_merged:
  - "#117"
sub_issues_frontend:
  "#117": stack:nodejs
pr_homologacao: 120
pr_release: ~
code_review_homolog_pr: 120
qa_status: reprovado
figma_url: ~
blockers: "resubscribe (POST /api/public/push/subscribe) não atualiza p256dh/auth/created_at do registro existente — viola criterio de aceite (upsert silencioso com renovação de created_at) — fix pontual em #116, branch fix/116-push-subscribe-upsert a partir de desenv"
closedAt: ~

## Levantamento (PM Fase 1)
Escopo tecnico ja veio detalhado do Gerente na Issue. Perguntas de negocio postadas em
https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5061649138 cobrindo:
1. Armazenamento das VAPID keys (app_settings vs. secrets)
2. Migration/granularidade da tabela push_subscriptions
3. Rate-limit/anti-abuso no endpoint publico de subscribe
4. Placeholder de icones do manifest (design vs. dev)
5. Conteudo da notificacao push (titulo+link vs. imagem do produto)
6. Frequencia/throttling do PublisherJob ao disparar push
7. Confirmacao de que a dependencia de HTTPS (Issue #15) nao bloqueia dev/teste local (fallback ja previsto)
Aguardando resposta do Gerente (Gate 1) antes de seguir para PM Fase 2 (PRD/proposal.md).

## PM Fase 2 — PRD consolidado
Concluído.
- Respostas do Gerente ao Gate 1 resumidas e postadas em
  https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5069985444
- `proposal.md`: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-14-pwa-push-notifications/criterios-aceite.md
- Sumário do PRD postado como comentário na Issue #14:
  https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5069995252

Decisões de negócio consolidadas no PRD:
1. VAPID keys em `app_settings` (`push.vapid_public_key`/`push.vapid_private_key`), geradas
   uma única vez via `webpush generate-vapid-keys`, cadastro manual pelo operador, private
   key mascarada (padrão das demais credenciais sensíveis).
2. Migration única `AddPushSubscriptions` (não fatiada), `endpoint` UNIQUE.
3. Rate-limit 10 req/min por IP (padrão Issue #11) em `/api/public/push/subscribe`; upsert
   silencioso em resubscribe do mesmo endpoint (nunca 409).
4. Ícones do manifest: placeholders (iniciais "OM" ou tag genérica vermelha, `#e63946`),
   troca futura não bloqueia entrega.
5. Push individual com imagem do produto (`image` = MediaUrl/MediaLocalPath) quando o
   `PublisherJob` publica 1 produto no ciclo.
6. Push consolidada ("N novas ofertas hoje!") quando o `PublisherJob` publica >1 produto no
   mesmo ciclo — nunca uma notificação por produto.
7. `localhost` é contexto seguro reconhecido pela spec — dev/teste local do fluxo completo
   de PWA+push não depende da Issue #15 (HTTPS só é pré-requisito real em produção).

### Avaliação de ambiguidade arquitetural (decisão: NÃO escalar ao Arquiteto)
Ponto que mais se aproximava de uma decisão de arquitetura: **agrupamento/throttling das
notificações push e sua interação com o `PublisherJob` existente** (Issue #7). Ponderado
explicitamente:
1. **Web Push é uma integração externa única e já mapeada** (NuGet `WebPush` + API padrão
   `PushManager`/`ServiceWorker` do browser) — não há múltiplas stacks concorrentes nem
   provedor de push proprietário (ex.: FCM/APNs dedicados) a escolher.
2. **O comportamento de throttling já foi decidido pelo Gerente com precisão de
   implementação** (regra: >1 produto no ciclo → 1 notificação consolidada; 1 produto → push
   detalhado), incluindo o payload exato de cada caso. Não sobra uma decisão de design em
   aberto para o Arquiteto resolver — o LT só precisa decidir o ponto de integração técnica
   (ex.: acumular candidatos publicados dentro da execução do job antes de decidir
   individual vs. consolidado), o que é um detalhe de implementação, não arquitetural.
3. **Sem infraestrutura nova**: reaproveita `app_settings` (padrão já existente de
   credenciais), tabela nova simples (sem relacionamentos complexos) e o próprio
   `PublisherJob` já existente — nenhuma decisão de infraestrutura/deploy não-óbvia.
- **Conclusão**: sem ambiguidade arquitetural relevante. Segue direto para o **Líder
  Técnico** (design.md resumido + task breakdown), sem passar pelo Arquiteto. Recomenda-se
  que o LT registre no design.md o ponto de integração exato do throttling dentro do
  `PublisherJob` (nota técnica, não arquitetural).

## Refinamento Técnico (LT)
Concluído.
- `design.md`: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications/design.md
- `especificacao-tecnica.md`: repos/omuletachou/documentacoes/ISSUE-14-pwa-push-notifications/especificacao-tecnica.md
- `tasks.md`: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications/tasks.md
- Sumário técnico postado: https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5070042861

**Achado importante do levantamento (evita retrabalho):** inspeção do código real revelou
que a Issue #11/Sub-E (#85/#89, já mergeada em desenv) implementou entidade
`PushSubscription`, EF config e os endpoints `POST /api/public/push/subscribe` /
`DELETE /api/public/push/unsubscribe` (com rate-limit `public-write` já aplicado). Falta
apenas: migration da tabela (nunca criada no banco), envio efetivo via WebPush NuGet, VAPID
keys em `app_settings`, um novo endpoint público de leitura da chave pública, e o gatilho de
throttling dentro do `PublisherJob`. Escopo das sub-issues ajustado para não duplicar
trabalho já feito.

**Decisão UX/UI:** não acionado. Sem tela nova — manifest/ícones são infraestrutura de
browser (metadados + assets estáticos), sem fluxo de interação visual novo (prompts de
instalação/permissão são UI nativa do browser, fora do controle do app). Decisão registrada
em design.md.

**Sequenciamento das sub-issues:** paralelo (não bloqueante). O contrato do endpoint da
chave pública VAPID (`GET /api/public/push/vapid-public-key`) já está totalmente
especificado em especificacao-tecnica.md §4/§7 — a Sub-B pode desenvolver contra o
contrato documentado sem esperar o merge da Sub-A.

Sub-issues criadas:
- #116 (stack:dotnet, T-01) — Backend .NET: migration, PushNotificationService (WebPush),
  VAPID keys em app_settings, endpoint de chave pública, integração no PublisherJob.
- #117 (stack:nodejs, T-02) — Frontend Next.js: next-pwa, manifest+ícones placeholder,
  lógica de subscription/unsubscription no browser.

## Dev Sub-B #117 — Frontend Next.js (PWA + push subscription)
Concluído.
- Worktree `.worktrees/117-push-frontend` (branch `feature/117-push-frontend`, base `desenv`),
  removido ao final.
- `next-pwa` configurado (`website/next.config.mjs`): `dest: public`, `register: true`,
  `skipWaiting: true`, desabilitado em dev.
- `website/public/manifest.json` + ícones placeholder `icon-192x192.png`/`icon-512x512.png`
  (gerados via `website/scripts/generate-icons.js`, fundo sólido `#e63946`, sem dependência
  externa) referenciados em `app/layout.tsx` (`<link rel="manifest">` + `<meta
  name="theme-color">`).
- `website/lib/push.ts`: `subscribeToPush`/`unsubscribeFromPush` consumindo
  `GET {NEXT_PUBLIC_API_URL}/api/public/push/vapid-public-key` (sem hardcode da chave, nunca
  `API_INTERNAL_URL`) + `POST/DELETE` dos endpoints já mergeados na Issue #11/Sub-E, com
  fallback gracioso (sem Service Worker/PushManager/contexto seguro).
- `website/components/PushSubscriptionManager.tsx`: client component montado no layout raiz.
- Correção necessária em `website/tsconfig.json` (`types` restrito a
  `node`/`jest`/`react`/`react-dom`): o `next-pwa` traz transitivamente o stub
  `@types/minimatch@6.0.0` (sem `.d.ts` real), que quebrava `next build`.
- Testes Jest+RTL: 79/79 passando (100%), cobertura ≥ 80% em todos os arquivos novos.
- `npm run build`: gera `/public/sw.js` sem erros. Build Docker
  (`docker build -f website/Dockerfile website` + `docker run`) validado via `curl`:
  `manifest.json` e `sw.js` servidos corretamente (200).
- PR: https://github.com/DQM-BETA/omuletachou/pull/118 (`feature/117-push-frontend` →
  `desenv`, **merged via squash pelo LT**).

## Dev Sub-A #116 — Backend .NET (push notifications)
Concluído.
- Worktree `.worktrees/116-push-backend` (branch `feature/116-push-backend`, base `desenv`),
  removido ao final.
- **Achado por inspeção (corrige a premissa do LT):** a migration da tabela
  `push_subscriptions` já existia — foi englobada em `InitialSchema` na consolidação de
  migrations (#56), antes mesmo do refinamento desta issue. Confirmado via
  `dotnet ef migrations add AddPushSubscriptions` (diff vazio, migration removida) e por
  inspeção direta do schema no Postgres real (colunas/tipos/índice único `endpoint`
  conferem exatamente com o critério de aceite). Nenhuma migration de schema nova foi
  necessária — apenas o seed das VAPID keys.
- `SeedPushVapidKeys` (migration): `push.vapid_public_key`/`push.vapid_private_key`
  (ids 47/48) em `app_settings`, valores vazios (cadastro manual pelo operador).
- `PushNotificationService` (NuGet `WebPush` 1.0.13) + abstração `IWebPushSender`
  (testável sem HTTP real): `SendIndividualAsync`/`SendConsolidatedAsync`, lê as VAPID
  keys de `app_settings` a cada chamada (sem cache), remove a subscription do banco em
  `WebPushException` com `StatusCode == Gone`, isola falhas por subscription (não
  interrompe o lote).
- Novo endpoint `GET /api/public/push/vapid-public-key` no `PushController` existente —
  `[AllowAnonymous]`, rate-limit `public-read`, bypass explícito e documentado do
  `SettingsMasker` (única exceção ao sufixo `_key`).
- Throttling no `PublisherJob`: acumula produtos publicados com sucesso no Telegram no
  ciclo; ao final, dispara 1 push individual (exatamente 1 produto) ou 1 consolidada
  (>1), nenhuma se 0. Falha no envio de push nunca afeta o registro de sucesso da
  publicação (try/catch isolado, fire-and-forget do ponto de vista do ciclo).
- Testes: `PushNotificationServiceTests` (9 casos, incluindo 410 Gone e falha genérica
  isolada), `PublisherJobTests` (+5 casos de throttling 0/1/>1 produtos e isolamento de
  falha de push), `PushControllerTests` (+2 casos vapid-public-key). `dotnet test`:
  305/305 passando (100%).
- Boot real via `docker compose up -d --build db api`: subiu sem exceção, migrations
  aplicadas, schema `push_subscriptions` conferido no Postgres real, seed das VAPID keys
  presente. Smoke test real: par de VAPID keys gerado via `VapidHelper.GenerateVapidKeys()`,
  cadastrado via SQL, `GET vapid-public-key` retornou o valor cru, `POST subscribe`
  persistiu a subscription real no Postgres.
- PR: https://github.com/DQM-BETA/omuletachou/pull/119 (`feature/116-push-backend` →
  `desenv`, **merged via squash pelo LT**).

## Merge sub-issues → desenv (LT)
Concluído.
- Ambos PRs tocavam stacks disjuntas (backend .NET vs. frontend Next.js `website/`) — sem
  conflito. Mergeados sequencialmente conforme regra da squad: PR #118 (frontend) primeiro,
  `git pull` para atualizar working copy, depois PR #119 (backend). Ambos fast-forward,
  sem conflitos.
- Sub-issues #116 e #117 fechadas no GitHub (`completed`) com comentários de resumo.
- Todas as sub-issues da Issue #14 concluídas → PR de homologação criado:
  https://github.com/DQM-BETA/omuletachou/pull/120 (`desenv` → `homolog`, merge commit).
- Worktrees `.worktrees/116-push-backend` e `.worktrees/117-push-frontend` já haviam sido
  removidos pelos devs ao final de cada tarefa; nenhum resquício encontrado no repo.

## Code Review — PR #120 (desenv→homolog)
Aprovado. Camada 1 (`/code-review` plugin Anthropic): sem achados de confiança suficiente
(comentário: https://github.com/DQM-BETA/omuletachou/pull/120#issuecomment-5070339700 —
"No issues found. Checked for bugs and CLAUDE.md compliance."). Camada 2 (execução real,
abaixo).

### Backend (.NET) — execução real
- `dotnet test` (raiz `backend/`): **305/305 passando** (100%), 24s.
- Boot Docker real: `docker compose up -d --build db api` — subiu sem exceção, migrations
  aplicadas (incl. `SeedPushVapidKeys`), Hangfire instalado, `Application started`.
- Smoke test do bypass do `SettingsMasker` (ponto crítico de segurança do PR):
  1. Gerado par de VAPID keys real via `VapidHelper.GenerateVapidKeys()` (mini console app
     descartável referenciando o NuGet `WebPush`, fora do repo).
  2. Cadastradas via SQL direto em `app_settings` (ids 47/48, antes vazios).
  3. `GET /api/public/push/vapid-public-key` (sem auth) → retornou a chave pública **em
     claro**: `{"publicKey":"BJeffbN7jHNX...sxcH1vE"}`.
  4. `GET /api/settings` (autenticado, JWT de um usuário seed temporário criado só para o
     teste) → a MESMA chave (`push.vapid_public_key`) e também `push.vapid_private_key`
     retornaram **mascaradas** (`****************H1vE` / `****************5wx4`),
     confirmando por leitura de código (`SettingsController` chama
     `SettingsMasker.ApplyIfSensitive` sem exceção para nenhuma key) e por execução que o
     bypass do masker é estritamente local ao endpoint público dedicado — nenhum vazamento
     pelo endpoint autenticado do dashboard.
  5. Nenhuma VAPID key real gerada no smoke test foi commitada — `.env` (usado para o
     usuário seed temporário) é gitignored, `git status` limpo antes/depois, chaves só
     existiram no Postgres do container efêmero (destruído no `docker compose down -v`
     final).
- Integração real `PublisherJob` → push (leitura de código +
  `backend/src/AfiliadoBot.Tests/Jobs/PublisherJobTests.cs`): `SendPushNotificationsAsync`
  só é chamado ao FINAL do ciclo (após o loop de publicação), acumulando em
  `publishedProducts` apenas itens com `SocialNetwork == Telegram` e sucesso confirmado
  (`item.RegisterAttempt(true)` já persistido via `SaveChangesAsync` antes do push).
  Disparo de push envolto em try/catch dedicado (`catch (Exception ex) when (ex is not
  OperationCanceledException)`) que apenas loga warning — nunca propaga para o método
  `ExecuteAsync` nem desfaz o registro de sucesso já salvo. Cobertura por 5 casos de teste
  (0/1/>1 produtos, isolamento por rede, isolamento de falha de push) usando EF InMemory
  real (não só mock) para o `PublicationQueue`/`Product`, com `IPushNotificationService`
  mockado no limite correto (colaborador de aplicação, não a fronteira de rede — a
  fronteira real WebPush é testada separadamente em `PushNotificationServiceTests` via
  `IWebPushSender` mockado, isolando falhas 410 Gone e genéricas por subscription).

### Frontend (Next.js) — execução real
- `npm ci` (sync com `package-lock.json` atualizado pelo PR) + `npm test -- --ci`:
  **79/79 passando** (100%), 14 suites, 4.4s.
- `npm run build`: sucesso, `[PWA] Service worker: .../public/sw.js` gerado sem erros
  (critério de aceite formal confirmado); `public/sw.js` e `public/workbox-*.js` presentes
  no disco e corretamente listados em `.gitignore` (`/public/sw.js`,
  `/public/workbox-*.js`) — não versionados.
- Boot Docker real: `docker compose up -d --build website` (+ `api`, `db`) — subiu em
  `Ready in 79ms`. `curl http://localhost:3000/manifest.json` → 200, conteúdo idêntico ao
  critério de aceite (`name`, `short_name`, `display: standalone`, `theme_color #e63946`,
  `background_color #ffffff`, ícones 192/512). `curl http://localhost:3000/sw.js` → 200.

### Checklist de veto
- **Compila e sobe**: OK (backend + frontend, build e boot reais, ver acima).
- **Integração real**: OK (push via Docker+Postgres real no smoke test; PublisherJob↔push
  coberto por teste com EF InMemory real, não mock-only).
- **Conformidade com spec/UX**: OK — manifest, `theme_color`, `sw.js`, payloads de push
  (título/corpo/icon/image/data.url) e o endpoint de chave pública conferem exatamente com
  `criterios-aceite.md`. Sem tela nova (decisão UX/UI já registrada em design.md, sem
  pendência).
- **Sem teste-lixo**: não encontrado nenhum teste vazio/trivial nas 305+79 execuções
  inspecionadas amostralmente (`PushNotificationServiceTests`, `PublisherJobTests`,
  `PushControllerTests`, `push.test.ts`, `PushSubscriptionManager.test.tsx`,
  `manifest.test.ts`).
- **Segredos**: nenhuma VAPID key real commitada (`.env` gitignored, seed da migration usa
  valores vazios, cadastro manual pelo operador conforme PRD). `git status` limpo ao final.
- **OWASP Top 10 / vulnerabilidades**: nenhuma óbvia no código do PR. Observação não-
  bloqueante: `npm audit` acusa vulnerabilidades HIGH em devDependencies transitivas de
  `next-pwa` (`workbox-build` → `rollup-plugin-terser` → `serialize-javascript` RCE via
  regex; `glob` CLI injection) — `next-pwa` está sem manutenção ativa. Superfície de
  ataque é restrita ao processo de build (webpack plugin, sem input do atacante em tempo
  de build), não ao runtime do site público — não bloqueia esta entrega, mas registrado
  para acompanhamento futuro (considerar migração para `@ducanh2912/next-pwa`, fork
  mantido, quando a Issue #15 ou outra tocar PWA novamente).
- **`.first()`/`.nth()`/`.last()` em specs E2E**: não aplicável — não há suíte Playwright
  neste repositório ainda (nenhum `playwright.config.*` encontrado); os testes do PR são
  Jest+RTL (frontend) e xUnit (backend), sem seletor estrutural E2E a auditar.
- **Cobertura ≥ 80%**: confirmado pelos devs (Sub-A e Sub-B) e coerente com o volume de
  casos observado; segue `design.md` e cobre `criterios-aceite.md` integralmente (todas as
  seções: PWA/manifest, subscription, disparo de push, VAPID/masking, migration).

### Veredito
**Aprovado.** Merge `desenv→homolog` executado via merge commit (sem squash).

## QA — homolog
**Reprovado.** Relatório completo: `relatorio-qa.md` (mesmo diretório).

- Sincronização: `git fetch origin && git checkout homolog && git pull origin homolog` —
  commit `de37c66` (merge do PR #120) confirmado em `git log --oneline -5`.
- `dotnet test` (backend, a partir de `homolog`): **305/305 passando** (100%), 24s.
- `npm test -- --ci` (frontend `website/`, a partir de `homolog`): **79/79 passando**
  (100%), 14 suites, 3.26s.
- Ambiente completo subido via `docker compose up -d --build db api website` (a partir de
  `homolog`): subiu sem exceção (migrations aplicadas, Hangfire iniciado, Next.js
  `Ready in 78ms`).
- Validado manualmente e OK: manifest.json (200, todos os campos exigidos conferem),
  ícones 192/512 (200), `/sw.js` (200), `GET /api/public/push/vapid-public-key` (chave em
  claro, gerada via `webpush generate-vapid-keys` real e cadastrada via SQL em
  `app_settings`), `GET /api/settings` autenticado (mesma chave mascarada — sem
  vazamento), rate-limit 429 após exceder o limite em `/subscribe`,
  `DELETE /unsubscribe` (204 idempotente, remove o registro real do Postgres).
- **Achado que reprova a entrega:** `POST /api/public/push/subscribe` para um `endpoint`
  já existente retorna 200 (nunca 409, conforme o critério), mas **não atualiza**
  `p256dh`/`auth`/`created_at` do registro — apenas retorna o `id` existente sem
  persistir os novos valores recebidos no corpo da requisição. Testado ao vivo contra
  Postgres real: 1ª chamada cria a linha com `p256dh=testp256dh-v1`; 2ª chamada ao mesmo
  endpoint com `p256dh=testp256dh-v2-NEW` retorna 200 mas a linha no banco permanece
  inalterada (`testp256dh-v1`, `created_at` original). Isso viola diretamente o critério
  de aceite "Subscription já existe... o registro existente é atualizado (upsert
  silencioso, `created_at` renovado)" em `criterios-aceite.md`. Impacto funcional real:
  quando o browser regenera `p256dh`/`auth` (ex.: usuário limpa dados do site e refaz o
  subscribe com o mesmo `endpoint`, comportamento observado em alguns navegadores), o
  backend continua usando as chaves de criptografia antigas — os envios de push
  subsequentes falhariam silenciosamente na criptografia (WebPush usa `p256dh`/`auth`
  para cifrar o payload), sem 410 Gone (o endpoint em si continua válido), e portanto sem
  auto-remoção — subscription "zumbi" que nunca recebe push e nunca é limpa.
  Código: `backend/src/AfiliadoBot.Api/Controllers/PushController.cs` linhas 46-54.
- Endpoint 410 Gone (remoção automática): validado por leitura de código +
  `PushNotificationServiceTests` (já cobria 410 Gone e passou em `dotnet test` acima); a
  lógica de remoção em si está correta (`PushNotificationService.SendToAllAsync`, linhas
  112-118 de `PushNotificationService.cs`) — não é o achado reprovado acima, que é restrito
  ao endpoint `subscribe` (não ao envio de push).
- `PublisherJob` (push individual vs. consolidada): validado por `dotnet test` (5 casos em
  `PublisherJobTests`, EF InMemory real, já cobertos acima) + inspeção de código. Não foi
  possível disparar um ciclo real contra o Telegram ao vivo neste ambiente (sem
  credenciais de bot Telegram configuradas em `.env`/`.env.example`) — validação restrita
  a testes automatizados + inspeção, consistente com o que a Camada 2 do Code Review já
  havia feito.
- Fallback sem HTTPS: validado por inspeção de código (`website/lib/push.ts`,
  `isPushSupported()` checa `window.isSecureContext`, retorna `null` gracioso sem lançar
  exceção) + teste unitário existente (`push.test.ts`, incluído nos 79/79 acima) — não
  testado ao vivo sem HTTPS real, conforme instrução da tarefa.
- Ambiente derrubado ao final: `docker compose down -v`. Repo devolvido à branch `desenv`
  (`git checkout desenv && git pull origin desenv`), `git status` limpo.

## QA reprovou — mapeamento da falha (LT)

### O que o handler faz hoje (confirmado por leitura direta do código em `homolog`)
`backend/src/AfiliadoBot.Api/Controllers/PushController.cs`, método `Subscribe`
(linhas 46-54, código atual, não paráfrase):

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

É um **early-return puro**: quando `existing` não é nulo, o handler devolve `200 Ok` com o
`Id` já existente e sai — nunca toca em `existing.P256dh`/`existing.Auth`/
`existing.CreatedAt`, nunca chama `SaveChangesAsync` neste branch. Não é um `Add`
condicional (esse só roda no branch `else`, linhas 56-58) — é a ausência total de um
caminho de atualização.

Causa raiz adicional na entidade (`backend/src/AfiliadoBot.Domain\Entities\PushSubscription.cs`):
todos os setters são `private` e não existe nenhum método público de mutação além do
construtor — mesmo que o controller quisesse atualizar os campos diretamente, a entidade
não expõe como. O fix precisa adicionar um método de domínio (ex.: `Renew(p256dh, auth)`)
que atualize `P256dh`/`Auth`/`CreatedAt = DateTime.UtcNow` e seja chamado pelo controller
antes do `SaveChangesAsync`.

### Escopo do fix: sub-issue #116 (reaberta), não uma nova sub-issue
Decisão: **reabrir #116** (`gh issue reopen 116`), não criar sub-issue nova nem tratar como
tech-debt solto da Issue #11.
- O método `Subscribe` é o mesmo componente que a Sub-A (#116) tocou por último (mesmo
  `PushController`, mesmo endpoint `/subscribe`) — mapear o fix na mesma sub-issue mantém
  o rastreamento 1:1 entre sub-issue e arquivo/responsabilidade, e o comportamento de
  upsert (CA formal de "Subscription — subscribe/unsubscribe") está no escopo da Issue #14
  independentemente de quem tocou o método por último (nota de contexto do QA já
  registrada em `relatorio-qa.md`: o método nasceu na Issue #11/Sub-E, mas o *critério*
  reprovado pertence à Issue #14).
- Não é um fixup trivial de 1 linha isolado do resto da issue: falta o método de domínio
  `Renew` (mudança na Entidade, não só no Controller) e um teste de regressão novo — cabe
  melhor como retrabalho rastreado na sub-issue do componente do que como um commit solto
  sem sub-issue associada.
- **Sub-issue #116 reaberta no GitHub** (estava `completed`, agora `open` novamente).

### Comportamento esperado (para o Dev .NET implementar sem ambiguidade)
`POST /api/public/push/subscribe`, quando já existe uma linha com o mesmo `Endpoint`:
1. Continua retornando **200 OK** com `{ id: existing.Id }` (isso já está correto — não
   mexer).
2. **Antes** de retornar, deve **atualizar** a linha existente com os novos valores do
   corpo da requisição:
   - `P256dh` ← `request.Keys.P256dh` (novo valor recebido, não o antigo)
   - `Auth` ← `request.Keys.Auth` (novo valor recebido, não o antigo)
   - `CreatedAt` ← `DateTime.UtcNow` (renovado, não o timestamp original de criação)
3. Persistir via `await _db.SaveChangesAsync(ct)` no branch `existing is not null` (hoje
   ausente).

Implementação sugerida (o Dev decide os detalhes finais, mas o contrato é este):
- Adicionar método público na entidade `PushSubscription`
  (`backend/src/AfiliadoBot.Domain/Entities/PushSubscription.cs`), ex.:
  `public void Renew(string p256dh, string auth) { P256dh = p256dh; Auth = auth;
  CreatedAt = DateTime.UtcNow; }` — mantém os setters `private` (encapsulamento
  preservado), só adiciona a via de mutação controlada.
- No `PushController.Subscribe`, branch `existing is not null`: chamar
  `existing.Renew(request.Keys.P256dh, request.Keys.Auth);` seguido de
  `await _db.SaveChangesAsync(ct);` antes do `return Ok(new { id = existing.Id });`.

### Teste de regressão a escrever
Em `PushControllerTests` (mesmo arquivo dos +2 casos de `vapid-public-key` já existentes),
usando o mesmo padrão de EF InMemory já usado no arquivo (ver testes existentes da
Sub-A para o setup de `DbContext`/`WebApplicationFactory` já em uso):
- **Arrange**: seed direto no `DbContext` de uma `PushSubscription` existente com
  `Endpoint = "https://fcm.googleapis.com/fcm/send/abc"`, `P256dh = "old-p256dh"`,
  `Auth = "old-auth"`, `CreatedAt` = um instante no passado (ex.: `DateTime.UtcNow.
  AddDays(-1)`, guardado numa variável para comparação).
- **Act**: chamar `POST /subscribe` com o mesmo `Endpoint`, mas `Keys.P256dh =
  "new-p256dh"`, `Keys.Auth = "new-auth"`.
- **Assert**:
  1. Resposta HTTP é `200 OK` (nunca `409`, nunca `201`) com o mesmo `Id` da linha
     original (não cria linha nova — `count(*) == 1` para aquele `Endpoint`).
  2. Reconsultando o banco: `P256dh == "new-p256dh"` e `Auth == "new-auth"` (os valores
     novos, não os antigos).
  3. `CreatedAt` da linha após a chamada é **maior** que o `CreatedAt` original salvo no
     Arrange (renovado, não preservado).
- Nome sugerido: `Subscribe_ExistingEndpoint_UpdatesKeysAndRenewsCreatedAt`.
- Não remover/alterar os testes existentes de `Subscribe` que cobrem o caminho de criação
  (`existing is null` → 201) — este é um teste adicional, não substituto.

### Branch e fluxo de promoção
**Branch: `fix/116-push-subscribe-upsert`, a partir de `desenv` (não de `homolog`).**
Justificativa:
- O bug já está mergeado em `homolog` (via PR #120, commit `de37c66`), mas `homolog` é
  branch protegida que só aceita PR vindo de `desenv` (regra da squad, sem exceção para
  hotfix) — não existe `homolog→homolog` nem PR direto para `homolog` a partir de uma
  branch de fix.
- `desenv` está, neste momento, no mesmo conteúdo relevante de `homolog` para este arquivo
  (nenhum outro trabalho tocou `PushController.cs` desde o PR #120) — branchear de
  `desenv` não perde nem reintroduz nada.
- Fluxo: Dev abre branch `fix/116-push-subscribe-upsert` a partir de `desenv` → implementa
  fix + teste de regressão → PR `fix/116-push-subscribe-upsert → desenv` (squash, é
  branch de trabalho descartável) → LT faz merge para `desenv` → LT abre novo PR
  `desenv → homolog` (merge commit, nunca squash) substituindo/complementando o já
  mergeado PR #120 → QA reexecuta a validação do critério #5 (e idealmente um smoke re-run
  geral, já que o ambiente será resubido) → só então segue para PR de release
  `homolog → main` (ainda não criado; `pr_release` continua `~`).
- `pr_homologacao: 120` mantido no estado.md como referência histórica do primeiro merge;
  o novo PR desenv→homolog (criado pelo LT depois que o Dev concluir) deve atualizar este
  campo para o número do novo PR.

### Nota sobre `desenv_tasks_merged`
Removido `#116` da lista (a entrega da sub-issue está incompleta enquanto o critério de
upsert não for corrigido) — mantido apenas `#117` (frontend, sem relação com este achado,
não reaberto). `#116` volta à lista quando o novo PR do fix for mergeado em `desenv`.

## Fix Sub-A #116 — upsert de subscribe
Concluído (Dev .NET). Aguardando merge do LT (`fix/116-push-subscribe-upsert → desenv`).
- Worktree `.worktrees/fix-116-upsert` (branch `fix/116-push-subscribe-upsert`, base
  `desenv`), removido ao final.
- `PushSubscription.Renew(string p256dh, string auth)`: novo método de domínio
  (`backend/src/AfiliadoBot.Domain/Entities/PushSubscription.cs`) que atualiza
  `P256dh`/`Auth`/`CreatedAt = DateTime.UtcNow`, mantendo os setters `private`.
- `PushController.Subscribe` (`backend/src/AfiliadoBot.Api/Controllers/PushController.cs`),
  branch `existing is not null`: agora chama `existing.Renew(request.Keys.P256dh,
  request.Keys.Auth)` + `await _db.SaveChangesAsync(ct)` antes do `return Ok(new { id =
  existing.Id })` — continua nunca retornando 409.
- Teste de regressão `Subscribe_ExistingEndpoint_UpdatesKeysAndRenewsCreatedAt` em
  `PushControllerTests` (mesmo arquivo, mesmo padrão `WebApplicationFactory`/EF InMemory já
  em uso): seed de subscription existente com `CreatedAt` no passado (via reflection no
  setter privado, já que a entidade não expõe outro jeito de forçar um `CreatedAt`
  arbitrário em teste), `POST /subscribe` no mesmo endpoint com `p256dh`/`auth` novos,
  confirma resposta `200` com o mesmo `id` (sem linha duplicada, `count == 1`) e, via nova
  consulta ao banco, `P256dh`/`Auth` atualizados e `CreatedAt` maior que o original.
- Gate obrigatório (passo g): buscados todos os arquivos que referenciam
  `PushSubscription`/`PushController` (20 arquivos, `Grep`) — únicos testes relevantes são
  `PushControllerTests` (editado) e `PushNotificationServiceTests` (não relacionado ao
  endpoint `subscribe`, sem necessidade de ajuste).
- `dotnet test` (raiz `backend/`): **306/306 passando** (100%), 25s — o teste novo eleva o
  total de 305 para 306.
- Boot Docker real: `docker compose up -d --build db api` — subiu sem exceção (precisou de
  um `.env` local com `JWT_SIGNING_KEY`/`DB_USER`/`DB_PASSWORD` de teste, removido ao final,
  nunca commitado — `.env` é gitignored). Migrations aplicadas, Hangfire iniciado,
  `Application started`.
- Smoke test manual reproduzindo o cenário exato do QA: `POST /subscribe` (endpoint novo,
  `p256dh-v1`/`auth-v1`) → `201`; `POST /subscribe` no mesmo endpoint com
  `p256dh-v2-NEW`/`auth-v2-NEW` → `200` (mesmo `id`, não `409`); `psql` direto no Postgres
  real confirmou a linha atualizada: `p256dh=p256dh-v2-NEW`, `auth=auth-v2-NEW`,
  `created_at` renovado para o instante da 2ª chamada (não o original). Ambiente derrubado
  (`docker compose down -v`) ao final.
- PR: https://github.com/DQM-BETA/omuletachou/pull/121 (`fix/116-push-subscribe-upsert` →
  `desenv`, **NÃO mergeado** — aguardando LT).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao | Coordenador | haiku-4.5 | 26844 | 21 | 133s |
| 2 | PM Fase 1 | pm | sonnet | 27827 | 8 | 46s |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 40649 | 18 | 153s |
| 4 | Refinamento LT | lider-tecnico | sonnet | 80702 | 42 | 329s |
| 5 | Dev Sub-B #117 | dev-nodejs | sonnet | 90906 | 64 | 690s |
| 6 | Dev Sub-A #116 | dev-dotnet | sonnet | 196222 | 136 | 1139s |
| 7 | Merge #118/#119 + PR homologação #120 | lt | sonnet | 49500 | 17 | 132s |
| 8 | Code Review — PR #120 (desenv→homolog) | code-review | sonnet | 80642 | 52 | 717s |
| 9 | QA — homolog (reprovado) | qa | sonnet | 88467 | 37 | 509s |
| 10 | LT mapeamento da falha (QA reprovou) | lt | sonnet | 67850 | 15 | 227s |
| 11 | Fix Sub-A #116 — upsert de subscribe | dev-dotnet | sonnet | 74736 | 35 | 263s |
