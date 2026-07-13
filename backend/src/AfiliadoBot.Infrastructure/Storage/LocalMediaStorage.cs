using AfiliadoBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Storage;

/// <summary>
/// Implementacao de <see cref="IMediaStorage"/> que baixa midia via HTTP para o disco local
/// (volume /app/media, persistido entre restarts via docker-compose). Usado pelo ProcessorJob
/// (Issue #6) para trazer a midia dos produtos aprovados para armazenamento proprio.
/// Segue o mesmo padrao de tratamento de erro de <c>MercadoLivreCollector</c>: nenhuma exception
/// nao capturada escapa do metodo — falha retorna LocalPath nulo e loga Warning.
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

    public LocalMediaStorage(HttpClient httpClient, ILogger<LocalMediaStorage> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(string? LocalPath, string MediaType)> DownloadAsync(string mediaUrl, CancellationToken ct = default)
    {
        var mediaType = DetectMediaType(mediaUrl);

        if (string.IsNullOrWhiteSpace(mediaUrl) || !Uri.TryCreate(mediaUrl, UriKind.Absolute, out _))
        {
            _logger.LogWarning("LocalMediaStorage: URL de midia invalida/malformada: {MediaUrl}", mediaUrl);
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
