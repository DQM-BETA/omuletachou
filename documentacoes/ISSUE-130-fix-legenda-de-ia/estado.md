issue: 130
titulo: "fix: Legenda de IA nunca é persistida — todo post sai sem legenda"
etapa_atual: QA
ultimo_agente: code-review
openspec_change: openspec/changes/issue-130-fix-legenda-de-ia
tech_stacks:
  - dotnet
  - angular
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-130-fix-legenda-de-ia
openspec_path: repos/omuletachou/openspec/changes/issue-130-fix-legenda-de-ia
sub_issues:
  - "#139 (stack:dotnet, task_id: Sub-A) — backend: migration, PublicationQueue.Caption, ProcessorJob, 4 publishers, ProductDetailDto, ProcessorJobTests.cs — BLOQUEANTE — MERGED (PR #141)"
  - "#140 (stack:angular, task_id: Sub-B) — frontend: ProductDetail/ProductsService + facebook-manual.component consomem ai_caption — MERGED (PR #142)"
desenv_tasks_merged:
  - "#139"
  - "#140"
sub_issues_frontend:
  "#140": angular
pr_homologacao: 143
pr_release: ~
code_review_homolog_pr: 143
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: 5167525186

## PM Fase 1
Levantamento de requisitos postado na Issue #130 (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5167522736), com 5 perguntas objetivas para o Gerente:
1. Onde armazenar a legenda por rede social (novo campo `Caption` em `PublicationQueue` vs. gerar no momento da publicação vs. outra abordagem).
2. Quando gerar a legenda (no enfileiramento vs. no momento da publicação).
3. Se a correção deve expor a legenda de IA no Facebook Manual (dashboard) ou fica fora do escopo.
4. Se é necessário registrar/comunicar a retrocompatibilidade (produtos já publicados sem legenda) ou apenas seguir em frente.
5. Confirmação de que a cobertura de teste (`ProcessorJobTests.cs`) será corrigida para validar persistência, não só a chamada.

Aguardando respostas do Gerente (Gate 1) para prosseguir com PM Fase 2 (proposal.md + critérios de aceite).

## PM Fase 2
Gerente respondeu ao Gate 1 (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169630106):
1. Novo campo `Caption` em `PublicationQueue` — fonte de verdade para publicação. `Product.AiCaption` pode ser removido ou mantido só para propósitos não-autoritativos. Migration: `ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL DEFAULT ''`.
2. Geração mantida no `ProcessorJob` (não move para `PublisherJob`) — evita multiplicar chamadas pagas à API Claude em retries e preserva separação de responsabilidades.
3. Facebook Manual no escopo: `ProductDetailDto` (backend) + `ProductDetail` (frontend) passam a expor/consumir a caption real da rede Facebook.
4. Sem backfill/retrocompatibilidade — só uma linha de changelog no PR.
5. Confirmado: `ProcessorJobTests.cs` corrigido para validar persistência (`PublicationQueue.Caption`), não apenas chamada de mock.

PRD consolidado e postado (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169632533):
- `openspec/changes/issue-130-fix-legenda-de-ia/proposal.md`
- `documentacoes/ISSUE-130-fix-legenda-de-ia/criterios-aceite.md` (CA1–CA18)

**Ambiguidade arquitetural:** nenhuma. Todas as decisões de design vieram definidas pelo Gerente no Gate 1 (campo, ponto de geração, escopo do Facebook Manual, migration aditiva simples). Sem múltiplas stacks em conflito, integração externa nova, ou trade-off de arquitetura não-óbvio. Segue direto para o Líder Técnico (refinamento técnico / task breakdown), sem passar pelo Arquiteto.

## Líder Técnico (refinamento)
Código real inspecionado antes do design (`ProcessorJob.cs`, os 4 publishers, `PublicationQueue.cs`,
`ProductDtos.cs`/`ProductsController.cs`, `facebook-manual.component.ts/html`, `ProcessorJobTests.cs`):
confirmado que `GenerateCaptionAsync` é chamado em `CreatePublicationQueueEntriesAsync` mas o retorno é
descartado (linha 256 do `ProcessorJob.cs`); os 4 publishers leem `product.AiCaption` (nunca escrito por
código ativo); o Facebook Manual usa `post.product?.description` como legenda (mesmo bug na ponta do
dashboard).

