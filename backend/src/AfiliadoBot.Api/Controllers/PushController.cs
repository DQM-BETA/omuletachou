using AfiliadoBot.Api.RateLimiting;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Inscricao/cancelamento de push notifications do PWA (Issue #11 / Sub-E). Publico
/// (sem [Authorize]) pois e o proprio site publico (Next.js) quem chama, antes de o
/// visitante ter qualquer nocao de login — nao existe conceito de usuario final aqui,
/// so o dashboard (ReportsController/SettingsController/etc.) exige token.
///
/// Rate limit: especificacao-tecnica.md §3/§6 define a policy nomeada "public-write"
/// (10 req/min/IP) para POST /subscribe, registrada pela Sub-D (#84) em
/// RateLimiterConfigurator.AddPublicPolicies. Aplicada aqui via [EnableRateLimiting]
/// no endpoint (CA-E4) — Sub-D ja mergeada em desenv.
/// </summary>
[ApiController]
[Route("api/public/push")]
[AllowAnonymous]
public class PushController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;

    public PushController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    [HttpPost("subscribe")]
    [EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || request.Keys is null
            || string.IsNullOrWhiteSpace(request.Keys.P256dh)
            || string.IsNullOrWhiteSpace(request.Keys.Auth))
        {
            return BadRequest(new { message = "endpoint e keys.p256dh/keys.auth sao obrigatorios." });
        }

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, ct);

        if (existing is not null)
        {
            // Endpoint ja cadastrado: subscribe e idempotente no sentido de nao duplicar
            // (mesmo endpoint = mesmo dispositivo/navegador). Retorna 200 sem criar linha nova.
            return Ok(new { id = existing.Id });
        }

        var subscription = new PushSubscription(request.Endpoint, request.Keys.P256dh, request.Keys.Auth);
        _db.PushSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return Created(string.Empty, new { id = subscription.Id });
    }

    [HttpDelete("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return BadRequest(new { message = "endpoint e obrigatorio." });

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

        if (existing is not null)
        {
            _db.PushSubscriptions.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        // CA-E3: 204 idempotente mesmo se o endpoint nao existir (especificacao-tecnica.md §6)
        // — nunca 404, para nao permitir a um chamador nao autenticado inferir por
        // tentativa/erro se um determinado endpoint de push esta cadastrado.
        return NoContent();
    }
}

public record PushSubscribeRequest(string Endpoint, PushKeys? Keys);

public record PushKeys(string P256dh, string Auth);
