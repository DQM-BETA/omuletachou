---
issue: 133
titulo: "chore: Hardening e débito técnico — auditoria completa 2026-08-03"
etapa_atual: QA
ultimo_agente: code_review
status_comment_id: 5178622317
openspec_change: ~
tech_stacks:
  - dotnet
  - angular
  - infra
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-133-hardening-debito-tecnico
openspec_path: ~
sub_issues:
  - "#145 (stack:dotnet, task_id:Sub-A)"
  - "#146 (infra, task_id:Sub-B)"
  - "#147 (stack:angular, task_id:Sub-C)"
desenv_tasks_merged: ["#145", "#146", "#147"]
sub_issues_frontend: {}
pr_homologacao: 151
pr_release: ~
code_review_homolog_pr: 151
qa_status: ~
figma_url: ~
blockers: nenhum
createdAt: "2026-08-04"
closedAt: ~
---

## Descrição
Consolidação de achados não-bloqueantes da auditoria completa de código (Code Review) + teste
funcional (QA) pedida pelo Gerente em 2026-08-03. Achados categorizados por tema:

- **Segurança**: DELETE sem rate-limiting, senha com comparação não tempo-constante, SSRF, header forwarding
- **Dependências vulneráveis**: Angular, next-pwa, Newtonsoft.Json com vulnerabilidades High
- **Infraestrutura**: .gitignore bloqueando .dockerignore, deploy sem healthcheck, imagens sem pin de versão
- **Qualidade de código**: Código morto (Class1.cs, testes boilerplate)
- **Lacuna funcional**: ProcessorJob com falsa sensação de "publicado", Facebook credentials não seedadas

## Triagem (LT, 2026-08-04) — issue puramente técnica, sem PM/Arquiteto

O Gerente autorizou ("resolva") — triagem feita diretamente pelo LT, dado que não há ambiguidade
de negócio nem de arquitetura em nenhum destes itens (fixes técnicos objetivos).

### Fazer agora (baixo risco, mecânico, alto valor) → Sub-A/B/C

| Item | Por quê agora |
|---|---|
| Rate-limit em `unsubscribe` | Policy `PublicWritePolicy` já existe e já é usada em `subscribe`/`vapid-public-key` — só falta o atributo. Zero risco, 1 linha. |
| `HangfireAuthFilter` timing-safe + lockout | Vulnerabilidade real (timing attack) e endpoint administrativo sensível (`/hangfire` expõe todos os jobs). Fix mecânico (`CryptographicOperations.FixedTimeEquals` + contador em memória), sem mudança de contrato externo. |
| SSRF allowlist em `LocalMediaStorage` | Defesa em profundidade barata (checagem de IP antes do download). Risco de regressão baixo — só rejeita ranges que nunca deveriam ser mídia legítima de produto. |
| `dashboard/nginx.conf` X-Forwarded-* | 3 linhas de config, sem risco — hoje "funciona por acidente" via NPM, tornar explícito remove fragilidade do rate-limiting por IP real. |
| `.gitignore`/`.dockerignore` | Puramente aditivo (remove 1 linha do gitignore, cria 3 arquivos novos). Sem risco de regressão. |
| `deploy.sh` healthcheck | Script de operação, não código de produção — falha segura (para de reportar sucesso falso). Baixo risco, alto valor operacional (1º deploy real ainda vai acontecer). |
| Pin de versão `postgres`/NPM | Mecânico (troca de tag), reduz risco de drift silencioso em `docker compose pull` futuro. |
| `Class1.cs` mortos | Código nunca referenciado desde o scaffold inicial — remoção sem risco. |
| `app.component.spec.ts` boilerplate | Teste placeholder sem valor de regressão real — remoção/substituição sem risco. |
| `ProcessorJob.MarkAsPublished()` incondicional | Bug real de domínio (produto marcado "Published" sem nada enfileirado, distorce Reports). Fix contido: reaproveita `ProductStatus.Error` já existente, sem introduzir novo status nem migração de schema. |
| Seed `facebook.access_token`/`facebook.page_id` | Mesmo padrão já usado 2x (Instagram/YouTube) — sem essa seed, a lacuna funcional acima (A4) não pode nem ser testada fim-a-fim para Facebook (rede nunca teria credenciais para qualificar). Migration mecânica, ids 49/50 confirmados livres. |
| `Newtonsoft.Json` transitivo | Investigado: vem só do `Hangfire.Core` (não há referência direta em nenhum `.csproj`). Fix é 1 `PackageReference` direto pinado em 13.0.3 — mecânico, baixo risco, dentro da mesma major. Por isso migrou de "fora de escopo" (proposto pelo Gerente) para "fazer agora" (critério do próprio brief: "se for fix simples, mova para fazer agora"). |

