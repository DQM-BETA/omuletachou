using Microsoft.AspNetCore.Cors.Infrastructure;

namespace AfiliadoBot.Api.Cors;

/// <summary>
/// CORS restrito a lista explicita de origins (Issue #11 / Sub-D, CA-D8/CA-D9/CA-D10). NUNCA usa
/// AllowAnyOrigin em nenhum ambiente — a lista vem de "Cors:AllowedOrigins" em appsettings.json
/// (versionado, sem secret), configuravel por ambiente via appsettings.{Environment}.json.
/// </summary>
public static class CorsConfigurator
{
    public const string PolicyName = "public-cors";

    /// <summary>
    /// Origins default (Gate 1): site publico (prod + www), dashboard admin e os dois hosts de
    /// desenvolvimento local (Next.js/Angular). Usado somente quando "Cors:AllowedOrigins" nao
    /// estiver definido em appsettings — em producao a secao deve sempre estar explicita.
    /// </summary>
    public static readonly string[] DefaultAllowedOrigins =
    [
        "https://omuletachou.com.br",
        "https://www.omuletachou.com.br",
        "https://dashboard.omuletachou.com.br",
        "http://localhost:3000",
        "http://localhost:4200",
    ];

    public static void AddPublicCorsPolicy(this CorsOptions options, IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var origins = configuredOrigins is { Length: > 0 } ? configuredOrigins : DefaultAllowedOrigins;

        options.AddPolicy(PolicyName, policy =>
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    }
}
