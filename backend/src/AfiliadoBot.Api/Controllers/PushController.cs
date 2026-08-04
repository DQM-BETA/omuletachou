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
            // (mesmo endpoint = mesmo dispositivo/navegador). Upsert silencioso (Issue #14,
            // criterios-aceite.md): renova P256dh/Auth/CreatedAt com os novos valores
            // recebidos (o browser pode ter gerado um novo par de chaves para o mesmo
            // endpoint, ex. apos limpar o cache) e retorna 200 sem criar linha nova.
            existing.Renew(request.Keys.P256dh, request.Keys.Auth);
            await _db.SaveChangesAsync(ct);
            return Ok(new { id = existing.Id });
        }

        var subscription = new PushSubscription(request.Endpoint, request.Keys.P256dh, request.Keys.Auth);
        _db.PushSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return Created(string.Empty, new { id = subscription.Id });
    }

    [HttpDelete("unsubscribe")]
    [EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)]
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

    /// <summary>
    /// Chave publica VAPID em claro (Issue #14 / Sub-A, especificacao-tecnica.md §4). Bypass
    /// EXPLICITO do <c>SettingsMasker</c>: embora "push.vapid_public_key" termine em "_key" e
    /// portanto seja classificada como sensivel pelo masker do dashboard (GET /api/settings,
    /// SettingsController — comportamento aceito, ver design.md), esta chave especifica NAO e
    /// um segredo (e enviada ao browser via applicationServerKey do Web Push) e precisa ser
    /// lida em claro por este endpoint publico dedicado, nunca pelo SettingsController.
    /// </summary>
    [HttpGet("vapid-public-key")]
    [EnableRateLimiting(RateLimiterConfigurator.PublicReadPolicy)]
    public async Task<IActionResult> GetVapidPublicKey(CancellationToken ct)
    {
        var value = await _db.AppSettings
            .Where(s => s.Key == "push.vapid_public_key")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        return Ok(new { publicKey = string.IsNullOrWhiteSpace(value) ? null : value });
    }
}

public record PushSubscribeRequest(string Endpoint, PushKeys? Keys);

public record PushKeys(string P256dh, string Auth);