### Fora de escopo desta rodada (documentar por quê)

| Item | Por quê fica de fora |
|---|---|
| Upgrade Angular `17.3.0` → 18/19 | Breaking change real (major bump, migração de APIs, possível rework de templates/testes do dashboard inteiro). Esforço desproporcional a uma rodada de hardening — precisa de sprint dedicado com regressão completa do dashboard. As 10 vulnerabilidades High são de XSS em libs internas do Angular CLI/build, não expostas diretamente por input de usuário não sanitizado conhecido — risco real mitigado, urgência menor que o esforço. |
| Upgrade/substituição `next-pwa` | Cadeia transitiva (`serialize-javascript`) é RCE, mas **só em build-time** (nunca roda em produção com input de usuário) — risco real baixo apesar da severidade "High" do scanner. Trocar a lib de PWA do Next.js é mudança arquitetural (avaliar alternativas como `@ducanh2912/next-pwa` ou Workbox direto), não um bump de versão simples. Precisa de avaliação própria antes de decidir a lib substituta. |
| Backup automatizado do volume `postgres_data` | Decisão operacional/infra que depende da VM real de produção (Oracle Cloud ARM) ainda não provisionada (Issue #15, backlog). Definir estratégia (cron + `pg_dump` + storage externo, snapshot de volume, etc.) sem o ambiente real na frente é prematuro — mais adequado quando a #15 for executada na prática. |

### Paralelismo
As 3 sub-issues (#145 backend .NET, #146 infra, #147 frontend/dashboard) não têm dependência
funcional entre si — tocam arquivos/repos-lógicos disjuntos (backend/, docker-compose.yml +
deploy.sh + .gitignore, dashboard/). Podem ser desenvolvidas em paralelo por devs distintos.

## Sub-B (#146) — infra: dockerignore, healthcheck no deploy.sh, pin de imagens

Implementado em worktree isolado (`fix/146-hardening-infra`, base `desenv`). Escopo:

1. Removida a linha `.dockerignore` do `.gitignore` raiz.
2. Criados `backend/.dockerignore`, `dashboard/.dockerignore`, `website/.dockerignore`.
3. `deploy.sh`: aguarda `db`/`api` ficarem `healthy` (poll, 30 tentativas × 2s) antes de imprimir
   "deploy concluído"; falha com `exit 1` se algum ficar `unhealthy`, parar/crashar ou não existir.
4. `docker-compose.yml`: `postgres:16-alpine` → `postgres:16.14-alpine`; `jc21/nginx-proxy-manager:latest`
   → `jc21/nginx-proxy-manager:2.15.1` (últimas tags estáveis no Docker Hub em 2026-08-04).

Validação real (boot Docker): build dos 3 serviços com os `.dockerignore` novos ok; `db`/`api`/
`website`/`dashboard` sobem saudáveis com as imagens pinadas; lógica de espera do `deploy.sh`
extraída e testada nos dois caminhos — sucesso (`healthy` → "deploy concluído") e falha (container
parado → `exit 1`, sem "deploy concluído"). `nginx-proxy-manager` não pôde subir localmente por
conflito de porta 80 no host Windows (ambiente de dev, não relacionado às mudanças) — pin/config
validado via `docker compose config` + resolução da tag no Docker Hub. Ambiente de teste limpo
(`docker compose down -v`) ao final.

PR: https://github.com/DQM-BETA/omuletachou/pull/148 (`fix/146-hardening-infra` → `desenv`,
**MERGED via squash**).

## Sub-A (#145) — backend .NET: rate-limit, timing-safe, SSRF, ProcessorJob, Facebook seed, Newtonsoft.Json

Implementado em worktree isolado (`fix/145-hardening-backend`, base `desenv`). Escopo (7 itens,
todos triados pelo LT como seguros/mecânicos — ver tabela "Fazer agora" acima):

1. `PushController.Unsubscribe`: `[EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)]`
   (mesma policy de subscribe/vapid-public-key).
2. `HangfireAuthFilter`: comparação de senha em tempo constante (SHA-256 dos dois valores +
   `CryptographicOperations.FixedTimeEquals`, evita vazar tamanho/prefixo via timing) + lockout de
   5 tentativas/5min por IP (`ConcurrentDictionary` estático, já que `/hangfire` é middleware de
   Dashboard, não Controller — `[EnableRateLimiting]` não se aplica).
3. `LocalMediaStorage`: allowlist SSRF antes do `GetAsync` — rejeita scheme != http/https e
   qualquer IP resolvido em range privado/loopback/link-local (127.0.0.0/8, 10.0.0.0/8,
   172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16 incluindo o metadata endpoint
   169.254.169.254, além de IPv6 loopback/link-local/ULA `fc00::/7`). Resolução de DNS extraída
   para um `Func<string, CancellationToken, Task<IPAddress[]>>` injetável (construtor `internal`,
   `InternalsVisibleTo` adicionado ao `AfiliadoBot.Infrastructure.csproj`) para manter a suíte
   hermética sem depender de resolução de rede real.
4. Removidos os 3 `Class1.cs` de scaffolding (`Application`/`Domain`/`Infrastructure`).
5. `ProcessorJob.CreatePublicationQueueEntriesAsync` passa a retornar `Task<int>` (contagem de
   entradas efetivamente criadas); `ExecuteAsync` usa `product.MarkAsError(...)` quando zero
   (reaproveita `ProductStatus.Error` existente), `MarkAsPublished()` só quando ≥1 rede qualificou.
   Testes pré-existentes que não seedavam nenhuma rede (e ainda assim esperavam `Published`) foram
   corrigidos para seedar Telegram — gate obrigatório (busca de testes que referenciam o módulo
   modificado).
6. Migration `SeedFacebookCredentials` (ids 49/50, `dotnet ef migrations add` + `InsertData`/
   `DeleteData` manuais seguindo o padrão exato de `SeedInstagramCredentials`).
7. `Newtonsoft.Json` fixado em `13.0.3` via `PackageReference` direto em `AfiliadoBot.Api.csproj`
   (antes só vinha 11.0.1 via transitivo do `Hangfire.Core`).

TDD para os itens 1/2/3/5 (testes novos + regressão dos existentes). `dotnet test`: 336/336
passando (100%) — 1 teste de integração pré-existente (`InstagramPublisherTests.PublishAsync_
PollingContinuaAteFinished_QuandoInProgress`) falhou isoladamente na 1ª rodada da suíte completa
mas passou em isolamento e na 2ª rodada completa; flakiness de timing pré-existente, não
relacionada às mudanças desta sub-issue.

Validação real via boot Docker (`docker compose up -d --build db api`, `.env` local descartável
gerado a partir de `.env.example`, removido ao final): API sobe sem exceção (boot do DI ok,
migração `SeedFacebookCredentials` aplicada); 11ª requisição de `DELETE /unsubscribe` → 429;
Hangfire aceita a senha correta antes do lockout, mas após 5 tentativas erradas do mesmo IP
bloqueia inclusive a senha correta na 6ª tentativa; produto com `media_url=http://127.0.0.1/...`
processado via `POST /api/jobs/processor/trigger` é bloqueado pela allowlist SSRF (log de warning
dedicado, `media_local_path` permanece vazio) mas segue publicado normalmente via Telegram
(rede qualificada); produto sem nenhuma rede habilitada/credenciada vai para
`ProductStatus.Error` com a mensagem exata do critério de aceite; `GET /api/settings` expõe
`facebook.access_token`/`facebook.page_id` mascarados. Ambiente Docker limpo
(`docker compose down -v`) ao final.

PR: https://github.com/DQM-BETA/omuletachou/pull/149 (`fix/145-hardening-backend` → `desenv`,
**MERGED via squash**).

