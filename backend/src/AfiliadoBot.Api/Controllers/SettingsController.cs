using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AfiliadoBot.Api.Settings;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// CRUD de configuracao (Issue #11 / Sub-C). GET lista todas as chaves de <c>app_settings</c>
/// mascarando as sensiveis (CA-C1/CA-C2/CA-C3); PUT sobrescreve o valor de uma chave existente
/// e nunca ecoa o valor completo de volta (CA-C4/CA-C5/CA-C6). Protegido por [Authorize]
/// (CA-C10) — a chave de assinatura JWT em si (Jwt:SigningKey) nunca passa por aqui
/// (especificacao-tecnica.md §2, nao e uma linha de app_settings).
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(AfiliadoBotDbContext db, ILogger<SettingsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var settings = await _db.AppSettings
            .OrderBy(s => s.Key)
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(ct);

        var result = settings
            .Select(s => new SettingDto(s.Key, SettingsMasker.ApplyIfSensitive(s.Key, s.Value)))
            .ToList();

        // Log estruturado (recomendacao do Arquiteto, design.md §2.2, nao bloqueante):
        // apenas metadados (quem/quando) — NUNCA o valor de nenhuma chave, mascarado ou nao.
        _logger.LogInformation(
            "Settings GET by user {UserId} at {Timestamp}",
            User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            DateTimeOffset.UtcNow);

        return Ok(result);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
            return NotFound();

        // CA-C5: nunca cria a chave implicitamente — so atualiza uma ja existente.
        setting.UpdateValue(request.Value ?? string.Empty);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Settings PUT key={Key} by user {UserId} at {Timestamp}",
            key,
            User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            DateTimeOffset.UtcNow);

        // CA-C6: a resposta nunca contem o valor completo em texto plano para chaves
        // sensiveis (nem o novo, nem o antigo) — no maximo o valor ja mascarado.
        var responseValue = SettingsMasker.ApplyIfSensitive(key, setting.Value);
        return Ok(new SettingDto(key, responseValue));
    }
}