Documentação escrita:
- `openspec/changes/issue-130-fix-legenda-de-ia/design.md` (resumido — sem ambiguidade arquitetural).
- `documentacoes/ISSUE-130-fix-legenda-de-ia/especificacao-tecnica.md` (contratos exatos: migration,
  construtor de `PublicationQueue`, trechos exatos dos 4 publishers, `ProductDetailDto.ai_caption`,
  `ProductsController.GetProduct`, `ProductDetail.ai_caption` no Angular, correção de
  `ProcessorJobTests.cs`).
- `openspec/changes/issue-130-fix-legenda-de-ia/tasks.md` (por sub-issue, critérios + contexto técnico).

Sub-issues criadas:
- #139 — Sub-A (backend, `stack:dotnet`, BLOQUEANTE): migration + `PublicationQueue.Caption` +
  `ProcessorJob` + 4 publishers + `ProductDetailDto`/`ProductsController` + `ProcessorJobTests.cs`.
- #140 — Sub-B (frontend, `stack:angular`, depende do contrato do DTO da Sub-A): `ProductDetail`/
  `ProductsService` + `facebook-manual.component` consomem `ai_caption`.

**Decisão UX/UI:** não acionado. Nenhuma tela/componente novo — apenas troca da fonte de dados de um texto
já existente na tela (campo de legenda), documentado em design.md.

Sumário técnico postado na Issue #130 (comentário
https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169710249). Comentário 📍 Status editado
para "Em Desenvolvimento".

## Dev Sub-A #139
Worktree `.worktrees/139-fix-caption-backend` (branch `fix/139-caption-backend`, a partir de `desenv`).

Implementado (TDD RED→GREEN):
- Migration `AddCaptionToPublicationQueue` (`ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL
  DEFAULT ''`), gerada via `dotnet ef migrations add` — SQL efetivo conferido, igual ao especificado.
- `PublicationQueue.cs`: nova propriedade `Caption`; construtor com 4º parâmetro `caption` (breaking change
  interno — único caller de produção era `ProcessorJob`; todos os ~80 call sites de teste em
  `PublicationQueueTests.cs`, `ReportsControllerTests.cs`, `QueueControllerTests.cs`,
  `InstagramPublisherTests.cs`, `PublisherJobTests.cs`, `TelegramPublisherTests.cs`,
  `TikTokPublisherTests.cs`, `YoutubePublisherTests.cs` atualizados para o novo construtor).
- `PublicationQueueConfiguration.cs`: mapeamento da coluna `caption`.
- `ProcessorJob.cs` (`CreatePublicationQueueEntriesAsync`): retorno de `GenerateCaptionAsync` agora
  persistido no item de `PublicationQueue` criado por rede (antes descartado).
- 4 publishers (`TelegramPublisher`, `YoutubePublisher` — `BuildMetadataJson` ganhou parâmetro `caption`,
  `InstagramPublisher`, `TikTokPublisher`): leitura trocada de `product.AiCaption` para `item.Caption`.
  Confirmado via grep: nenhuma referência a `AiCaption` restante nos 4 arquivos (CA5).
- `ProductDetailDto`/`ProductsController.GetProduct`: novo campo `ai_caption`, resolvido do item de
  `PublicationQueue` mais recente (`OrderByDescending(CreatedAt)`) da rede Facebook associado ao produto —
  `null` quando não existe item para essa rede (distinto de `""`, valor válido para itens legados).
- `ProcessorJobTests.cs`: mock de `GenerateCaptionAsync` retorna valor determinístico por rede (`$"Legenda
  {network}"`); asserts de persistência real em `PublicationQueue.Caption` (CA15); novo teste de múltiplas
  redes habilitadas comprovando captions distintas sem sobrescrita (CA16); testes `Times.Never` existentes
  (CA17) mantidos, já continham `entries.Should().BeEmpty()/NotContain(...)`.
- 2 novos testes em `ProductsControllerTests.cs` (CA12): `ai_caption` retorna a caption do item Facebook
  mais recente (múltiplos itens, incl. Telegram, para comprovar filtro por rede) / `null` quando não há
  item de fila para Facebook.
- Corrigidos 3 testes de publisher que quebraram silenciosamente com a extração mecânica dos call sites
  (`InstagramPublisherTests.PublishAsync_AnexaDisclosure_QuandoLegendaNaoContem`,
  `PublishAsync_NaoDuplicaDisclosure_QuandoJaPresente`, `TikTokPublisherTests` equivalentes): a legenda de
  teste precisa vir de `item.Caption`, não mais de `product.AiCaption`.

