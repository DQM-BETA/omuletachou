# Tasks — ISSUE-227: Exibir data/hora da última execução de cada job na tela Jobs

> Devs leem só este arquivo. Contexto técnico completo em
> `documentacoes/ISSUE-227-exibir-data-hora-ultima-execucao-jobs/especificacao-tecnica.md` e decisões
> de arquitetura em `openspec/changes/issue-227-exibir-data-hora-ultima-execucao-jobs/design.md`.

## T-01 (stack:dotnet) — Persistir execuções de job (`JobRun`) e endpoint de agregação

**Sub-issue:** ver número no GitHub (criada pelo LT).

### O que fazer
Criar a entidade `JobRun` + enums `JobName`/`JobRunStatus`, o serviço `IJobRunTracker`, instrumentar
`CollectorJob.ExecuteAsync`, `ProcessorJob.ExecuteAsync`, `PublisherJob.ExecuteAsync` e as 3 actions
de collector individual (`TriggerAmazonCollector`, `TriggerMercadoLivreCollector`,
`TriggerShopeeCollector`) em `JobsController`, e criar o endpoint `GET /api/jobs/last-executions`.
Checklist completo de arquivos: `especificacao-tecnica.md` §3.

### Critérios de aceite (Given/When/Then — mapeados de `criterios-aceite.md`)
- **CA 4.1/5.1** (`JobRunTrackerTests`): `RunAsync` persiste `JobRun` com `Status = Running` e
  `StartedAt` **antes** de executar a ação; após sucesso, `Status = Success` + `FinishedAt`
  preenchido; após exceção, `Status = Failed` + `FinishedAt` + `ErrorMessage` preenchidos e a
  exceção é relançada (nunca engolida).
- **CA 1.1/5.1** (`JobsController`/testes de integração): `POST /api/jobs/{qualquer}/trigger` cria
  um `JobRun` para o `JobName` correspondente, tanto para os 3 jobs (`CollectorJob`/`ProcessorJob`/
  `PublisherJob`, instrumentados dentro do método) quanto para os 3 collectors individuais
  (instrumentados no controller).
- **CA 2.1** (regressão + tracking): `TriggerAmazonCollector`/`TriggerMercadoLivreCollector`/
  `TriggerShopeeCollector` continuam retornando 400 com a mensagem de credenciais ausentes quando
  `CollectAsync` lança `InvalidOperationException` — **e** o `JobRun` correspondente fica registrado
  como `Failed`.
- **CA 4.2/5.1** (`CollectorJobTests`/`ProcessorJobTests`/`PublisherJobTests`): chamar
  `ExecuteAsync` diretamente (simulando o cron do Hangfire, sem passar pelo controller) também gera
  um `JobRun` — prova que o mesmo mecanismo cobre os dois disparadores (agendado e manual).
- **CA 3.1** (`GET /api/jobs/last-executions`): job sem nenhum `JobRun` no banco retorna
  `status: null`, `startedAt: null`, `finishedAt: null` para aquele `jobName` — sempre as 6 entradas
  no array, mesmo com histórico parcial.
- **CA 4.3** (`GET /api/jobs/last-executions`): job com múltiplas execuções retorna apenas a de
  `StartedAt` mais recente; as execuções antigas continuam no banco (não deletadas), só não
  aparecem no endpoint.
- **CA 2.2** (`GET /api/jobs/last-executions`): última execução com falha retorna
  `status: "failed"` com `startedAt`/`finishedAt`/`errorMessage` preenchidos (nunca `status:
  "success"` para uma execução que falhou).
- **CA 5.2**: o endpoint consulta exclusivamente `JobRuns` (EF Core) — nenhuma consulta às tabelas
  internas do Hangfire storage.
- Migration EF Core criada e aplicável (`dotnet ef migrations add AddJobRuns` a partir de
  `backend/src/AfiliadoBot.Infrastructure`); `Program.cs` já roda `Migrate()` automaticamente, não
  precisa de passo manual adicional.
- Cobertura de testes do projeto backend ≥ 80%.

