using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace AfiliadoBot.Api.RateLimiting;

/// <summary>
/// Rate limiting nativo do .NET 8 para os endpoints publicos (Issue #11 / Sub-D,
/// especificacao-tecnica.md §3, design.md §3). Duas policies nomeadas, particionadas por IP
/// (RemoteIpAddress ja reescrito pelo ForwardedHeadersMiddleware — CA-D11/CA-D12):
/// - "public-read" (60 req/min/IP): PublicController (deals/slug/category).
/// - "public-write" (10 req/min/IP): aplicada em POST /api/public/push/subscribe via
///   [EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)] (PushController — CA-E4).
/// Limites configuraveis via appsettings ("RateLimiting:PublicReadPermitLimit" /
/// "RateLimiting:PublicWritePermitLimit"), com os valores do Gate 1 como default.
/// </summary>
public static class RateLimiterConfigurator
{
    public const string PublicReadPolicy = "public-read";
    public const string PublicWritePolicy = "public-write";

    public const int DefaultPublicReadPermitLimit = 60;
    public const int DefaultPublicWritePermitLimit = 10;

    public static void AddPublicPolicies(this RateLimiterOptions options, IConfiguration configuration)
    {
        var readLimit = configuration.GetValue("RateLimiting:PublicReadPermitLimit", DefaultPublicReadPermitLimit);
        var writeLimit = configuration.GetValue("RateLimiting:PublicWritePermitLimit", DefaultPublicWritePermitLimit);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(PublicReadPolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => FixedWindowOptions(readLimit)));

        options.AddPolicy(PublicWritePolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => FixedWindowOptions(writeLimit)));
    }

    /// <summary>
    /// Constroi um limiter isolado (sem HttpContext/host), usado nos testes unitarios das
    /// policies para validar o comportamento (permit limit, janela, rejeicao) sem precisar
    /// disparar dezenas de requisicoes HTTP reais.
    /// </summary>
    public static PartitionedRateLimiter<string> CreateFixedWindowLimiter(int permitLimit, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => FixedWindowOptions(permitLimit, window)));

    private static FixedWindowRateLimiterOptions FixedWindowOptions(int permitLimit, TimeSpan? window = null) => new()
    {
        PermitLimit = permitLimit,
        Window = window ?? TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    };

    // Particionamento por IP real do cliente. Depende do ForwardedHeadersMiddleware ja ter
    // reescrito HttpContext.Connection.RemoteIpAddress (registrado ANTES de UseRateLimiter() no
    // pipeline) — sem isso, todo o trafego atras do nginx compartilharia o mesmo IP (o do proxy).
    private static string PartitionKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