Gate de testes: `dotnet test` → **321/321 passando (100%)**.

Boot real (evidência, não suposição): `.env` local criado (gitignored) com `DB_USER`/`DB_PASSWORD`/
`JWT_SIGNING_KEY`/seed de usuário; `docker compose up -d --build db api` — API sobe sem exceção (migration
aplicada automaticamente no startup, seed de usuário ok, Hangfire registrado). Verificação funcional via
`psql` + chamadas HTTP internas ao container (`docker exec afiliado_api curl ...`):
- `\d publication_queue` confirma coluna `caption text NOT NULL DEFAULT ''::text` (CA1).
- Produto de teste inserido (`Status=Queued`, Telegram habilitado) + `POST /api/jobs/processor/trigger`
  (autenticado) → `publication_queue.caption` preenchida com texto real e não vazio (fallback determinístico
  do `ClaudeAiService.GenerateCaptionAsync` quando a API Claude não está configurada no ambiente local, que
  nunca lança exceção — comportamento preexistente, CA6 preservado).
- Item de fila Facebook inserido manualmente com caption própria → `GET /api/products/{id}` retornou
  `"ai_caption":"Legenda Facebook de teste real via banco"`, confirmando CA12 fim-a-fim.
- Dados de teste removidos, `docker compose down`, `.env` local removido ao final (não versionado).

PR aberto: https://github.com/DQM-BETA/omuletachou/pull/141 (`fix/139-caption-backend` → `desenv`, contém
nota de changelog CA18 no corpo do PR). Worktree removido após push.

## Líder Técnico (merge Sub-A #139)
PR #141 revisado (diff completo, 22 arquivos, +904/-99) — atenção especial à migration
(`AddCaptionToPublicationQueue`: coluna `caption text NOT NULL DEFAULT ''`, aditiva, sem risco de
quebra em dados existentes) e ao contrato do DTO (`ProductDetailDto.AiCaption` → `ai_caption`, novo
campo opcional, não quebra consumidores existentes do endpoint). Os 4 publishers confirmados lendo
`item.Caption` em vez de `product.AiCaption` (bug original corrigido de forma consistente).

