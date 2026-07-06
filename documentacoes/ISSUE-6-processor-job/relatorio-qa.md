# Relatório de QA — Issue #6 (Processor Job)

## Status: REPROVADO

PR #51 confirmado **MERGED** em homolog (`c08e965`, `mergedAt: 2026-07-06T20:34:50Z`). Branch local
sincronizada via `git fetch` + `git checkout homolog` + `git reset --hard origin/homolog`; commit
`c08e965` confirmado no topo do `git log`.

## 1. Testes unitários

```
dotnet build → 0 erros, 0 avisos
dotnet test  → Aprovado! 80/80, Duração 24s
```

Todos os 80 testes passando, incluindo os que cobrem o fix da sub-issue #52
(`MercadoLivreCollectorTests`, `ProcessorJobTests`).

## 2. Inspeção de código

- `ProcessorJob.cs`: máquina de estados (Queued→Processing→Published/Error), slug condicional,
  categoria, download de mídia com fallback sem exceção, link de afiliado ML via `SourceUrl`
  (fix #52 confirmado — não usa mais `ImageUrl`/`ExternalId`), fila de publicação com Facebook
  `ManualPending` e demais redes `Scheduled` via round-robin, pular redes sem credenciais — tudo
  implementado conforme `criterios-aceite.md`.
- `MercadoLivreCollector.cs`: `MercadoLivreItem.Permalink` capturado de `item.permalink` e
  propagado para `Product.SourceUrl` tanto na criação quanto no upsert — fix #52 confirmado.
- Migrations `AddMediaLocalPathToProducts` e `AddSourceUrlToProducts` presentes e incrementais.
- Testes cobrindo os 21 CAs confirmados por nome/comportamento (`ProcessorJobTests`,
  `LocalMediaStorageTests`, `CategoryDetectorTests`).

## 3. Validação integrada (E2E via Docker) — FALHOU

Subida via `docker compose up -d --build` (repo raiz): todos os 4 containers (`afiliado_db`,
`afiliado_api`, `afiliado_website`, `afiliado_dashboard`) subiram com sucesso.

- `GET /health` → `200 {"status":"healthy",...}` — OK.
- `POST /api/jobs/processor/trigger` → **HTTP 500**.

Log da API (`docker logs afiliado_api`):
```
Npgsql.PostgresException: 28P01: password authentication failed for user "${DB_USER}"
  at ... AfiliadoBot.Application.Jobs.ProcessorJob.ExecuteAsync(...) line 59
```

Reproduzido também **após `docker compose down -v`** (volumes limpos do zero) — não é resíduo de
estado de banco de sessão anterior.

### Causa raiz identificada (mismatch de chave de configuração)

- `docker-compose.yml` define a env var `ConnectionStrings__Default` (→ `ConnectionStrings:Default`
  em runtime), com valores de `${DB_USER}`/`${DB_PASSWORD}` corretamente interpolados
  (confirmado via `docker compose config`, que mostra
  `Username=afiliado;Password=senha_local_dev`).
- `Program.cs` (linha 14) chama `builder.Configuration.GetConnectionString("DefaultConnection")`
  — chave **`DefaultConnection`**, diferente de `Default`.
- Como as chaves não coincidem, a env var do compose nunca é usada. O app cai no
  `appsettings.json`, que tem a chave certa (`DefaultConnection`) mas com **placeholders literais
  nunca resolvidos**: `"Host=db;Port=5432;Database=afiliadoBot;Username=${DB_USER};Password=${DB_PASSWORD}"`
  (ASP.NET não faz interpolação de shell nessa string — o Postgres recebe literalmente o texto
  `${DB_USER}` como username, daí o erro de autenticação).

**Impacto:** qualquer operação que toque o banco falha em ambiente Docker (compose), incluindo o
fluxo completo do `ProcessorJob` (e presumivelmente do `CollectorJob`/demais endpoints com EF).
Isso não é flutuação de infraestrutura local — é um bug de mismatch de nome de chave entre
`docker-compose.yml` e `Program.cs`/`appsettings.json`, presente no código mergeado em homolog.
Bloqueia o critério "d3 — Validação integrada obrigatória" do processo de QA: a aplicação sobe,
mas o fluxo ponta a ponta que usa o banco real falha.

## 4. Critérios de aceite

CA1–CA21 têm cobertura unitária correta (ver seção 2), mas **não puderam ser confirmados via
fluxo integrado real** porque o endpoint que dispara o `ProcessorJob` falha por erro de config de
conexão com o banco. Como o processo de QA exige rodar a aplicação de ponta a ponta (não apenas
testes unitários/mock) antes de aprovar, o resultado é reprovação.

| CA | Cobertura unitária | Validação integrada (Docker) |
|---|---|---|
| CA1–CA21 | OK (80/80 testes) | Bloqueado — `POST /api/jobs/processor/trigger` retorna 500 (falha de conexão com banco) |

## 5. E2E / Screenshots

N/A — projeto backend sem UI web pública testável via Playwright (`test:visual` não aplicável a
este repo; sem `package.json` de frontend E2E no escopo desta issue).

## Conclusão

Reprovado. Causa: mismatch entre a chave de connection string usada em `docker-compose.yml`
(`ConnectionStrings__Default`) e a chave lida em `Program.cs`
(`GetConnectionString("DefaultConnection")`), fazendo o app cair no `appsettings.json` com
placeholders `${DB_USER}`/`${DB_PASSWORD}` nunca resolvidos. Resultado: toda operação que usa o
banco falha (`28P01: password authentication failed for user "${DB_USER}"`) em ambiente Docker
Compose — o ambiente real de homolog/produção.

**Correção sugerida:** alinhar a chave em `docker-compose.yml` para `ConnectionStrings__DefaultConnection`
(ou renomear a leitura em `Program.cs` para `GetConnectionString("Default")`), e nunca deixar
placeholders de shell (`${...}`) direto em `appsettings.json` (usar apenas env vars via
docker-compose, sem duplicar/placeholder no JSON).
