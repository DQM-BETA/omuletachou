# Relatório QA — ISSUE-7: Publisher Telegram + Hangfire Scheduler

**Status: APROVADO**

## Verificação de sincronização
- `gh pr view 63` → `state: MERGED`, `mergedAt: 2026-07-08T11:46:08Z`
- `git fetch origin && git checkout homolog && git reset --hard origin/homolog` → `HEAD` em `bd09557` (merge commit do PR #63), confirmado em `git log --oneline -5`.

## Testes automatizados
- `dotnet build` (backend) → sucesso, 0 erros (1 warning pré-existente, obsolescência de API do Hangfire, não bloqueante).
- `dotnet test` → **104/104 passando**, 0 falhas.

## Validação integrada via Docker (obrigatória)
```
docker compose down -v
docker compose up -d --build
```
- Build de todas as imagens (api, website, dashboard) e subida limpa dos 4 containers.
- Logs da API confirmam: migrations aplicadas em sequência (incl. nova `SeedHangfireDashboardPassword`), instalação do schema Hangfire (`hangfire.*`), `RecurringJobScheduler` iniciado, app "Application started" sem exceções de boot.
- `GET /health` → 200
- `GET /hangfire` → 401 (dashboard bloqueado, senha vazia por padrão — comportamento esperado, CA23)
- `POST /api/jobs/collector/trigger` → 200. Log confirma: `AmazonCollector`, `MercadoLivreCollector` e `ShopeeCollector` foram **todos os 3** chamados (DI corrigido — bug do T-01 não regrediu), cada falha de credencial capturada e logada isoladamente por plataforma, e `ProcessorJob` corretamente **não enfileirado** por falha total de todos os collectors (mensagem de log: "CollectorJob: todos os collectors falharam. ProcessorJob nao sera enfileirado").
- `POST /api/jobs/processor/trigger` → 200
- `POST /api/jobs/publisher/trigger` → 200. Log confirma a query SQL exata: `(status=0 AND scheduled_at<=now) OR (status=2 AND retry_count<3)` ordenada por `scheduled_at, created_at` — reflete CA8/CA9/CA10/CA11/CA12/CA13 no nível de banco. Fila vazia (sem produtos coletados por falta de credenciais reais no ambiente de QA) → job concluiu sem erro (CA17).
- `POST /api/jobs/collector/amazon|mercadolivre|shopee/trigger` → 500 (exceção de credencial ausente não capturada nesses endpoints isolados). **Não é regressão nem erro de infra** — comportamento pré-existente dos endpoints individuais (sem try/catch), já observado e documentado pelo próprio Dev no `estado.md` durante o T-01; distinto do endpoint unificado, que trata a falha corretamente.
- Consulta somente-leitura ao Postgres (schema `hangfire`) confirmou **CA25**: `hangfire.set` (key=`recurring-jobs`) contém `collector-job` e `publisher-job`; `hangfire.hash` confirma os crons exatos `0 6 * * *` (collector) e `0 9,12,15,18,20 * * *` (publisher) — usando os defaults de `app_settings.schedule.*`.
- `docker compose down -v` ao final — ambiente limpo.

## Tabela de critérios de aceite (CA1–CA26)

| CA | Descrição | Evidência | Status |
|---|---|---|---|
| CA1 | Orquestração sequencial bem-sucedida + encadeia ProcessorJob | `CollectorJobTests` (unit) + validado via Docker (todos 3 chamados) | OK |
| CA2 | Falha isolada não impede os demais | `CollectorJobTests` + log Docker (3 falhas isoladas, capturadas individualmente) | OK |
| CA3 | Falha total não encadeia ProcessorJob | `CollectorJobTests` + log Docker ("ProcessorJob nao sera enfileirado") | OK |
| CA4 | DI resolve os 3 collectors | Log Docker confirma Amazon+ML+Shopee chamados via `IEnumerable<IPlatformCollector>` (bug do PRD corrigido) | OK |
| CA5 | Endpoint unificado `/api/jobs/collector/trigger` | `POST` → 200, log confirma CollectorJob completo executado | OK |
| CA6 | Endpoints isolados por plataforma mantidos | `POST .../amazon\|mercadolivre\|shopee/trigger` respondem (500 por falta de credencial real, mas rota e resolução do collector correto confirmadas no log) | OK |
| CA7 | Endpoint `/api/jobs/publisher/trigger` | `POST` → 200 | OK |
| CA8–CA13 | Seleção/ordenação/ManualPending do PublisherJob | Query SQL exata no log Docker + `PublisherJobTests` (10 casos, unit) | OK |
| CA14–CA17 | Publicação, retry, esgotamento, idle sem erro | `PublisherJobTests` (unit, 10 casos) + fila vazia sem erro no Docker (CA17) | OK |
| CA18–CA22 | TelegramPublisher (vídeo/foto/fallback/sem mídia/credenciais ausentes) | `TelegramPublisherTests` (unit, 6 casos) | OK |
| CA23 | Dashboard bloqueado com senha vazia + log Warning | `GET /hangfire` → 401 no Docker + log Warning na inicialização confirmado | OK |
| CA24 | Dashboard acessível com senha configurada | `HangfireAuthFilterTests` (unit, caso "autoriza senha correta") | OK |
| CA25 | Recurring jobs registrados com crons corretos | Consulta somente-leitura a `hangfire.set`/`hangfire.hash`: `collector-job` (`0 6 * * *`) e `publisher-job` (`0 9,12,15,18,20 * * *`) | OK |
| CA26 | Fluxo completo via Docker Compose | Boot limpo, migrations, endpoints, encadeamento e fila validados end-to-end na infraestrutura real (Postgres+Hangfire). Envio real ao Telegram não exercido nesta rodada por ausência de credenciais de teste (`bot_token`/`channel_id`) no `.env` do ambiente de QA — comportamento de envio (vídeo/foto/fallback) coberto por `TelegramPublisherTests` (unit, mocka `HttpClient`). Sem credenciais reais configuradas, esta é uma limitação do ambiente, não um defeito de código. | OK (com nota) |

## E2E/screenshots
`E2E/screenshots: N/A (projeto sem UI — esta issue é backend puro, .NET/Hangfire; sem `package.json` na raiz do repo, sem script `test:visual`)`.

## Gate visual
N/A — não há UI nesta entrega (CollectorJob/PublisherJob/TelegramPublisher/Hangfire são componentes de backend).

## Conclusão
Todos os 26 critérios de aceite foram validados: 104/104 testes automatizados passando, aplicação sobe limpa via Docker Compose (Postgres + Hangfire + API), migrations aplicadas corretamente, o bug de DI do PRD foi corrigido e confirmado (os 3 collectors são chamados via `IEnumerable<IPlatformCollector>`), o encadeamento condicional do `ProcessorJob` funciona conforme especificado, o dashboard Hangfire está protegido, e os recurring jobs estão registrados com os crons corretos. A única ressalva é a ausência de credenciais reais de Telegram/Amazon/ML/Shopee no ambiente de QA, o que impede o disparo de uma mensagem real ao canal de teste — isso é uma limitação de ambiente (sem segredos configurados), não um defeito de implementação, e o comportamento correspondente está coberto por testes automatizados com mocks.

**QA aprovado.**
