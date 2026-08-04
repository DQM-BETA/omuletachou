using System.Net;
using System.Net.Sockets;
using AfiliadoBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Storage;

/// <summary>
/// Implementacao de <see cref="IMediaStorage"/> que baixa midia via HTTP para o disco local
/// (volume /app/media, persistido entre restarts via docker-compose). Usado pelo ProcessorJob
/// (Issue #6) para trazer a midia dos produtos aprovados para armazenamento proprio.
/// Segue o mesmo padrao de tratamento de erro de <c>MercadoLivreCollector</c>: nenhuma exception
/// nao capturada escapa do metodo — falha retorna LocalPath nulo e loga Warning.
///
/// SSRF allowlist (Issue #133 / #145, item A3): antes de qualquer download, o host da URL e
/// resolvido e rejeitado se apontar para loopback/rede privada/link-local (inclui o metadata
/// endpoint de nuvem 169.254.169.254). Risco aceito documentado na issue original: MediaUrl vem
/// so de collectors internos, nao de input direto do usuario final — ainda assim, defesa em
/// profundidade barata contra uma fonte externa comprometida.
/// </summary>
public class LocalMediaStorage : IMediaStorage
{
    /// <summary>
    /// Diretorio fisico raiz onde a midia local e gravada — exposto como <c>public</c> (Issue #9
    /// / #73) para que <c>Program.cs</c> mapeie o mesmo path via <c>UseStaticFiles</c>
    /// (<c>RequestPath=/media</c>), garantindo que <see cref="InstagramPublisher"/> monte URLs
    /// publicas que realmente apontam para onde os arquivos sao salvos.
    /// </summary>
    public const string MediaDirectory = "/app/media";
    private static readonly string[] VideoExtensions = { ".mp4", ".webm" };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalMediaStorage> _logger;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _hostResolver;

    public LocalMediaStorage(HttpClient httpClient, ILogger<LocalMediaStorage> logger)
        : this(httpClient, logger, (host, ct) => Dns.GetHostAddressesAsync(host, ct))
    {
    }

    /// <summary>
    /// Construtor interno usado pelos testes para injetar um resolvedor de DNS falso — evita
    /// que a suite dependa de resolucao de rede real para hosts de teste (ex.
    /// "cdn.example.com"), mantendo o allowlist SSRF (item A3) coberto de forma hermetica.
    /// </summary>
    internal LocalMediaStorage(
        HttpClient httpClient,
        ILogger<LocalMediaStorage> logger,
        Func<string, CancellationToken, Task<IPAddress[]>> hostResolver)
    {
        _httpClient = httpClient;
        _logger = logger;
        _hostResolver = hostResolver;
    }

    public async Task<(string? LocalPath, string MediaType)> DownloadAsync(string mediaUrl, CancellationToken ct = default)
    {
        var mediaType = DetectMediaType(mediaUrl);

        if (string.IsNullOrWhiteSpace(mediaUrl) || !Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("LocalMediaStorage: URL de midia invalida/malformada: {MediaUrl}", mediaUrl);
            return (null, mediaType);
        }

        if (!await IsAllowedAsync(uri, ct))
        {
            _logger.LogWarning(
                "LocalMediaStorage: URL de midia bloqueada pela allowlist SSRF (host privado/loopback/link-local): {MediaUrl}",
                mediaUrl);
            return (null, mediaType);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(mediaUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "LocalMediaStorage: falha de rede ao baixar midia de {MediaUrl}. Produto segue sem midia local.", mediaUrl);
            return (null, mediaType);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "LocalMediaStorage: timeout ao baixar midia de {MediaUrl}. Produto segue sem midia local.", mediaUrl);
            return (null, mediaType);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "LocalMediaStorage: resposta HTTP {StatusCode} ao baixar midia de {MediaUrl}. Produto segue sem midia local.",
                (int)response.StatusCode, mediaUrl);
            return (null, mediaType);
        }

        try
        {
            Directory.CreateDirectory(MediaDirectory);

            var extension = GetExtension(mediaUrl);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var localPath = Path.Combine(MediaDirectory, fileName);

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await contentStream.CopyToAsync(fileStream, ct);

            return (localPath, mediaType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "LocalMediaStorage: falha ao gravar midia local de {MediaUrl}. Produto segue sem midia local.", mediaUrl);
            return (null, mediaType);
        }
    }

    /// <summary>
    /// Allowlist SSRF: rejeita scheme diferente de http/https e qualquer host que resolva para
    /// um IP privado/loopback/link-local. Nunca lanca — falha de resolucao de DNS e tratada como
    /// "nao permitido" (mesmo padrao defensivo do restante da classe).
    /// </summary>
    private async Task<bool> IsAllowedAsync(Uri uri, CancellationToken ct)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(uri.Host, out var literalIp)
                ? new[] { literalIp }
                : await _hostResolver(uri.Host, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }

        if (addresses.Length == 0)
            return false;

        return addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10)
                return false;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                return false;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return false;

            // 169.254.0.0/16 (link-local, inclui o metadata endpoint 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254)
                return false;

            // 127.0.0.0/8 (redundante com IsLoopback, mantido por clareza)
            if (bytes[0] == 127)
                return false;

            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return false;

            var bytes = address.GetAddressBytes();

            // fc00::/7 (unique local address)
            if ((bytes[0] & 0xFE) == 0xFC)
                return false;

            return true;
        }

        // Familia de endereco desconhecida: nao permitir por padrao.
        return false;
    }

    private static string DetectMediaType(string mediaUrl)
    {
        var extension = GetExtension(mediaUrl);
        return VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? "video" : "image";
    }

    private static string GetExtension(string mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            return string.Empty;

        var pathPart = mediaUrl;

        if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
            pathPart = uri.AbsolutePath;

        var queryIndex = pathPart.IndexOf('?');
        if (queryIndex >= 0)
            pathPart = pathPart[..queryIndex];

        var extension = Path.GetExtension(pathPart);
        return extension;
    }
}