## Sub-C (#147) — frontend/dashboard: nginx headers + limpeza de teste boilerplate

Implementado em worktree isolado (`fix/147-hardening-frontend`, base `desenv`). Escopo:

1. `dashboard/nginx.conf`, location `/api/`: adicionados explicitamente
   `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;` e
   `proxy_set_header X-Forwarded-Proto $scheme;` — `$proxy_add_x_forwarded_for` encadeia (append)
   ao valor já recebido do NPM em vez de sobrescrever, mantendo a cadeia de proxies íntegra para o
   `ForwardedHeadersMiddleware` do backend.
2. `dashboard/src/app/app.component.spec.ts`: removido (opção (a) da especificação técnica).
   Decisão: `AppComponent` não tem responsabilidade própria além de renderizar `<router-outlet>`
   (`app.component.html` é só `<router-outlet></router-outlet>`; a propriedade `title` nunca é
   lida/bindada no template) — os 3 testes eram o boilerplate padrão do `ng new`
   (`should create the app` / `should have the title` / `should render the router outlet`), nunca
   customizados, sem valor real de regressão para este app. Nenhum outro spec referencia
   `AppComponent` (Gate obrigatório verificado via Grep antes do PR).

`ng test --watch=false`: 104/104 passando (100%, eram 107 antes — 3 a menos pelos testes
removidos). `ng build`: produção sem erros (warning de budget pré-existente de 285KB acima do
limite, não relacionado a esta mudança).

