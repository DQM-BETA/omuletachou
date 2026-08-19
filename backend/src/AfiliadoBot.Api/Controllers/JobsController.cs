using AfiliadoBot.Api.Jobs;
using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Platforms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Disparo manual dos jobs registrados no Hangfire (Issue #11 / Sub-C, CA-C7/C8/C9).
/// Substitui os endpoints minimos equivalentes que existiam soltos em Program.cs — mesmos
/// paths (mantem compatibilidade), agora protegidos por [Authorize] (CA-C10; antes nao
/// exigiam token, gap fechado por esta sub-issue).
/// Issue #227: os 3 collectors individuais sao instrumentados aqui via
/// <see cref="IJobRunTracker"/> (unico ponto de chamada hoje, sem agendamento proprio).
/// TriggerCollector/TriggerProcessor/TriggerPublisher NAO precisam de RunAsync no controller —
/// ja e coberto dentro do ExecuteAsync do respectivo Job (nao instrumentar duas vezes).
/// </summary>
[ApiController]
[Route("api/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    [HttpPost("collector/trigger")]
    public async Task<IActionResult> TriggerCollector([FromServices] CollectorJob job, CancellationToken ct)
    {
        await job.ExecuteAsync(ct);
        return Ok();
    }

    [HttpPost("collector/amazon/trigger")]
    public Task<IActionResult> TriggerAmazonCollector(
        [FromServices] AmazonCollector collector,
        [FromServices] IJobRunTracker jobRunTracker,
        CancellationToken ct)
        => TriggerIndividualCollectorAsync(collector, JobName.CollectorAmazon, "amazon", jobRunTracker, ct);

    [HttpPost("collector/mercadolivre/trigger")]
    public Task<IActionResult> TriggerMercadoLivreCollector(
        [FromServices] MercadoLivreCollector collector,
        [FromServices] IJobRunTracker jobRunTracker,
        CancellationToken ct)
        => TriggerIndividualCollectorAsync(collector, JobName.CollectorMercadoLivre, "mercadolivre", jobRunTracker, ct);

    [HttpPost("collector/shopee/trigger")]
    public Task<IActionResult> TriggerShopeeCollector(
        [FromServices] ShopeeCollector collector,
        [FromServices] IJobRunTracker jobRunTracker,
        CancellationToken ct)
        => TriggerIndividualCollectorAsync(collector, JobName.CollectorShopee, "shopee", jobRunTracker, ct);

    /// <summary>
    /// Issue #227: envolve a chamada a <see cref="IPlatformCollector.CollectAsync"/> com
    /// <see cref="IJobRunTracker.RunAsync"/> — o catch (InvalidOperationException) existente
    /// (credenciais ausentes) continua funcionando sem alteracao, ja que RunAsync sempre relanca
    /// a excecao apos registrar o JobRun como Failed (design.md §2.2, "zero mudanca de
    /// comportamento de erro").
    /// </summary>
    private async Task<IActionResult> TriggerIndividualCollectorAsync(
        IPlatformCollector collector,
        JobName jobName,
        string platformSlug,
        IJobRunTracker jobRunTracker,
        CancellationToken ct)
    {
        try
        {
            var count = 0;
            await jobRunTracker.RunAsync(jobName, async innerCt =>
            {
                var products = await collector.CollectAsync(innerCt);
                count = products.Count();
            }, ct);
            return Ok(new { count });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = $"Credenciais não configuradas para {platformSlug}: {ex.Message}" });
        }
    }

    [HttpPost("processor/trigger")]
    public async Task<IActionResult> TriggerProcessor([FromServices] ProcessorJob job, CancellationToken ct)
    {
        await job.ExecuteAsync(ct);
        return Ok();
    }

    [HttpPost("publisher/trigger")]
    public async Task<IActionResult> TriggerPublisher([FromServices] PublisherJob job, CancellationToken ct)
    {
        await job.ExecuteAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Issue #227: agrega a ultima execucao de cada um dos 6 jobs. Sempre retorna as 6 entradas
    /// (uma por JobName), preenchendo Status/StartedAt/FinishedAt = null quando nao ha nenhum
    /// JobRun para aquele job (CA 3.1). 6 consultas sequenciais indexadas por (JobName,
    /// StartedAt DESC) — NAO usar DISTINCT ON/FromSqlRaw nem GroupBy().Select(First()): ambos
    /// quebram (ou nao sao equivalentes) no provider InMemory usado nos testes de integracao
    /// (design.md §2.4).
    /// </summary>
    [HttpGet("last-executions")]
    public async Task<IActionResult> GetLastExecutions([FromServices] AfiliadoBotDbContext dbContext, CancellationToken ct)
    {
        var results = new List<JobLastExecutionDto>();

        foreach (var jobName in Enum.GetValues<JobName>())
        {
            var run = await dbContext.JobRuns
                .AsNoTracking()
                .Where(x => x.JobName == jobName)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync(ct);

            results.Add(run is null
                ? new JobLastExecutionDto(JobNameSlugs.ToSlug(jobName), null, null, null, null)
                : new JobLastExecutionDto(
                    JobNameSlugs.ToSlug(jobName),
                    JobNameSlugs.ToStatusSlug(run.Status),
                    run.StartedAt,
                    run.FinishedAt,
                    run.ErrorMessage));
        }

        return Ok(results);
    }
}
