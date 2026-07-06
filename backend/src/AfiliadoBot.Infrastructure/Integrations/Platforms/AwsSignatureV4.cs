using System.Security.Cryptography;
using System.Text;

namespace AfiliadoBot.Infrastructure.Integrations.Platforms;

/// <summary>
/// Implementacao manual da assinatura AWS Signature V4 (sem SDK AWS),
/// utilizada para autenticar requisicoes a Amazon Product Advertising API v5.
/// </summary>
public static class AwsSignatureV4
{
    /// <summary>
    /// Gera os headers necessarios (Authorization, X-Amz-Date, X-Amz-Content-Sha256)
    /// para uma requisicao POST assinada com AWS SigV4.
    /// </summary>
    public static IDictionary<string, string> SignRequest(
        string accessKey,
        string secretKey,
        string region,
        string service,
        string host,
        string path,
        string payload,
        string amzTarget)
    {
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        var payloadHash = Sha256Hex(payload);

        var canonicalHeaders =
            $"content-encoding:amz-1.0\n" +
            $"content-type:application/json; charset=utf-8\n" +
            $"host:{host}\n" +
            $"x-amz-content-sha256:{payloadHash}\n" +
            $"x-amz-date:{amzDate}\n" +
            $"x-amz-target:{amzTarget}\n";

        const string signedHeaders = "content-encoding;content-type;host;x-amz-content-sha256;x-amz-date;x-amz-target";

        var canonicalRequest =
            $"POST\n{path}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";

        var stringToSign =
            $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";

        var signingKey = GetSignatureKey(secretKey, dateStamp, region, service);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        var authorizationHeader =
            $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, " +
            $"SignedHeaders={signedHeaders}, Signature={signature}";

        return new Dictionary<string, string>
        {
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Content-Sha256"] = payloadHash,
            ["Authorization"] = authorizationHeader
        };
    }

    private static byte[] GetSignatureKey(string secretKey, string dateStamp, string region, string service)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Sha256Hex(string data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return ToHex(hash);
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
