using System.Text.RegularExpressions;

namespace AfiliadoBot.Infrastructure.Integrations.Social;

/// <summary>
/// Helper compartilhado de disclosure de conteudo comercial de afiliado (Issue #10 / #77):
/// extraido do <see cref="InstagramPublisher"/> (Issue #9 / #73) para ser reutilizado tambem pelo
/// <see cref="TikTokPublisher"/> — checagem deterministica (regex), garantindo a presenca do
/// disclosure independentemente do que a IA gerou, sem duplicar quando ja presente.
/// </summary>
public static class SocialDisclosureHelper
{
    public const string DefaultHashtag = "#publi";

    private static readonly Regex DisclosureRegex = new(
        @"#publi\b|#publicidade\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Anexa <paramref name="hashtag"/> ao final de <paramref name="caption"/> quando nenhuma
    /// variante de disclosure (<c>#publi</c>/<c>#publicidade</c>, case-insensitive) ja estiver
    /// presente. Comportamento identico ao antigo <c>InstagramPublisher.AppendDisclosureIfMissing</c>.
    /// </summary>
    public static string AppendIfMissing(string caption, string hashtag = DefaultHashtag)
    {
        if (DisclosureRegex.IsMatch(caption))
            return caption;

        return string.IsNullOrWhiteSpace(caption) ? hashtag : $"{caption} {hashtag}";
    }
}
