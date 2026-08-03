using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Infrastructure.Integrations.Platforms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Disparo manual dos jobs registrados no Hangfire (Issue #11 / Sub-C, CA-C7/C8/C9).
/// Substitui os endpoints minimos equivalentes que existiam soltos em Program.cs — mesmos
/// paths (mantem compatibilidade), agora protegidos por [Authorize] (CA-C10; antes nao
/// exigiam token, gap fechado por esta sub-issue).
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
    public async Task<IActionResult> TriggerAmazonCollector([FromServices] AmazonCollector collector, CancellationToken ct)
    {
        try
        {
            var products = await collector.CollectAsync(ct);
            return Ok(new { count = products.Count() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = $"Credenciais não configuradas para amazon: {ex.Message}" });
        }
    }

    [HttpPost("collector/mercadolivre/trigger")]
    public async Task<IActionResult> TriggerMercadoLivreCollector([FromServices] MercadoLivreCollector collector, CancellationToken ct)
    {
        try
        {
            var products = await collector.CollectAsync(ct);
            return Ok(new { count = products.Count() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = $"Credenciais não configuradas para mercadolivre: {ex.Message}" });
        }
    }

    [HttpPost("collector/shopee/trigger")]
    public async Task<IActionResult> TriggerShopeeCollector([FromServices] ShopeeCollector collector, CancellationToken ct)
    {
        try
        {
            var products = await collector.CollectAsync(ct);
            return Ok(new { count = products.Count() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = $"Credenciais não configuradas para shopee: {ex.Message}" });
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
}
