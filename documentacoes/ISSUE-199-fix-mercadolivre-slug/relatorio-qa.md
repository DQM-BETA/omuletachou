# Relatório QA — Issue #199 (rota `rapido`)

**Status: ✅ APROVADO**

Branch validada: `homolog` @ `16bf895a62aa69590c9dfd061eecd03572a731c3` (merge commit do PR #201, confirmado em `git log --oneline` após `git fetch` + `git pull origin homolog` — fast-forward de 11 commits, incluindo `0130af1` fix#1 slug e `12331e8` fix#2 ai_reason).

## Contexto
Dois bugs reais corrigidos nesta issue:
1. `MercadoLivreCollector.GenerateSlug` gerava slug sem truncar, estourando `varchar(300)` da coluna `slug` → `Npgsql.PostgresException 22001` → `SaveChangesAsync` (chamado 1x ao fim do loop duplo de categorias) revertia o ciclo inteiro, perdendo TODOS os produtos do batch.
2. `Product.UpdateAiResult`/`MarkAsError` gravavam `AiReason` (também `varchar(300)`) sem truncar — resposta real da IA (Claude, não mock) podia passar de 300 chars. Achado pelo Code Review numa 1ª rodada de validação ao vivo, corrigido na mesma Issue antes de reacionar QA.

## 1. Suíte de testes
```
dotnet test -c Release
Aprovado! – Com falha: 0, Aprovado: 441, Ignorado: 0, Total: 441, Duração: 42s
```
441/441 conforme esperado.

Cobertura (`--collect:"XPlat Code Coverage"`, cobertura.xml parseado):
- line-rate: **89.4%**
- branch-rate: **80.8%** (>= 80% ✓)

## 2. Build/boot real (rebuild sem cache)
Containers antigos (`afiliado_api`) estavam com imagem anterior ao merge (lição do Code Review anterior) → `docker compose down` + `docker compose build --no-cache api` + `docker compose up -d db api`.
- Build sem cache: sucesso (`Image omuletachou-api Built`).
- Boot real: migrations "already up to date", Hangfire SQL objects instalados, `Now listening on: http://[::]:8080`.
- `GET /health` → `{"status":"healthy",...}` HTTP 200.

## 3. Validação integrada ao vivo (fluxo ponta a ponta, dados reais)
- Login real: `POST /api/auth/login` com credenciais de `.env` (`SEED_USER_EMAIL`/`SEED_USER_PASSWORD`) → token JWT obtido.
- Trigger real do coletor: `POST /api/jobs/collector/mercadolivre/trigger` com `Authorization: Bearer <token>` contra a API real do Mercado Livre.
  - Duração: 126s.
  - **Resultado: HTTP 200, `{"count":117}`** — antes do fix: HTTP 500, zero produtos salvos.
- Logs do container (`docker logs afiliado_api --since 10m`): zero ocorrências de `"value too long"` / `22001` / `varchar(300)`. Único erro presente: `MercadoLivreApiException: Resposta HTTP 404 em .../MLB75526622/items` — isolamento de falha por item pré-existente (Issue #182/#190), não regressão; não derruba o ciclo.

## 4. Verificação direta no Postgres
```sql
SELECT COUNT(*) FILTER (WHERE platform=1) AS total_ml,
       MAX(LENGTH(slug)) AS max_slug_len, COUNT(*) FILTER (WHERE LENGTH(slug)>300) AS slug_over_300,
       MAX(LENGTH(ai_reason)) AS max_ai_reason_len, COUNT(*) FILTER (WHERE LENGTH(ai_reason)>300) AS ai_reason_over_300
FROM products WHERE platform=1;
```
| total_ml | max_slug_len | slug_over_300 | max_ai_reason_len | ai_reason_over_300 |
|---|---|---|---|---|
| 117 | 211 | **0** | 300 | **0** |

Checagem global (todas as plataformas): `slug_over_300_any_platform = 0`, `ai_reason_over_300_any_platform = 0`.

Evidência qualitativa:
- Slug mais longo real persistido (211 chars): `fones-de-ouvido-usb-c-tipo-c-com-microfone-integrado-som-est...` — dentro do limite, sufixo do `externalId` preservado (`-mlb...`).
- 1 produto com `ai_reason` truncado exatamente em 300 chars (resposta real do Claude cortada em `...avaliaç`) — evidência forte de que a truncagem `SetAiReason` está ativa em produção com dados reais, não só em teste/mock.

## 5. Inspeção qualitativa de código e testes (TDD)
- `MercadoLivreCollector.GenerateSlug` (linhas 543-576): trunca apenas a parte derivada do título, preservando o sufixo `-{externalId}` intacto (garante unicidade — `IX_products_slug` é UNIQUE). Teste `CollectAsync_TruncaSlug_QuandoTituloGeraSlugMaiorQue300Caracteres` usa título real >300 chars, assere `Slug.Length <= 300` e `EndWith("-mlb88888888")`.
- `Product.SetAiReason` (linha 141, `MaxAiReasonLength=300`): reaproveitado por `UpdateAiResult` e `MarkAsError`. Testes `UpdateAiResult_TruncaAiReason_QuandoMaiorQue300Caracteres`, `UpdateAiResult_NaoTruncaAiReason_QuandoDentroDoLimite`, `MarkAsError_TruncaAiReason_QuandoMaiorQue300Caracteres`, `MarkAsError_NaoTruncaAiReason_QuandoDentroDoLimite` — cobrem truncagem e não-truncagem para ambos os métodos.
- Nomes de teste e asserções condizem com o comportamento esperado; nenhuma contradição encontrada.

## 6. Gate visual / E2E Playwright
`test:visual` existe em `dashboard/package.json` e `website/package.json` (repo tem UI). Porém o diff completo desta Issue (#199, ambos os fixes) toca **exclusivamente** arquivos backend:
```
backend/src/AfiliadoBot.Domain/Entities/Product.cs
backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/MercadoLivreCollector.cs
backend/src/AfiliadoBot.Tests/Domain/ProductTests.cs
backend/src/AfiliadoBot.Tests/Integrations/MercadoLivreCollectorTests.cs
```
Nenhum arquivo de `dashboard/` ou `website/` foi alterado — confirmado via `git diff --stat` entre os commits de fix. Não é julgamento de plataforma ("é backend, logo N/A"); é constatação objetiva de que o diff desta issue não contém superfície de UI a validar.

`E2E/screenshots: N/A (diff da Issue #199 não toca dashboard/ nem website/ — 100% backend, sem UI a validar)`

## Tabela de critérios de aceite (issue #199)

| Critério | Evidência | Status |
|---|---|---|
| Slug truncado para caber em varchar(300), preservando unicidade (sufixo externalId) | Teste `CollectAsync_TruncaSlug_QuandoTituloGeraSlugMaiorQue300Caracteres` (TDD, título real >300 chars) + Postgres: `max_slug_len=211`, `slug_over_300=0` | ✅ |
| TDD com título real longo o suficiente para estourar o limite antes da truncagem | `tituloLongo.Length.Should().BeGreaterThan(300)` (precondição do cenário) | ✅ |
| Ciclo de coleta não perde todos os produtos por um único slug estourado | Trigger real: HTTP 200, `count:117` (antes: HTTP 500, zero produtos) | ✅ |
| (2º bug, mesma issue) `ai_reason` truncado para varchar(300) em `UpdateAiResult`/`MarkAsError` | Testes dedicados + Postgres: `max_ai_reason_len=300`, `ai_reason_over_300=0`, evidência real de truncagem em resposta real do Claude | ✅ |
| Build/boot real da stack (rebuild sem cache) | `docker compose build --no-cache api` + boot OK + `/health` 200 | ✅ |
| Suíte de testes 100% verde | 441/441 | ✅ |
| Cobertura de branch >= 80% | 80.8% | ✅ |

**100% dos critérios validados. Nenhuma issue funcional ou de negócio encontrada.**
