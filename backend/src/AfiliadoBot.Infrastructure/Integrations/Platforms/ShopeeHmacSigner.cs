using System.Security.Cryptography;
using System.Text;

namespace AfiliadoBot.Infrastructure.Integrations.Platforms;

/// <summary>
/// Gera a assinatura HMAC-SHA256 exigida pela Shopee Affiliate API (Open API v2).
/// Formato de assinatura documentado pela Shopee: HMAC-SHA256 aplicado sobre a
/// concatenacao "{partnerId}{timestamp}{payload}", usando o partner secret como chave,
/// resultando em hexadecimal minusculo. O header de autenticacao segue o padrao
/// "SHA256 {signature}" adotado por esta integracao (ver criterios de aceite da Issue #5).
/// </summary>
public static class ShopeeHmacSigner
{
    /// <summary>
    /// Calcula a assinatura HMAC-SHA256 em hexadecimal.
    /// </summary>
    /// <param name="partnerId">Identificador do parceiro (shopee.app_id).</param>
    /// <param name="path">Path do endpoint (ex.: "/graphql"), incluido por documentacao/rastreabilidade
    /// da base string, ainda que a Shopee GraphQL assine partnerId+timestamp+payload.</param>
    /// <param name="timestamp">Unix timestamp (segundos) do momento da requisicao.</param>
    /// <param name="secret">Partner secret (shopee.secret) usado como chave HMAC.</param>
    /// <param name="payload">Corpo (body) da requisicao GraphQL, serializado como string.</param>
    public static string Sign(string partnerId, string path, long timestamp, string secret, string payload)
    {
        // Base string: partner_id + timestamp + payload (path mantido no parametro por
        // clareza/rastreabilidade da chamada, mas nao entra na base conforme padrao GraphQL da Shopee).
        var baseString = $"{partnerId}{timestamp}{payload}";

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(baseString);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