Validação real via boot Docker (`db`, `api`, `dashboard`), em projeto Compose isolado
(`-p omuletachou-147`, com `docker-compose.override.yml` temporário removendo `container_name`
fixo dos 3 serviços) para não colidir com a stack `afiliado_*` de outra sub-issue rodando em
paralelo no mesmo host — `.env` local descartável gerado a partir de `.env.example`, removido ao
final junto com o override:
- `nginx -t` dentro do container `dashboard`: config sintaticamente válida.
- Requisição via proxy do dashboard (`/api/public/deals`) chega corretamente na API (200, JSON
  válido) — proxy_pass segue funcionando após o fix.
- Rate-limiter `public-write` (`POST /api/public/push/subscribe`, 10 req/min/IP): 9 requisições em
  400 (validação), 10ª/11ª em 429 — confirma que o IP resolvido pelo `ForwardedHeadersMiddleware` a
  partir do header agora setado explicitamente pelo nginx do dashboard segue particionando
  corretamente por IP real.
- Confirmado também (teste adicional, exec direto no container `dashboard` via `curl -H
  "X-Forwarded-For:..."`) que o `ForwardLimit=1` do backend ignora um `X-Forwarded-For` injetado
  pelo próprio cliente e confia apenas no hop imediato adicionado pelo nginx via
  `$proxy_add_x_forwarded_for` — comportamento de segurança esperado (evita spoofing de IP para
  escapar do rate limit).

Ambiente Docker limpo (`docker compose -p omuletachou-147 down -v`) ao final; worktree removido
após push. Stack `afiliado_*` da sub-issue paralela (#146) não foi tocada pelos comandos desta
sessão (escopo isolado via `-p`/`--network` explícitos).

PR: https://github.com/DQM-BETA/omuletachou/pull/150 (`fix/147-hardening-frontend` → `desenv`,
**MERGED via squash**).

## Merge sequencial das 3 sub-issues (LT, 2026-08-04)

