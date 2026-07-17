using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace AfiliadoBot.Api.Proxy;

/// <summary>
/// ForwardedHeadersMiddleware (Issue #11 / Sub-D, design.md §3), necessario para que
/// HttpContext.Connection.RemoteIpAddress reflita o IP real do cliente atras do nginx (proxy
/// reverso, container separado na mesma rede Docker) — sem isso, o rate limiter (CA-D11/CA-D12)
/// enxergaria sempre o IP do proxy, e o limite por IP viraria efetivamente global.
/// KnownNetworks vem de "ForwardedHeaders:KnownNetworks" (lista de CIDRs, default = rede padrao
/// do Docker Compose). ForwardLimit=1: so confia no hop imediatamente a frente (nginx) — evita
/// que um cliente malicioso injete um X-Forwarded-For arbitrario para escapar do rate limit
/// (nginx deve sobrescrever, nao anexar, o header recebido do cliente).
/// </summary>
public static class ForwardedHeadersConfigurator
{
    /// <summary>
    /// CIDR padrao do Docker Compose (bridge network range privado, RFC 1918) — usado quando
    /// "ForwardedHeaders:KnownNetworks" nao estiver configurado explicitamente.
    /// </summary>
    public const string DefaultKnownNetworkCidr = "172.16.0.0/12";

    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        var cidrs = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
        cidrs = cidrs is { Length: > 0 } ? cidrs : [DefaultKnownNetworkCidr];

        foreach (var cidr in cidrs)
        {
            if (TryParseCidr(cidr, out var network))
                options.KnownNetworks.Add(network);
        }
    }

    private static bool TryParseCidr(string cidr, out Microsoft.AspNetCore.HttpOverrides.IPNetwork network)
    {
        network = default!;
        var parts = cidr.Split('/', 2);
        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out var address))
            return false;

        if (!int.TryParse(parts[1], out var prefixLength))
            return false;

        network = new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefixLength);
        return true;
    }
}
