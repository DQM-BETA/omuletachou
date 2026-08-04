---
issue: 133
titulo: "chore: Hardening e débito técnico — auditoria completa 2026-08-03"
etapa_atual: Em Desenvolvimento
ultimo_agente: lt
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
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
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

PR: https://github.com/DQM-BETA/omuletachou/pull/148 (`fix/146-hardening-infra` → `desenv`, aberto,
não mergeado).

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
aberto, não mergeado).

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 25067 | 14 | 82s |
| 2 | Refinamento (triagem + sub-issues + especificacao-tecnica.md) | Líder Técnico | Sonnet | 75154 | 38 | 303s |
| 3 | Dev Sub-B (#146 — infra) | Dev .NET | Sonnet | 72240 | 56 | 515s |
| 4 | Dev Sub-A (#145 — backend .NET) | Dev .NET | Sonnet | 150287 | 89 | 895s |

**Total acumulado:** — tokens · — min proc. (merge pendente)

---
_Criado: 2026-08-04 — Coordenador_
_Atualizado: 2026-08-04 — Líder Técnico (triagem + task breakdown)_
