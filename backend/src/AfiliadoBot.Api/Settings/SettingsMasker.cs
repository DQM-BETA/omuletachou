namespace AfiliadoBot.Api.Settings;

/// <summary>
/// Regras de mascaramento de <c>app_settings</c> sensiveis (Issue #11 / Sub-C).
/// Formato exato definido em especificacao-tecnica.md §5: 16 asteriscos fixos (nao
/// proporcional ao tamanho do valor real — evita vazar o comprimento do secret por
/// inferencia) + os ultimos 4 caracteres reais do valor.
/// </summary>
public static class SettingsMasker
{
    private static readonly string[] SensitiveSuffixes = { "_key", "_secret", "_token", "_password" };

    /// <summary>
    /// Uma chave e sensivel quando termina (case-insensitive) em _key, _secret, _token ou
    /// _password (CA-C1/CA-C2).
    /// </summary>
    public static bool IsSensitive(string key) =>
        !string.IsNullOrEmpty(key) &&
        SensitiveSuffixes.Any(suffix => key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Mascara um valor sensivel: 16 asteriscos fixos + ultimos 4 caracteres reais (ex.:
    /// "****************a1b2"). String vazia/nula retorna null (CA-C3: "nao configurado",
    /// nunca aplica o formato de mascara sobre uma string vazia).
    /// </summary>
    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        var last4 = value.Length <= 4 ? value : value[^4..];
        return new string('*', 16) + last4;
    }

    /// <summary>
    /// Aplica o mascaramento condicionalmente: mascara se a chave for sensivel (CA-C1),
    /// senao retorna o valor normalmente (CA-C2), tratando string vazia como null.
    /// </summary>
    public static string? ApplyIfSensitive(string key, string? value) =>
        IsSensitive(key) ? Mask(value) : (string.IsNullOrEmpty(value) ? null : value);
}