Ordem: #148 (infra) → #149 (backend) → #150 (frontend), um de cada vez, com `git pull origin
desenv` + revalidação de `mergeable` entre cada merge, conforme regra de merge sequencial da
squad. Todos os PRs `mergeStateStatus: CLEAN` antes do merge (squash), sem conflitos — as 3
sub-issues tocam áreas disjuntas (backend/, docker-compose.yml+deploy.sh+.gitignore, dashboard/)
como previsto na triagem. Branches remotas deletadas após cada merge. Sub-issues #145, #146, #147
fechadas com comentário de resumo.

PR de homologação criado: https://github.com/DQM-BETA/omuletachou/pull/151
(`desenv` → `homolog`, merge commit, aguardando Code Review + QA + Gate 2).

## Fix pós-code-review (PR #151) — bypass SSRF via IPv4-mapped-IPv6

O `/code-review` (plugin) rodado no PR #151 (desenv→homolog) encontrou um bug real em
`LocalMediaStorage.IsPublicAddress` (herdado da Sub-A #145): endereços IPv6 mapeados de IPv4 (ex.
`::ffff:169.254.169.254`, `::ffff:10.0.0.1`) têm `AddressFamily.InterNetworkV6` e pulavam
inteiramente as checagens de range IPv4 (metadata endpoint 169.254.169.254, redes privadas
10/8, 172.16/12, 192.168/16), caindo só nas checagens de link-local/site-local/ULA IPv6 — nenhuma
das quais cobre esse caso. Uma resposta DNS hostil retornando esse tipo de endereço bypassava o
allowlist recém-adicionado.

Fix implementado em worktree isolado (`fix/151-ssrf-ipv4-mapped-ipv6`, base `desenv`, já que o bug
está em `desenv` atual): no ramo `AddressFamily.InterNetworkV6` de `IsPublicAddress`, detecta
`address.IsIPv4MappedToIPv6` e, se verdadeiro, desembrulha via `address.MapToIPv4()` e reexecuta
as checagens de range IPv4 (extraídas para o novo método `IsPublicIPv4Address`) antes de aceitar
como público.

TDD: 4 testes de regressão novos em `LocalMediaStorageTests` — `::ffff:169.254.169.254` e
`::ffff:10.0.0.1` bloqueados via IP literal na URL, mesmo cenário via resolução DNS (fake
resolver), e confirmação de que IPv6 público legítimo (`2001:4860:4860::8888`, não mapeado)
continua sendo aceito. `dotnet test`: 340/340 passando (100%). Gate obrigatório (Grep por
referências ao módulo modificado): só o próprio arquivo de teste e `Program.cs` (registro de DI)
referenciam a classe — nada mais a ajustar. `dotnet run` confirma boot do DI/container até a etapa
de migração do banco (falha apenas por ausência de Postgres real no ambiente local de teste,
esperado e não relacionado à mudança).

PR: https://github.com/DQM-BETA/omuletachou/pull/152 (`fix/151-ssrf-ipv4-mapped-ipv6` → `desenv`,
**MERGED via squash** em 2026-08-04). Worktree removido após push.

## Merge do fix SSRF (LT, 2026-08-04)

Revisado o diff do PR #152 antes do merge: fix mínimo e contido — extrai o range-check IPv4 para
`IsPublicIPv4Address` e reusa via `address.MapToIPv4()` no ramo IPv6 quando `IsIPv4MappedToIPv6`;
3 testes de regressão cobrindo IP literal (`::ffff:169.254.169.254`, `::ffff:10.0.0.1`), resolução
DNS fake e o caso positivo (IPv6 público legítimo `2001:4860:4860::8888` continua aceito).

`gh pr merge 152 --squash --delete-branch` executado com sucesso (`mergedAt: 2026-08-04T12:42:34Z`,
branch remota deletada). PR #151 (`desenv` → `homolog`, ainda OPEN) absorveu automaticamente o
commit squash como seu último commit
(`fix(ISSUE-133): desembrulhar IPv4-mapped-IPv6 no allowlist SSRF...`) — mesmo padrão já observado
para #148/#149/#150 e antes para #131/#132; nenhuma ação extra de merge necessária em #151.
`git pull origin desenv` confirma fast-forward local (`747ea97..e46c8e7`); repo_path checked out em
`desenv`, limpo e atualizado ao final desta invocação.

## Code Review — PR #151 (validação final)

Camada 2 (Code Review agente — execução ao vivo, complementar ao `/code-review` estático já rodado
2x no PR, cujo único achado — bypass SSRF IPv4-mapped-IPv6 — já havia sido corrigido no PR #152 e
revalidado em comentário anterior: https://github.com/DQM-BETA/omuletachou/pull/151#issuecomment-5179262074).

**1. Estado do branch:** `git fetch && git checkout desenv && git pull origin desenv` — HEAD em
`d13332e` (mais recente que o `d13332e` esperado), com `e46c8e7` (fix SSRF) presente no histórico.

**2. Build/boot Docker real:** `.env` local descartável gerado a partir de `.env.example`
(credenciais dummy, `SEED_USER_EMAIL`/`SEED_USER_PASSWORD` para permitir login de teste).
`docker compose up -d --build db api dashboard` — build dos 3 serviços sem erro; `docker compose ps`
confirma `db` e `api` `healthy`, `dashboard` `Up` (sem healthcheck definido). Logs do `api` sem
exceção de boot/DI; migrações aplicadas até `20260804120430_SeedFacebookCredentials` (confirmado
via `__EFMigrationsHistory` e `SELECT` em `app_settings` — ids 49/50 `facebook.access_token`/
`facebook.page_id` presentes com valor vazio, id 32 `hangfire.dashboard_password` presente vazio);
seed do usuário operador (`cr-test@example.com`) executado com sucesso.

**3. Suítes de teste:**
- `dotnet test` (backend): **340/340 aprovados**, 0 falhas, ~25s.
- `ng test --watch=false --browsers=ChromeHeadless` (dashboard): **104/104 aprovados**, 0 falhas.
Sem regressão em relação ao esperado (340+/104+).

**4. Validação funcional ao vivo dos pontos críticos:**
- **Rate-limit `unsubscribe`**: 11 requisições `DELETE /api/public/push/unsubscribe?endpoint=...`
  do mesmo IP (via `curl` de dentro do container `api`, endpoints distintos por request para não
  colidir com dedupe de negócio) — requisições 1–10 → `204`, 11ª → `429`. Confirma
  `PublicWritePolicy` (10 req/min/IP) aplicada corretamente ao endpoint.
- **Hangfire lockout**: senha configurada em `app_settings` via SQL direto
  (`CorrectHangfirePass123`). 5 tentativas com senha errada seguidas do mesmo IP → `401` cada uma;
  6ª tentativa **com a senha correta** → ainda `401` (lockout ativo, janela de 5 min). Sanity check
  adicional: reiniciado o container `api` (limpa o `ConcurrentDictionary` em memória) — senha
  correta em estado "fresco" → `200`; senha errada isolada → `401`. Confirma tanto o
  comportamento normal quanto o lockout timing-safe.
- **SSRF allowlist**: dois produtos inseridos diretamente no banco com `status=Queued` (1) —
  `media_url=http://127.0.0.1:8080/health` (loopback) e (2)
  `media_url=http://[::ffff:169.254.169.254]/latest/meta-data/` (IPv4-mapped-IPv6 apontando para o
  metadata endpoint — exatamente o bypass corrigido no PR #152). Login via `POST /api/auth/login`
  para obter JWT, `POST /api/jobs/processor/trigger` disparado com o token. Logs do `api` confirmam
  os dois warnings dedicados (`LocalMediaStorage: URL de midia bloqueada pela allowlist SSRF...`)
  para ambas as URLs; `SELECT media_local_path FROM products` confirma que nenhum arquivo foi
  baixado para nenhum dos dois produtos (campo vazio). Validação ao vivo do exato bypass
  encontrado pelo `/code-review` — comprovadamente fechado no ambiente real, não só nos testes
  unitários mockados.

**5. Checklist de veto:**
- Sem segredos commitados: `gh pr diff 151` inspecionado — únicas ocorrências de padrões
  sensíveis (`amazon.secret_key`, `youtube.api_key`, `claude.api_key`) são chaves de configuração
  seedadas com `Value = ""` (mesmo padrão de todas as outras migrations de seed já existentes),
  não segredos reais.
- Conformidade com `repos/omuletachou/CLAUDE.md`: stack, convenções de branch/commit e estratégia
  de merge (squash feature→desenv, merge commit desenv→homolog) seguidas.
- Integração real: toda a validação funcional (item 4) foi feita contra containers Docker reais
  (Postgres real, API real, Hangfire real com storage Postgres real) — não há mock-only nos
  caminhos críticos desta issue. Os testes automatizados (`dotnet test`/`ng test`) usam mocks onde
  apropriado (unitários), mas a suíte inclui testes de integração reais (ex.
  `PushSubscribeRateLimitIntegrationTests`, `HangfireAuthFilterTests`) e esta validação ao vivo
  complementa com o boot real end-to-end.
- Sem OWASP Top 10 introduzido: os 3 itens de segurança desta issue (rate-limit, timing-safe +
  lockout, SSRF allowlist) são fixes de hardening, não introduzem superfície nova.
- `.first()`/`.nth()`/`.last()` em specs E2E: `gh pr diff 151` não contém nenhum spec Playwright
  (`.spec.ts` de E2E) — nenhum arquivo E2E tocado nesta PR. Item não aplicável.
- Diff coerente com o escopo descrito (backend .NET #145, infra #146, frontend #147 + fix SSRF
  #152 absorvido) — confirmado via `gh pr diff --json files`/lista de arquivos tocados.

**6. Achados do `/code-review` (plugin, análise estática):** 1 achado real (bypass SSRF
IPv4-mapped-IPv6), já corrigido no PR #152 e revalidado em comentário próprio no PR. Nenhum outro
achado pendente.

**7. Ambiente limpo:** `docker compose down -v` executado ao final (containers, network e volumes
`postgres_data`/`media_files` removidos); `.env` local removido. `repo_path` checked out em
`desenv` (confirmado via `git branch --show-current`), working tree limpo.

**Veredito: APROVADO.** `gh pr merge 151 --repo DQM-BETA/omuletachou --merge` executado com
sucesso (`mergeCommit: d96b5ec`, `mergedAt: 2026-08-04T12:54:47Z`, `state: MERGED`). Card
permanece em Em Desenvolvimento; próxima etapa: QA.

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 25067 | 14 | 82s |
| 2 | Refinamento (triagem + sub-issues + especificacao-tecnica.md) | Líder Técnico | Sonnet | 75154 | 38 | 303s |
| 3 | Dev Sub-B (#146 — infra) | Dev .NET | Sonnet | 72240 | 56 | 515s |
| 4 | Dev Sub-A (#145 — backend .NET) | Dev .NET | Sonnet | 150287 | 89 | 895s |
| 5 | Dev Sub-C (#147 — frontend/dashboard) | Dev Angular | Sonnet | 90943 | 83 | 1276s |
| 6 | Merge sequencial #148/#149/#150 + PR homologação #151 | LT | Sonnet | 50667 | 18 | 192s |
| 7 | Fix code-review (bypass SSRF IPv4-mapped-IPv6, PR #152) | Dev .NET | Sonnet | 53380 | 18 | 187s |
| 8 | Merge PR #152 -> desenv (absorvido em #151) | LT | Sonnet | 50367 | 8 | 119s |
| 9 | Code Review — validação final PR #151 (live, merge desenv->homolog) | code-review | Sonnet | 103850 | 65 | 712s |

**Total acumulado:** — tokens · — min proc. (merge pendente — consolidação na quiescência)

---
_Criado: 2026-08-04 — Coordenador_
_Atualizado: 2026-08-04 — Code Review (PR #151 aprovado e mergeado desenv→homolog, próximo: QA)_
