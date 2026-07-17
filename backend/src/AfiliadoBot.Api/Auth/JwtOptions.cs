namespace AfiliadoBot.Api.Auth;

/// <summary>
/// Configuracao de emissao/validacao do JWT (Issue #11 / Sub-A). Bind de "Jwt" em
/// appsettings.json / variaveis de ambiente (Jwt__SigningKey etc.).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 24;
}
