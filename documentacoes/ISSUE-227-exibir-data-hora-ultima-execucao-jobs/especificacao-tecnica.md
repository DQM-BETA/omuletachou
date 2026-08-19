# Especificação Técnica — ISSUE-227: Exibir data/hora da última execução de cada job

> Decisões de arquitetura em `openspec/changes/issue-227-exibir-data-hora-ultima-execucao-jobs/design.md`
> (Arquiteto). Este documento traduz as decisões em contratos e critérios de implementação para os
> devs, e resolve os 2 pontos marcados `[LT CONFIRMAR AO VIVO]` no design.

## 1. Decisões pendentes resolvidas pelo LT

### 1.1 Estado "em andamento" na tela (design §2.3)
**Decisão: NÃO exibir chip/indicador dedicado de "Em andamento" nesta issue.** Nenhum cenário de
`criterios-aceite.md` testa esse estado. Como os 6 jobs executam de forma síncrona dentro da própria
requisição (manual) ou do worker Hangfire (agendado), a janela em que `GET /api/jobs/last-executions`
retornaria `Status: "running"` para a MESMA execução que está sendo exibida é, na prática, apenas
quando outra aba/sessão consulta a tela enquanto um disparo está em andamento em paralelo — caso raro
e fora do escopo dos critérios. **Tratamento mínimo obrigatório (robustez, não feature nova):** o
template/lógica de mapeamento de status no frontend deve tratar `"running"` de forma não-quebrada
(ex.: rótulo neutro "Em execução...", sem ícone de sucesso nem de falha) — apenas para não deixar o
card em estado indefinido/quebrado caso o valor apareça; não é necessário desenho visual dedicado
(sem spinner, sem polling). Backend não muda nada (o enum `JobRunStatus.Running` já existe no design
por necessidade de domínio, independente da exibição).

