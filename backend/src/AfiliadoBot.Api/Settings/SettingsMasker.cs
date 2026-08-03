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
    /// Numero fixo de asteriscos usado para valores curtos (Length &lt;= 4), igual ao
    /// pior caso do formato longo (16 + 4 caracteres do sufixo). Precisa ser CONSTANTE
    /// (nao proporcional ao Length real) para nao vazar o comprimento do segredo por
    /// inferencia (Regressao Issue #136).
    /// </summary>
    private const int ShortValueMaskLength = 20;

    /// <summary>
    /// Mascara um valor sensivel: 16 asteriscos fixos + ultimos 4 caracteres reais (ex.:
    /// "****************a1b2"). String vazia/nula retorna null (CA-C3: "nao configurado",
    /// nunca aplica o formato de mascara sobre uma string vazia).
    /// Regressao Issue #131: valores com Length &lt;= 4 NUNCA revelam o valor real (isso
    /// vazaria o segredo completo em claro) — mascara totalmente, so asteriscos.
    /// Regressao Issue #136: a mascara de valores curtos usa um comprimento de saida FIXO
    /// (<see cref="ShortValueMaskLength"/>), nao <c>16 + value.Length</c> — caso contrario o
    /// comprimento da resposta (17 a 20 asteriscos) vazaria o tamanho real do segredo por
    /// inferencia, mesmo sem revelar o conteudo.
    /// </summary>
    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        if (value.Length <= 4) return new string('*', ShortValueMaskLength);

        var last4 = value[^4..];
        return new string('*', 16) + last4;
    }

    /// <summary>
    /// Aplica o mascaramento condicionalmente: mascara se a chave for sensivel (CA-C1),
    /// senao retorna o valor normalmente (CA-C2), tratando string vazia como null.
    /// </summary>
    public static string? ApplyIfSensitive(string key, string? value) =>
        IsSensitive(key) ? Mask(value) : (string.IsNullOrEmpty(value) ? null : value);
}