### Contexto técnico
- `especificacao-tecnica.md` §1.1 (tratamento de `Running`, sem UI dedicada — não é escopo do
  backend, mas o enum deve suportar o valor), §2 (contrato exato do DTO/slugs), §3 (checklist de
  arquivos).
- `design.md` §2.1–§2.4 (código de referência: entidade, `RunAsync`, índice, agregação sem N+1).
- Stack: ASP.NET Core 8.0, EF Core 8.0, PostgreSQL 16 (produção) / EF Core InMemory (testes de
  integração via `CustomWebApplicationFactory`, `Hangfire__Enabled=false`).
- Repo: `repos/omuletachou` (branch base `desenv`).
- Padrão de entidade a seguir: `AfiliadoBot.Domain.Entities.PublicationQueue`/`PublicationLog`
  (construtor privado, setters privados, métodos de domínio).

## T-02 (stack:angular) — Exibir última execução no card da tela Jobs

**Sub-issue:** ver número no GitHub (criada pelo LT).
**Depende de:** T-01 (consome o endpoint `GET /api/jobs/last-executions`; pode iniciar em paralelo
usando o contrato do DTO em `especificacao-tecnica.md` §2, mas o PR final só é testável de ponta a
ponta com o backend mergeado).

### O que fazer
Consumir `GET /api/jobs/last-executions` em `JobsComponent`, mesclar no `JobButton[]` existente e
exibir data/hora + status no card, incluindo o caso "nunca executado" e o caso de falha. Checklist
completo de arquivos: `especificacao-tecnica.md` §4.

### Critérios de aceite (Given/When/Then — mapeados de `criterios-aceite.md`)
- **CA 1.1** (`jobs.component.spec.ts`): após `ngOnInit`, o card de um job já executado exibe
  data/hora de início e fim vindas do backend (não de `lastMessage`/estado local do clique atual).
- **CA 1.2**: o dado exibido vem sempre de `getLastExecutions()` no `ngOnInit` (nunca de estado
  em memória que se perderia entre sessões) — implícito na implementação de T-02, sem necessidade
  de mock de "nova sessão" no teste unitário (é garantido pela ausência de qualquer cache local).
- **CA 1.3**: card com última execução `success` exibe indicador de sucesso junto com data/hora.
- **CA 2.1/2.2**: card com última execução `failed` exibe indicador de falha claramente distinto
  do indicador de sucesso, junto com data/hora — nunca omite a informação nem mostra como sucesso.
- **CA 3.1**: card de job com `status: null` exibe mensagem "Nenhuma execução ainda" (ou
  equivalente), sem erro nem data/hora mal formatada.
- Refetch pós-disparo (`especificacao-tecnica.md` §1.2): após `trigger()` completar (sucesso ou
  erro), o card reflete a nova execução sem exigir F5 — chamar `getLastExecutions()` novamente no
  `subscribe`.
- Tratamento de `status: "running"` não quebra o template (rótulo neutro, sem exigir spinner/polling
  dedicado — `especificacao-tecnica.md` §1.1).
- Cobertura de testes do projeto frontend ≥ 80% (padrão do repo).

### Contexto técnico
- `especificacao-tecnica.md` §1 (decisões `[LT CONFIRMAR AO VIVO]` resolvidas), §2 (contrato do
  DTO/slugs — usar exatamente esses nomes de campo), §4 (checklist de arquivos).
- `design.md` §3 (contrato de componentes globais — só `JobsComponent`/`JobsService` afetados, sem
  mudança em shell/header).
- Stack: Angular 17+, `HttpClient`, Angular Material (`MatCardModule`/`MatIconModule` já em uso).
- Repo: `repos/omuletachou` (branch base `desenv`).
- Arquivos-base a editar (já existem, não criar do zero):
  `dashboard/src/app/core/services/jobs.service.ts`,
  `dashboard/src/app/pages/jobs/jobs.component.ts`,
  `dashboard/src/app/pages/jobs/jobs.component.html`,
  `dashboard/src/app/pages/jobs/jobs.component.spec.ts`.
