# Relatório de QA — Issue #6 (Processor Job)

## Status: APROVADO (rodada final)

PR #57 confirmado **MERGED** em homolog (`9d0d04c`, `mergedAt: 2026-07-07T13:15:06Z`, merge commit
`desenv→homolog`). Branch local sincronizada via `git fetch` + `git checkout homolog` +
`git reset --hard origin/homolog`; commit `9d0d04c` confirmado no topo do `git log`.

Esta é a rodada final, após a correção de 2 bugs de infra identificados nas rodadas anteriores:
1. Mismatch de chave de connection string (`ConnectionStrings__Default` vs `DefaultConnection`).
2. Histórico de migrations não aplicado corretamente (squash de migrations + falta de
   `Database.Migrate()` no startup) — corrigido no PR #56 (`fix(infra): consolidar historico de
   migrations em InitialSchema unico + auto-migrate no startup`).

## 1. Testes unitários

```
dotnet build → 0 erros, 0 avisos
dotnet test  → Aprovado! 80/80, Duração 24s
```

Todos os 80 testes passando.

## 2. Validação integrada (E2E via Docker) — PASSOU

```
docker compose down -v
docker compose up -d --build
```

Todos os 4 containers (`afiliado_db`, `afiliado_api`, `afiliado_website`, `afiliado_dashboard`)
subiram com sucesso (verificado com `docker ps`, todos `Up`).

- Porta publicada do `afiliado_api`: `5000:8080` (confirmado via `docker inspect`).
- `GET /health` → `200 {"status":"healthy","timestamp":"2026-07-07T13:17:22.83Z"}` — OK.
- `POST /api/jobs/processor/trigger` → **200**, sem corpo de erro.

Logs da API (`docker logs afiliado_api`) confirmam:
- Migration `20260707125445_InitialSchema` aplicada automaticamente no startup (auto-migrate),
  incluindo criação de todas as tabelas, índices e seed de `app_settings` — sem exceção.
- Após o trigger, o `ProcessorJob` executou a query real contra o Postgres:
  ```sql
  SELECT p.id, ..., p.media_local_path, p.media_type, p.media_url, ...
  FROM products AS p
  WHERE p.status = 1
  ORDER BY p.ai_score DESC
  ```
  Query executada sem erro de schema/conexão — retornou vazio por não haver produtos `Queued`
  no banco (comportamento de negócio esperado em ambiente limpo, não erro de infra).
- Nenhuma exceção (`Npgsql.PostgresException`, erro de autenticação, etc.) nos logs completos da
  API do início ao fim do teste.

Verificação direta do schema via `psql` dentro do container `afiliado_db`:
```
SELECT * FROM "__EFMigrationsHistory";
 MigrationId                    | ProductVersion
 20260707125445_InitialSchema   | 8.0.11
(1 row)
```
Única migration consolidada (confirma CA20 — sem duplicidade/squash quebrado).

```
\d products (trecho)
 media_url        | text
 media_type       | character varying(20)
 media_local_path | text   (nullable, presente)
```
Coluna `media_local_path` presente e nullable, exatamente como especifica CA20.

Logs de `afiliado_dashboard` (nginx) e `afiliado_website` (Next.js) sem erros — ambos operacionais.

Ambiente limpo ao final: `docker compose down -v` executado com sucesso.

## 3. Critérios de aceite

| CA | Descrição | Cobertura unitária | Validação integrada |
|---|---|---|---|
| CA1 | Download com sucesso | OK | Coberto (fluxo não exercitado por falta de produto Queued em ambiente vazio — validado via unitários) |
| CA2 | Detecção de vídeo por extensão | OK | idem |
| CA3 | Falha no download não bloqueia processamento | OK | idem |
| CA4 | Queued → Processing | OK | idem |
| CA5 | Conclusão com sucesso → Published | OK | idem |
| CA6 | Falha não recuperável → Error | OK | idem |
| CA7 | Rejected nunca processado | OK | idem |
| CA8 | Slug preenchido preservado | OK | idem |
| CA9 | Slug nulo gerado | OK | idem |
| CA10 | Detecção de categoria por palavra-chave | OK | idem |
| CA11 | Fallback sem match → Geral | OK | idem |
| CA12 | Preenchimento automático via API real ML | OK | idem |
| CA13 | Amazon/Shopee sem nova chamada | OK | idem |
| CA14 | Falha na chamada de link → Error | OK | idem |
| CA15 | Facebook sempre ManualPending | OK | idem |
| CA16 | Demais redes Scheduled com ScheduledAt futuro | OK | idem |
| CA17 | Round-robin por AiScore desc | OK | idem |
| CA18 | Rede sem credenciais é pulada | OK | idem |
| CA19 | Uma entrada por rede habilitada/com credenciais | OK | idem |
| CA20 | Migration incremental aplicada | OK | **Confirmado via psql direto no container** — coluna `media_local_path` presente, migration única no histórico |
| CA21 | CollectorJob enfileira ProcessorJob (Hangfire) | OK | Coberto via unitários |

Todos os 21 critérios têm cobertura de teste unitário (80/80 passando) e o ambiente real (Docker +
Postgres real) sobe, aplica as migrations corretamente e responde aos endpoints sem qualquer erro
de infraestrutura — os dois bugs que bloquearam as rodadas anteriores (connection string e
squash de migrations) estão confirmadamente corrigidos.

## 4. E2E / Screenshots

E2E/screenshots: N/A (projeto backend sem UI web pública testável via Playwright — `test:visual`
não presente no escopo desta issue).

## Conclusão

**APROVADO.** Build limpo, 80/80 testes unitários passando, aplicação sobe via Docker Compose com
banco real, migration `InitialSchema` aplicada automaticamente e corretamente (schema com
`media_local_path` confirmado via inspeção direta do Postgres), endpoints `/health` e
`/api/jobs/processor/trigger` respondendo 200 sem qualquer erro de conexão/schema. Os bugs de
infra das rodadas anteriores (mismatch de connection string e histórico de migrations quebrado)
estão resolvidos e validados neste ambiente integrado real.
