# Design — ISSUE-227: Exibir data/hora da última execução de cada job na tela Jobs

## 1. Visão geral

Hoje `JobsComponent` (Angular) guarda `lastResult`/`lastMessage` só em memória do componente
(`JobButton[]`) — some no refresh porque nunca veio do backend. Não existe, hoje, nenhum registro
próprio de execução de job: `JobsController` apenas invoca `CollectorJob.ExecuteAsync`,
`ProcessorJob.ExecuteAsync`, `PublisherJob.ExecuteAsync` ou `IPlatformCollector.CollectAsync`
diretamente e devolve `Ok()`/`BadRequest()` sem persistir nada sobre a execução em si.

A decisão central desta issue é **onde capturar** início/fim/status de cada execução de forma que
cubra os dois disparadores (Hangfire agendado e disparo manual) sem duplicar lógica — e isso exige
corrigir uma premissa do `proposal.md` (ver §2.2): **o disparo manual não passa pelo Hangfire**.
`JobsController` chama `ExecuteAsync`/`CollectAsync` diretamente (chamada de método síncrona dentro
da própria requisição HTTP), nunca via `IBackgroundJobClient.Enqueue`. Isso descarta a opção de um
`IServerFilter`/`IElectStateFilter` do Hangfire (que só instrumentaria execuções agendadas — 0% dos
disparos manuais seriam capturados, violando a regra de negócio #3 do proposal: "registro próprio
único para ambos os fluxos").

Resultado: nova entidade `JobRun` (tabela `job_runs`) alimentada por um serviço central
`IJobRunTracker`, instrumentado dentro dos 3 métodos que já são o ponto de convergência entre
Hangfire e disparo manual (`CollectorJob.ExecuteAsync`, `ProcessorJob.ExecuteAsync`,
`PublisherJob.ExecuteAsync`) e nos 3 endpoints de `JobsController` que disparam os collectors
individuais (únicos pontos de chamada hoje, sem agendamento próprio). Endpoint novo
`GET /api/jobs/last-executions` agrega a última execução dos 6 jobs sem N+1 real (N=6 é fixo pela
quantidade de jobs, não pelo volume de dados — ver §2.4).

## 2. Decisões técnicas

### 2.1 Modelagem da entidade (ambiguidade #2 do proposal)

**Decisão:** nova entidade própria `JobRun` (tabela `job_runs`), seguindo o mesmo padrão de
`PublicationQueue`/`PublicationLog` (construtor privado para EF Core, setters privados, métodos de
domínio para transição de estado):

```csharp
public class JobRun
{
    public Guid Id { get; private set; }
    public JobName JobName { get; private set; }
    public JobRunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    private JobRun() { } // EF Core

    public static JobRun Start(JobName jobName) => new()
    {
        Id = Guid.NewGuid(),
        JobName = jobName,
        Status = JobRunStatus.Running,
        StartedAt = DateTime.UtcNow,
    };

    public void MarkAsSuccess()
    {
        Status = JobRunStatus.Success;
        FinishedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string? errorMessage)
    {
        Status = JobRunStatus.Failed;
        FinishedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }
}

public enum JobRunStatus { Running, Success, Failed }

public enum JobName
{
    Collector,
    CollectorAmazon,
    CollectorMercadoLivre,
    CollectorShopee,
    Processor,
    Publisher,
}
```

**Granularidade — 1 `JobName` por card da tela Jobs (6 hoje), não por "job Hangfire":** mapeia
1:1 com o `JobKind` já existente em `dashboard/.../jobs.service.ts`
(`collector | collector-amazon | collector-mercadolivre | collector-shopee | processor |
publisher`). É a granularidade que os critérios de aceite pedem (Cenário 3.1 lista exatamente esses
6 "jobs") e a que já existe na UI — não a granularidade de job Hangfire (que só tem 2 recorrentes:
`collector-job`, `publisher-job`; `ProcessorJob` é encadeado via `Enqueue`, e os 3 collectors
individuais não têm agendamento próprio hoje, só disparo manual). Nomes do enum em PascalCase
(convenção do projeto); a serialização para a API usa os mesmos slugs kebab-case que o frontend já
consome (mapeamento explícito em `JobNameSlugs`, não `JsonStringEnumConverter` — enums C# não
aceitam hífen no nome do membro).

**Por que não reaproveitar `Product`/`PublicationQueue` para isso:** não há relação com produto —
`JobRun` é sobre a execução do processo em si (coleta/processamento/publicação como um todo), não
sobre um item de fila. Confirma a regra de negócio já fechada no Gate 1 (registro próprio,
desacoplado do Hangfire).

**Índice:** composto `(JobName, StartedAt DESC)` — cobre tanto "última execução por job" (§2.4)
quanto uma futura consulta de histórico paginado por job (fora de escopo de código agora, mas o
índice já serve para isso sem mudança de schema depois).

```csharp
builder.HasIndex(x => new { x.JobName, x.StartedAt })
    .IsDescending(false, true)
    .HasDatabaseName("IX_job_runs_job_name_started_at");
```

### 2.2 Ponto de captura (ambiguidade #1 do proposal) — decisão central

**Achado que corrige a premissa do proposal:** o proposal assume que "o disparo manual também
passa pelo Hangfire como enqueue". Não passa. `JobsController` (todas as 6 actions) chama
`job.ExecuteAsync(ct)` / `collector.CollectAsync(ct)` **diretamente**, dentro da própria requisição
HTTP — nenhuma chamada a `IBackgroundJobClient.Enqueue` no caminho manual. Só `CollectorJob`
encadeia `ProcessorJob` via `Enqueue` internamente (automático, não é o disparo manual do
`processor/trigger`). Logo, **um filtro Hangfire (`IServerFilter`/`IElectStateFilter`) nunca veria
os disparos manuais** — descartado.

**Decisão:** serviço central `IJobRunTracker` (Scoped), injetado nos 3 métodos que já são o único
ponto de convergência entre o cron/enqueue do Hangfire e o disparo manual:

```csharp
public interface IJobRunTracker
{
    Task RunAsync(JobName jobName, Func<CancellationToken, Task> action, CancellationToken ct);
}
```

```csharp
public async Task RunAsync(JobName jobName, Func<CancellationToken, Task> action, CancellationToken ct)
{
    var run = JobRun.Start(jobName);
    _dbContext.JobRuns.Add(run);
    await _dbContext.SaveChangesAsync(ct);

    try
    {
        await action(ct);
        run.MarkAsSuccess();
    }
    catch (Exception ex) // inclui OperationCanceledException — nunca deixa o run preso em "Running"
    {
        run.MarkAsFailed(ex.Message);
        throw; // nunca engole exceção — ver "zero mudança de comportamento" abaixo
    }
    finally
    {
        await _dbContext.SaveChangesAsync(CancellationToken.None); // persiste mesmo se ct foi cancelado
    }
}
```

Instrumentado em:
- `CollectorJob.ExecuteAsync` → `JobName.Collector` (cobre o cron `"collector-job"` **e** o
  `POST /api/jobs/collector/trigger`, que chama o mesmo método).
- `ProcessorJob.ExecuteAsync` → `JobName.Processor` (cobre o `Enqueue` encadeado pelo
  `CollectorJob` **e** o `POST /api/jobs/processor/trigger`).
- `PublisherJob.ExecuteAsync` → `JobName.Publisher` (cobre o cron `"publisher-job"` **e** o
  `POST /api/jobs/publisher/trigger`).
- `JobsController.TriggerAmazonCollector` / `TriggerMercadoLivreCollector` / `TriggerShopeeCollector`
  → `JobName.CollectorAmazon` / `CollectorMercadoLivre` / `CollectorShopee`, envolvendo a chamada a
  `collector.CollectAsync(ct)`. Único ponto de chamada hoje para esses 3 (sem agendamento próprio) —
  não há necessidade de tocar em `AmazonCollector`/`MercadoLivreCollector`/`ShopeeCollector`.

**Por que instrumentar dentro do método em vez de em cada chamador:** cada um dos 3 Jobs tem
exatamente 2 pontos de disparo (cron/enqueue + controller). Envolver dentro do método (1 lugar) em
vez de em cada chamador (2 lugares × 3 jobs = 6) é menos código, menos risco de esquecer um
terceiro disparador futuro, e não exige tocar em `Program.cs` (registro do cron permanece
`j => j.ExecuteAsync(CancellationToken.None)`, inalterado).

**Zero mudança de comportamento de erro:** `RunAsync` nunca engole exceção — sempre relança após
registrar `Failed`. Isso preserva: (a) o retry automático do Hangfire para exceções não tratadas
nos jobs agendados; (b) o `catch (InvalidOperationException)` que já existe em
`TriggerAmazonCollector`/`TriggerMercadoLivreCollector`/`TriggerShopeeCollector` (retorna 400
"credenciais não configuradas") — a exceção relançada por `RunAsync` ainda é capturada por esse
`catch` já existente no controller, que continua funcionando sem alteração.

**Nuance aceita (não é regressão, é escopo):** `CollectorJob.ExecuteAsync` já isola falha por
plataforma internamente (`try/catch` por `collector`, Issue #7 CA1-CA4) e nunca lança quando todas
as plataformas falham (`anySuccess = false` só gera `LogWarning`). Envolver o método inteiro com
`RunAsync` **não muda isso**: o card "Collector (geral)" só aparecerá como `Failed` se
`ExecuteAsync` lançar uma exceção não tratada (bug/infra), não quando plataformas individuais
falham (já logadas e isoladas por design). Se o negócio quiser "todas as plataformas falharam"
refletido como `Failed` no card, é uma regra nova fora do escopo dos critérios de aceite desta
issue (nenhum cenário pede isso) — não decidido agora.

### 2.3 Estado "em andamento" — suportado no domínio, não obrigatório na tela

O proposal cita "em andamento" como possível estado (§ Caso de uso 2, "se aplicável ao escopo"),
mas nenhum cenário do `criterios-aceite.md` testa esse estado explicitamente. Como os 6 jobs
executam de forma síncrona dentro da própria requisição HTTP (manual) ou dentro do worker Hangfire
(agendado), `Running` fica persistido enquanto a execução está de fato ocorrendo — custa zero
código extra manter o 3º valor no enum (`JobRunStatus.Running`), e evita que uma execução longa
(ex.: `ProcessorJob` processando muitos produtos) apareça como "nunca executado" se o operador
atualizar a tela no meio do processamento. Exibir isso na tela Jobs (ex.: chip "Em andamento") é
**opcional para esta issue** — **[LT CONFIRMAR AO VIVO]** se inclui no mesmo PR ou registra como
melhoria; não bloqueia nenhum critério de aceite.

**Risco aceito:** se o processo da API cair no meio de uma execução, o `JobRun` fica preso em
`Running` (sem `FinishedAt`) permanentemente — não há watchdog/expiração. Aceitável para esta
issue (volume baixo, sem histórico de crashes documentado no projeto); registrar como possível
melhoria futura, não implementar agora (YAGNI).

### 2.4 Consulta "última execução por job" sem N+1 (ambiguidade #4 do proposal)

**Decisão:** 6 consultas (uma por `JobName`), cada uma `WHERE JobName = X ORDER BY StartedAt DESC
LIMIT 1`, sequenciais no mesmo `DbContext`:

```csharp
var lastRuns = new List<JobRun>();
foreach (var jobName in Enum.GetValues<JobName>())
{
    var run = await _dbContext.JobRuns
        .AsNoTracking()
        .Where(x => x.JobName == jobName)
        .OrderByDescending(x => x.StartedAt)
        .FirstOrDefaultAsync(ct);
    if (run is not null) lastRuns.Add(run);
}
```

**Por que isso não é o antipadrão N+1:** o padrão N+1 problemático é quando o número de consultas
escala com o *volume de dados* (ex.: 1 consulta por produto de uma lista paginada). Aqui `N = 6` é
**fixo pela quantidade de jobs** (o mesmo motivo que o proposal já antecipa: "há poucos jobs (6
hoje) e a consulta é de baixo volume") — não cresce com o histórico. Com o índice `(JobName,
StartedAt DESC)` de §2.1, cada uma das 6 consultas é um index scan direto (top-1), não um scan de
tabela. 6 round-trips sequenciais no mesmo request (~1-2ms cada em volume baixo) é desprezível para
um endpoint de dashboard interno chamado a cada abertura/refresh de tela.

**Alternativa rejeitada — `SELECT DISTINCT ON (job_name) ... ORDER BY job_name, started_at DESC`
(SQL nativo do Postgres) via `FromSqlRaw`:** é o idiom canônico do Postgres para "última linha por
grupo" e seria 1 único round-trip — mas os testes de integração deste projeto usam EF Core
`UseInMemoryDatabase` (`CustomWebApplicationFactory`, Hangfire desligado via
`Hangfire__Enabled=false`), que **não suporta `FromSqlRaw`** (lança exceção em runtime). Usar SQL
nativo quebraria a suíte de testes de integração do endpoint novo — rejeitado por não ser portável
entre o provider de teste (InMemory) e o de produção (Npgsql), sem ganho de performance relevante
no volume atual.

**Alternativa rejeitada — `GroupBy(x => x.JobName).Select(g => g.OrderByDescending(...).First())`:**
tradução "primeiro item por grupo" não é garantida por todos os providers relacionais do EF Core
(historicamente requer window functions e pode lançar em tempo de execução dependendo da versão do
provider Npgsql), com o agravante de que o provider InMemory avalia `GroupBy` no cliente (carrega
tudo em memória) — comportamento diferente e não equivalente ao que rodaria em produção contra
Postgres. Rejeitada pelo mesmo motivo de portabilidade/consistência teste-vs-produção.

**DTO de resposta** (`GET /api/jobs/last-executions`, novo endpoint em `JobsController`, mesma
proteção `[Authorize]` da classe):

```csharp
public record JobLastExecutionDto(
    string JobName,          // slug kebab-case: "collector" | "collector-amazon" | ... (= JobKind do frontend)
    string? Status,          // "running" | "success" | "failed" | null (nunca executado)
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage);
```

Sempre retorna as 6 entradas (uma por `JobName`), preenchendo com `Status = null` /
`StartedAt = null` quando não há nenhum `JobRun` para aquele job — resolve o Cenário 3.1
("Nenhuma execução ainda", sem erro/data mal formatada) no próprio contrato da API, sem o frontend
precisar inferir "ausência = nunca executado" de um array possivelmente incompleto.

### 2.5 Retenção de histórico (ambiguidade #3 do proposal)

**Decisão: nenhuma rotina de expurgo/particionamento agora.** Volume estimado: `publisher-job`
roda até 5x/dia (cron fixo `0 9,12,15,18,20 * * *`), `collector-job` 1x/dia + esporádico manual,
`processor` só quando encadeado ou manual, os 3 collectors individuais só manual/esporádico — na
faixa de dezenas de linhas/dia no pior caso, ou seja, ordem de `10^4` linhas/ano. Tabela trivial
para Postgres 16 sem índice nem particionamento especial além do já definido em §2.1. Adicionar
expurgo agora seria otimização prematura sem sinal de necessidade (nenhum requisito de negócio
pede retenção limitada) — se o volume crescer (ex.: novos jobs, cron mais frequente), tratar como
issue própria quando o sinal aparecer.

## 3. Contrato de componentes globais

**Não aplicável a esta issue.** Não há tela nova nem mudança em `AppComponent`/shell/header do
dashboard Angular — `JobsComponent` (`dashboard/src/app/pages/jobs/`) já existe e continua sendo a
única tela afetada; a mudança é puramente de conteúdo (consumir um novo endpoint e exibir os campos
retornados nos cards já existentes), sem introduzir nem duplicar layout/header/providers.

| Componente | Responsável | Afetado por esta issue |
|---|---|---|
| Shell/Header/Sidenav (`app.component.ts`) | Global, fora de `pages/jobs/` | Não |
| `JobsComponent` (`pages/jobs/jobs.component.ts`) | Conteúdo da própria tela `/jobs` | Sim — passa a buscar `GET /api/jobs/last-executions` e mesclar no `JobButton[]` já existente |
| `JobsService` (`core/services/jobs.service.ts`) | Chamadas HTTP de jobs | Sim — novo método `getLastExecutions()` |

## 4. Fluxo de dados (resumo)

```
Hangfire cron "collector-job" ──┐
POST /api/jobs/collector/trigger ┴─→ CollectorJob.ExecuteAsync
                                        └─ IJobRunTracker.RunAsync(JobName.Collector, corpo atual, ct)
                                             └─ JobRun.Start → SaveChanges → executa → MarkAsSuccess|Failed → SaveChanges

Enqueue interno (CollectorJob) ──┐
POST /api/jobs/processor/trigger ┴─→ ProcessorJob.ExecuteAsync
                                        └─ IJobRunTracker.RunAsync(JobName.Processor, ...)

Hangfire cron "publisher-job" ──┐
POST /api/jobs/publisher/trigger ┴─→ PublisherJob.ExecuteAsync
                                        └─ IJobRunTracker.RunAsync(JobName.Publisher, ...)

POST /api/jobs/collector/{amazon|mercadolivre|shopee}/trigger
  └─ JobsController: IJobRunTracker.RunAsync(JobName.Collector*, () => collector.CollectAsync(ct), ct)

GET /api/jobs/last-executions (novo)
  └─ 6x: JobRuns.Where(JobName == X).OrderByDescending(StartedAt).FirstOrDefault()
  └─ monta JobLastExecutionDto[6], preenchendo "nunca executado" quando ausente

dashboard: JobsComponent.ngOnInit → JobsService.getLastExecutions() → mescla em job.lastExecution*
```

## 5. Componentes afetados

| Componente | Mudança | Escopo |
|---|---|---|
| `AfiliadoBot.Domain.Entities.JobRun` (novo) | Nova entidade — histórico de execuções | Backend |
| `AfiliadoBot.Domain.Enums.JobName`, `JobRunStatus` (novos) | Enums de domínio | Backend |
| `AfiliadoBot.Infrastructure.Data.Configurations.JobRunConfiguration` (novo) | EF config: tabela `job_runs`, índice composto | Backend |
| Nova migration EF Core | Cria tabela `job_runs` | Backend |
| `AfiliadoBot.Application.Jobs.IJobRunTracker`/`JobRunTracker` (novo) | Serviço central de instrumentação (§2.2) | Backend |
| `AfiliadoBot.Application.Jobs.CollectorJob` | Corpo de `ExecuteAsync` envolvido por `IJobRunTracker.RunAsync` | Backend |
| `AfiliadoBot.Application.Jobs.ProcessorJob` | Idem | Backend |
| `AfiliadoBot.Application.Jobs.PublisherJob` | Idem | Backend |
| `AfiliadoBot.Api.Controllers.JobsController` | 3 actions de collector individual envolvidas por `RunAsync`; novo endpoint `GET last-executions` | Backend |
| `AfiliadoBot.Api.Jobs.JobDtos` (novo, `JobLastExecutionDto` + `JobNameSlugs`) | DTO + mapeamento enum→slug kebab-case | Backend |
| `Program.cs` | `builder.Services.AddScoped<IJobRunTracker, JobRunTracker>();` | Backend |
| `dashboard/.../core/services/jobs.service.ts` | Novo método `getLastExecutions(): Observable<JobLastExecutionDto[]>` + tipo do DTO | Frontend |
| `dashboard/.../pages/jobs/jobs.component.ts` | `ngOnInit` busca last-executions e mescla em `JobButton` (novos campos `lastExecutionStatus/StartedAt/FinishedAt`, distintos dos campos efêmeros `triggering/lastResult/lastMessage` do clique atual); recomenda-se refazer o fetch após `trigger()` bem-sucedido para refletir o novo estado sem exigir F5 — **[LT CONFIRMAR AO VIVO]** | Frontend |
| `dashboard/.../pages/jobs/jobs.component.html` | Card passa a exibir status/data-hora vindos do backend em vez de só `lastMessage` local; mensagem "Nenhuma execução ainda" quando `lastExecutionStatus === null` | Frontend |
| Testes (`JobRunTrackerTests`, `JobsTriggerTests`, `CollectorJobTests`?, `ProcessorJobTests`, `PublisherJobTests`, `jobs.component.spec.ts`) | Cobrir cenários dos critérios de aceite (§6) | Backend/Frontend |

## 6. Casos de teste a cobrir (mapeamento para os critérios de aceite)

- `JobRunTrackerTests`: `RunAsync` persiste `Running` antes de executar a ação, `Success` +
  `FinishedAt` após sucesso, `Failed` + `FinishedAt` + `ErrorMessage` após exceção — e sempre
  relança (CA 4.1, CA 5.1).
- `JobsTriggerTests`/testes de `JobsController`: `POST /api/jobs/{x}/trigger` cria um `JobRun` para
  o `JobName` correspondente; `GET /api/jobs/last-executions` reflete a execução mais recente após
  o disparo manual (CA 1.1, CA 5.1); `InvalidOperationException` de credenciais ausentes continua
  retornando 400 **e** registra `JobRun` como `Failed` (regressão + CA 2.1).
- `CollectorJobTests`/`ProcessorJobTests`/`PublisherJobTests`: `ExecuteAsync` chamado diretamente
  (sem passar pelo controller, simulando o cron do Hangfire) também gera `JobRun` — prova que o
  mesmo mecanismo cobre os dois disparadores (CA 4.2, CA 5.1, CA 5.2).
- `GET /api/jobs/last-executions`: job nunca executado retorna `Status: null` para aquele `JobName`
  (CA 3.1); job com múltiplas execuções retorna apenas a mais recente por `StartedAt` (CA 4.3); job
  cuja última execução falhou retorna `Status: "failed"` com timestamps (CA 2.1, CA 2.2); execuções
  antigas continuam no banco (não deletadas) mesmo não aparecendo no endpoint (CA 4.1, CA 4.3).
- `jobs.component.spec.ts`: card renderiza data/hora + status vindos do backend após `ngOnInit`
  (CA 1.1); mensagem "Nenhuma execução ainda" quando `Status` é `null` (CA 3.1); status `failed`
  renderiza indicador de falha, não de sucesso (CA 2.1, CA 2.2).

## 7. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| `JobRun` preso em `Running` se o processo cair no meio de uma execução | Aceito para esta issue (§2.3) — sem watchdog; registrar como melhoria futura se acontecer na prática |
| Esquecer de envolver um futuro 4º ponto de disparo de um job existente (ex.: novo endpoint) com `IJobRunTracker` | Instrumentação vive dentro do método `ExecuteAsync`/`CollectAsync` (não no chamador) — qualquer novo disparador que reutilize o mesmo método já fica coberto automaticamente, sem ação adicional |
| Card "Collector (geral)" mostra `Success` mesmo quando todas as plataformas internas falharam (nuance §2.2) | Documentado como escopo aceito (isolamento de falha por plataforma é comportamento pré-existente da Issue #7, não desta issue); revisar como regra de negócio nova se o Gerente pedir explicitamente |
| 6 queries sequenciais em `GET /api/jobs/last-executions` degradarem se o número de jobs crescer muito | Aceitável no volume atual (§2.4); se o número de jobs crescer significativamente, revisitar com `DISTINCT ON` nativo (exigiria também revisar a estratégia de teste de integração, que hoje usa EF InMemory) |
| Migration nova (`job_runs`) precisa rodar em produção antes do deploy do código que a usa | `db.Database.Migrate()` já roda automaticamente no startup (`Program.cs`, padrão já usado por todas as migrations existentes) — sem passo manual adicional |

## 8. Dependências

- Depende de `CollectorJob`, `ProcessorJob`, `PublisherJob`, `JobsController` existentes
  (Issues #6/#7/#11) — modificados, não recriados.
- Depende do padrão de entidade com construtor privado + métodos de domínio já estabelecido por
  `PublicationQueue`/`PublicationLog` (Issue #6/#7) — reaproveitado, não uma convenção nova.
- Nenhuma dependência de pacote NuGet/npm nova.

## 9. Fora de escopo (confirmado no proposal)

- Tela/funcionalidade de relatório sobre o histórico (só a persistência é necessária agora).
- Política de retenção/expurgo do histórico (§2.5).
- Exibição de "em andamento" na tela Jobs (§2.3 — suportado no domínio, exibição é opcional/LT).
- Qualquer mudança na lógica de quando/como um job é disparado (agendamento, retries do Hangfire).
- Consultar diretamente as tabelas internas do Hangfire storage (regra de negócio já fechada —
  `JobRun` é a única fonte usada pelo endpoint novo).