### 1.2 Refetch após disparo manual (design §5, linha `jobs.component.ts`)
**Decisão: SIM, refazer o fetch de `getLastExecutions()` após um `trigger()` bem-sucedido**, mesclando
o resultado no `JobButton[]` correspondente. Justificativa: o design já recomendava isso para não
exigir F5; o custo é uma chamada HTTP extra (mesmo endpoint usado no `ngOnInit`), e evita que o
operador dispare um job e continue vendo o card sem refletir a execução recém-concluída até o próximo
refresh manual. Não é obrigatório por nenhum critério de aceite (todos os cenários usam "abre ou dá
refresh"), mas está dentro do escopo natural da sub-issue de frontend e evita uma lacuna óbvia de UX.
Implementação: no `next`/`error` do `subscribe` de `trigger()` (ambos os branches, já que uma
execução pode terminar como `Failed` mesmo com a chamada HTTP retornando 200 seguida de erro — na
prática hoje o controller só retorna erro HTTP em `InvalidOperationException` de credenciais, que nem
chega a criar/atualizar via `IJobRunTracker` de forma incompleta; refetch em ambos os branches é o
comportamento mais simples e correto), chamar novamente `getLastExecutions()` e mesclar.

## 2. Contrato da API (backend → frontend)

`GET /api/jobs/last-executions` (novo endpoint em `JobsController`, mesma proteção `[Authorize]` da
classe, sem parâmetros, sempre retorna array com as 6 entradas — uma por `JobName`):

```json
[
  {
    "jobName": "collector",
    "status": "success",
    "startedAt": "2026-08-19T10:00:00Z",
    "finishedAt": "2026-08-19T10:02:15Z",
    "errorMessage": null
  },
  {
    "jobName": "collector-amazon",
    "status": null,
    "startedAt": null,
    "finishedAt": null,
    "errorMessage": null
  }
]
```

- `jobName`: slug kebab-case, mesmo valor de `JobKind` do frontend (`collector` |
  `collector-amazon` | `collector-mercadolivre` | `collector-shopee` | `processor` | `publisher`).
- `status`: `"running" | "success" | "failed" | null` (null = nunca executado).
- `startedAt`/`finishedAt`: ISO-8601 UTC, `finishedAt` pode ser `null` quando `status = "running"`.
- `errorMessage`: preenchido apenas quando `status = "failed"`; `null` nos demais casos.
- Ordem do array: fixa, seguindo `Enum.GetValues<JobName>()` (mesma ordem do enum em §2.1 do
  design.md) — o frontend casa por `jobName`, não por posição, então a ordem não é contrato rígido,
  mas manter estável evita diffs desnecessários em snapshots de teste.

Mapeamento `JobName` (C#) → slug (`JobNameSlugs`, mapeamento explícito, não `JsonStringEnumConverter`):
| `JobName` (enum) | slug |
|---|---|
| `Collector` | `collector` |
| `CollectorAmazon` | `collector-amazon` |
| `CollectorMercadoLivre` | `collector-mercadolivre` |
| `CollectorShopee` | `collector-shopee` |
| `Processor` | `processor` |
| `Publisher` | `publisher` |

Mapeamento `JobRunStatus` (C#) → string da API: `Running` → `"running"`, `Success` → `"success"`,
`Failed` → `"failed"` (lowercase, mesmo padrão kebab/lowercase já usado em `PublicationStatus` etc. do
projeto — conferir convenção existente ao implementar; se o projeto já usa
`JsonStringEnumConverter` com `camelCase`/`PascalCase` para outros enums, seguir a mesma convenção do
restante da API para não introduzir uma serialização divergente só para este DTO).

## 3. Backend — checklist de implementação (sub-issue dotnet)

Todos os detalhes de código (assinaturas completas, corpo de `RunAsync`, índice EF) já estão em
`design.md` §2.1–§2.4 — não repetir aqui, só apontar os arquivos:

1. `AfiliadoBot.Domain/Enums/JobName.cs`, `JobRunStatus.cs` (novos).
2. `AfiliadoBot.Domain/Entities/JobRun.cs` (novo) — construtor privado, `Start`/`MarkAsSuccess`/
   `MarkAsFailed`, seguindo o padrão de `PublicationQueue`/`PublicationLog`.
3. `AfiliadoBot.Infrastructure/Data/Configurations/JobRunConfiguration.cs` (novo) — tabela
   `job_runs`, índice composto `(JobName, StartedAt DESC)` (design §2.1).
4. Registrar `DbSet<JobRun> JobRuns` no `AfiliadoBotDbContext` + nova migration EF Core
   (`dotnet ef migrations add AddJobRuns` a partir de `backend/src/AfiliadoBot.Infrastructure`).
5. `AfiliadoBot.Application/Jobs/IJobRunTracker.cs`, `JobRunTracker.cs` (novos) — implementação
   exata em design §2.2.
6. `Program.cs`: `builder.Services.AddScoped<IJobRunTracker, JobRunTracker>();`.
7. Envolver `CollectorJob.ExecuteAsync`, `ProcessorJob.ExecuteAsync`, `PublisherJob.ExecuteAsync`
   com `IJobRunTracker.RunAsync` (injetar `IJobRunTracker` no construtor de cada Job).
8. `JobsController`: injetar `IJobRunTracker` e envolver `TriggerAmazonCollector`,
   `TriggerMercadoLivreCollector`, `TriggerShopeeCollector` com `RunAsync` — **preservar** o
   `try/catch (InvalidOperationException)` existente em cada action (a exceção relançada por
   `RunAsync` deve seguir sendo capturada por esse catch, ver "zero mudança de comportamento" no
   design §2.2). `TriggerCollector`/`TriggerProcessor`/`TriggerPublisher` **não** precisam de
   `RunAsync` no controller — já é coberto dentro do `ExecuteAsync` do item 7 (não envolver duas
   vezes).
9. Novo endpoint `GET /api/jobs/last-executions` em `JobsController` + `JobDtos.cs`
   (`JobLastExecutionDto`, `JobNameSlugs`) — implementação de agregação em design §2.4 (6 queries
   sequenciais, `AsNoTracking`, sem `DISTINCT ON`/`GroupBy`).
10. Testes (design §6): `JobRunTrackerTests` (novo), ajustes em `CollectorJobTests`/
    `ProcessorJobTests`/`PublisherJobTests` (injetar `IJobRunTracker` — real ou fake — no
    construtor dos testes existentes sem quebrar os testes já passando), testes de
    `JobsController`/integração cobrindo `POST .../trigger` gera `JobRun` e
    `GET /api/jobs/last-executions` (nunca executado → `status: null`; múltiplas execuções →
    retorna só a mais recente; falha de credenciais retorna 400 **e** registra `JobRun Failed`).
    Cobertura mínima 80% (padrão do repo).

## 4. Frontend — checklist de implementação (sub-issue angular)

1. `dashboard/src/app/core/services/jobs.service.ts`: adicionar tipo `JobLastExecutionDto` (mesmo
   shape do §2 acima) e método `getLastExecutions(): Observable<JobLastExecutionDto[]>` fazendo
   `GET /api/jobs/last-executions`.
2. `dashboard/src/app/pages/jobs/jobs.component.ts`:
   - Estender `JobButton` com os novos campos vindos do backend — **não remover** `triggering`/
     `lastResult`/`lastMessage` (continuam representando o resultado efêmero do clique atual,
     distintos do histórico persistido; ver design §5). Novos campos sugeridos:
     `lastExecutionStatus: 'running' | 'success' | 'failed' | null`,
     `lastExecutionStartedAt: string | null`, `lastExecutionFinishedAt: string | null`,
     `lastExecutionError: string | null`.
   - `ngOnInit`: chamar `getLastExecutions()`, mesclar no array `jobs` casando por `kind` ↔
     `jobName` (mapa 1:1, mesmos slugs).
   - `trigger()`: após o `subscribe` (branches `next` e `error`, ver §1.2 acima), rechamar
     `getLastExecutions()` e mesclar novamente.
   - Tratar `status === 'running'` sem quebrar o template (rótulo neutro, ver §1.1) — sem criar
     spinner/polling dedicado.
3. `dashboard/src/app/pages/jobs/jobs.component.html`: cada card passa a exibir, abaixo do botão
   "Disparar": data/hora de início/fim formatadas (locale pt-BR, `DatePipe` do Angular já disponível
   no projeto — conferir se `CommonModule` já cobre, é o único import necessário), indicador de
   status (sucesso = verde/check, falha = vermelho/erro, nunca executado = mensagem "Nenhuma
   execução ainda", running = rótulo neutro conforme §1.1). Falha deve mostrar `lastExecutionError`
   quando presente (Cenário 2.1/2.2).
4. `dashboard/src/app/pages/jobs/jobs.component.spec.ts`: cobrir os cenários do design §6 (card
   renderiza dado do backend após `ngOnInit`; "Nenhuma execução ainda" quando `status === null`;
   falha renderiza indicador de falha, não de sucesso). Cobertura mínima 80% (padrão do repo).

## 5. Fora de escopo (herdado do design.md §9)
Tela de relatório de histórico, retenção/expurgo, chip visual dedicado de "em andamento" (só
tratamento não-quebrado, §1.1 acima), mudança em agendamento/retry do Hangfire, leitura direta de
tabelas internas do Hangfire storage.