Merge squash `fix/139-caption-backend` → `desenv` (PR #141), branch remota deletada. Fast-forward
limpo em `desenv` local, sem conflitos (`e9328d2..1276278`). Sub-issue #139 fechada com comentário de
resumo (https://github.com/DQM-BETA/omuletachou/issues/139).

Sub-B (#140, Angular) desbloqueada: contrato `ai_caption` já disponível em `desenv`.

## Dev Sub-B #140
Worktree `.worktrees/140-fix-caption-frontend` (branch `fix/140-caption-frontend`, a partir de `desenv`).

Implementado (TDD RED→GREEN):
- `ProductDetail` (`products.service.ts`): campo opcional `ai_caption?: string | null` adicionado (mesmo
  padrão snake_case de `ai_score`/`ai_reason`, sem decorators de serialização).
- `facebook-manual.component.html`: texto exibido/copiado do botão "copiar legenda" trocado de
  `post.product?.description` para `post.product?.ai_caption`, com fallback `'Legenda não disponível'`
  quando `null` (item legado ou job de IA ainda não gerou legenda para Facebook) — decisão de copy tomada
  pelo dev por não haver string exata fechada na especificação (CA14 exige apenas fallback explícito, não
  string literal); documentada aqui por transparência.
- `facebook-manual.component.ts`: sem mudança de lógica (confirmado pela especificação — só o template lia
  a propriedade).
- `facebook-manual.component.spec.ts`: fixture `productDetail` passa a ter `description` distinta de
  `ai_caption` (evita falso positivo de teste); CA-D1 ajustado para validar `ai_caption`; novo teste CA13
  (clique real no botão copia o valor de `ai_caption`, nunca `description`); novo teste CA14 (`ai_caption:
  null` → UI exibe fallback, não quebra, botão copia string vazia).
- `products.service.spec.ts`: revisado (CA-D1 existente) — não precisou de ajuste, `ai_caption` é opcional.

Gate de testes: `npm test` → **107/107 passando (100%)**. `ng build` → build de produção ok (warning de
budget de bundle pré-existente, fora de escopo desta correção).

Boot real (evidência, não suposição): `.env` local criado (gitignored, removido ao final) +
`docker-compose.override.test.yml` temporário (removido antes do commit, só expôs portas de teste) —
`docker compose up -d --build db api dashboard`. Migration da Sub-A confirmada aplicada automaticamente
(`\d publication_queue` mostra a coluna `caption`). Produto + item de `PublicationQueue` (Facebook,
`caption` preenchida) inseridos via SQL direto no Postgres do container. `GET /api/products/{id}`
autenticado, feito através do próprio proxy nginx do container `dashboard` (mesmo caminho de rede que o
Angular usa em produção), retornou `"ai_caption":"Legenda de IA real gerada para Facebook..."` distinto de
`"description"` — confirma que o contrato consumido pelo componente está correto fim-a-fim. Containers e
dados de teste removidos ao final (`docker compose down -v`).

PR aberto: https://github.com/DQM-BETA/omuletachou/pull/142 (`fix/140-caption-frontend` → `desenv`).
Worktree removido após push.

## Líder Técnico (merge Sub-B #140)
PR #142 revisado (diff completo, 3 arquivos) — confirmado uso de `ai_caption` em vez de `description` para
exibição e cópia da legenda no Facebook Manual, com fallback explícito quando `null`. Testes CA13/CA14
cobrem os dois cenários (legenda de IA real vs. fallback legado).

Merge squash `fix/140-caption-frontend` → `desenv` (PR #142), branch remota deletada. Fast-forward limpo em
`desenv` local, sem conflitos (`4c9111f..96589db`). Sub-issue #140 fechada com comentário de resumo
(https://github.com/DQM-BETA/omuletachou/issues/140).

**Ambas as sub-issues da Issue #130 mergeadas em `desenv` (#139 e #140).** PR de homologação criado:
https://github.com/DQM-BETA/omuletachou/pull/143 (`desenv` → `homolog`, merge commit — descreve bug, fix
completo, sub-issues e origem/auditoria pedida pelo Gerente em 2026-08-03).

## Code Review — PR #143

Verificacao completa executada (build + boot real + suite + checklist de veto), nao apenas leitura do
diff. Achados do plugin `/code-review` (comentario
https://github.com/DQM-BETA/omuletachou/pull/143#issuecomment-5170033722) revisados e incorporados: nenhum
achado de alta confianca pendente (os 2 candidatos avaliados — falta de backfill e cenario teorico de
multiplos itens de fila Facebook por produto — foram corretamente descartados pelo plugin; backfill e
decisao explicita do Gerente, e o segundo e impossivel no fluxo real porque `MarkAsPublished()` nao
permite retorno a `Queued`).

**1. `git pull origin desenv`** — branch ja atualizada (`a1e21a7`), sem divergencia.

**2. Suites completas:**
- `dotnet test` (backend) — **321/321 passando (100%)**, sem falhas, sem skip.
- `npm test` (dashboard, Karma/Chrome headless) — **107/107 passando (100%)**.

**3. Boot Docker real:** `.env` local temporario criado (gitignored, removido ao final) +
`docker compose up -d --build db api dashboard`. Build completo dos 3 servicos sem erro. Containers
`afiliado_db` (healthy), `afiliado_api` (healthy), `afiliado_dashboard` (up) confirmados via
`docker compose ps`. Logs da API sem excecao — migration `AddCaptionToPublicationQueue` aplicada
automaticamente no startup, seed de usuario ok, Hangfire registrado.

**4. Fluxo completo ao vivo (Telegram, ProcessorJob):**
- Login via `POST /api/auth/login` (usuario seed) → token JWT obtido.
- Credenciais de Telegram (fake, so para qualificar a rede) configuradas via
  `PUT /api/settings/telegram.bot_token`, `telegram.channel_id`, `networks.telegram.enabled=true`.
- Produto de teste inserido via SQL direto (`status=Queued`).
- `POST /api/jobs/processor/trigger` (autenticado) executado.
- `psql` confirma: `publication_queue` recebeu 1 entrada (`social_network=Telegram`,
  `status=Scheduled`) com `caption` = "Achei essa oferta: Produto Teste CR Telegram por R$ 20.00 (33%
  OFF)" (68 caracteres, texto real, nao vazio) — CA6/CA15 confirmados fim-a-fim. Redes sem credenciais
  (Youtube/Instagram/TikTok/Facebook) corretamente puladas com warning, sem quebrar o job.

**5. Fluxo completo ao vivo (Facebook Manual):**
- Produto de teste + item de `publication_queue` (`social_network=Facebook`, `status=ManualPending`,
  `caption='Legenda de IA real gerada para Facebook via CR teste'`) inseridos via SQL direto.
- `GET /api/products/{id}` (via container `afiliado_api` e, adicionalmente, via
  `afiliado_dashboard` — mesmo caminho de proxy nginx que o Angular usa em producao) retornou
  `"ai_caption":"Legenda de IA real gerada para Facebook via CR teste"`, distinto de `"description"`.
- `GET /api/queue/manual` (via proxy do dashboard) confirma o item `ManualPending` listado com
  `socialNetwork:"Facebook"` — o mesmo endpoint que `facebook-manual.component.ts` consome
  (`QueueService.listManualPending` + `ProductsService.getById` por item), cujo template renderiza
  `post.product?.ai_caption` (confirmado por leitura do componente/template, dado que o dado de origem
  ja foi validado fim-a-fim via o endpoint real).
- Dados de teste (produtos, itens de fila, settings de Telegram) removidos ao final via SQL.

**6. Migration com dados pre-existentes (retrocompatibilidade):** simulado deliberadamente o cenario de
producao — coluna `caption` removida manualmente, linha de `publication_queue` inserida sem a coluna
(equivalente a uma linha legada anterior a migration), migration reaplicada
(`ALTER TABLE publication_queue ADD caption text NOT NULL DEFAULT ''`). Resultado: linha legada recebeu
`caption=''` (comprimento 0) automaticamente, sem erro, sem perda de dados — confirma que a migration
aditiva e segura para banco com dados pre-existentes, conforme CA1 e a decisao do Gerente (sem
backfill necessario).

**7. Checklist de veto:**
- Compila e sobe: **OK** (build + boot + suite, evidencia acima).
- Integracao real: **OK** — fluxo cruza fronteira real (Postgres via EF Core, containers Docker, HTTP via
  proxy nginx do dashboard), sem mock-only nos caminhos criticos validados ao vivo.
- Conformidade com spec/criterios-aceite: **OK** — CA1, CA5, CA6, CA12, CA13, CA14, CA15, CA16, CA17
  confirmados por leitura do diff + validacao ao vivo (Telegram/Facebook). CA18 (changelog) presente no
  corpo do PR #143. Sem backfill (CA4/decisao do Gerente) — decisao explicita, nao e divergencia.
- Sem teste-lixo, sem segredo commitado: **OK** — `gh pr diff 143` varrido por padroes de segredo
  (api_key/secret/password/token com valor plausivel), nenhum encontrado. `.env` local usado na
  verificacao era temporario, gitignored, removido ao final (nao commitado).
- `.first()`/`.nth()`/`.last()` em specs E2E: **N/A** — este PR nao toca specs Playwright E2E (apenas
  specs Jasmine/Karma unitarios do dashboard: `facebook-manual.component.spec.ts`,
  `products.service.spec.ts`), nenhum uso de seletor estrutural via `.first()`.
- Cobertura: 321 testes backend + 107 testes frontend cobrindo os cenarios criticos (CA13/CA14/CA15/CA16
  incluidos nesta mudanca).
- OWASP Top 10: endpoints tocados (`/api/settings`, `/api/jobs/processor/trigger`, `/api/products/{id}`,
  `/api/queue/manual`) exigem autenticacao JWT (confirmado — chamadas sem token nao testadas
  intencionalmente pois nao fazem parte do escopo desta correcao, mas o middleware de auth ja existente
  nao foi alterado pelo diff).

**Veredito: APROVADO.** Merge `desenv` → `homolog` (merge commit, sem squash) executado:
PR #143. Containers derrubados ao final (`docker compose down -v`), `.env` local removido.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | PM Fase 1 | pm-analista-negocios | sonnet | 29010 | 14 | 95s |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 44474 | 21 | 188s |
| 4 | Refinamento LT | lider-tecnico | sonnet | 107624 | 42 | 359s |
| 5 | Dev Sub-A #139 | dev-dotnet | sonnet | 183618 | 115 | 724s |
| 6 | Merge Sub-A #139 (PR #141) | lt | sonnet | 47158 | 18 | 87s |
| 7 | Dev Sub-B #140 | dev-angular | sonnet | 87255 | 47 | 526s |
| 8 | Merge Sub-B #140 (PR #142) + PR homologacao (PR #143) | lt | sonnet | 47068 | 11 | 129s |
