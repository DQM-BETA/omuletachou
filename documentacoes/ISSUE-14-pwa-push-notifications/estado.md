issue: 14
titulo: "feat: PWA + Push Notifications"
rota: normal
etapa_atual: Aguardando QA (homolog)
ultimo_agente: code-review
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
  - "#116 (stack:dotnet, task_id:T-01) — Sub-A backend .NET — MERGED via PR #119"
  - "#117 (stack:nodejs, task_id:T-02) — Sub-B frontend Next.js — MERGED via PR #118"
desenv_tasks_merged:
  - "#116"
  - "#117"
sub_issues_frontend:
  "#117": stack:nodejs
pr_homologacao: 120
pr_release: ~
code_review_homolog_pr: 120
qa_status: ~
figma_url: ~
blockers: nenhum
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
