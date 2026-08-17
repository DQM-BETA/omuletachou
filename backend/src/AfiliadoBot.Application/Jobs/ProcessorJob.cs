using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Application.Jobs;

/// <summary>
/// Job (Hangfire) que orquestra o processamento de produtos aprovados (Status = Queued):
/// mudanca de estado (lock otimista), download de midia, slug, categoria, link de afiliado
/// do MercadoLivre, geracao de legenda por rede e montagem da fila de publicacao com
/// agendamento por round-robin (Issue #6).
/// </summary>
public class ProcessorJob
{
    private const string AffiliateLinkUrl = "https://api.mercadolibre.com/affiliate-tools/links";

    // Horarios fixos do cron do publisher (9h/12h/15h/18h/20h).
    private static readonly int[] RoundRobinHours = { 9, 12, 15, 18, 20 };

    private static readonly (SocialNetwork Network, string EnabledKey, string[] CredentialKeys)[] NetworkSettings =
    {
        (SocialNetwork.Telegram, "networks.telegram.enabled", new[] { "telegram.bot_token", "telegram.channel_id" }),
        (SocialNetwork.Youtube, "networks.youtube.enabled", new[] { "youtube.client_id", "youtube.client_secret", "youtube.refresh_token" }),
        (SocialNetwork.Instagram, "networks.instagram.enabled", new[] { "instagram.access_token", "instagram.page_id" }),
        (SocialNetwork.TikTok, "networks.tiktok.enabled", new[] { "tiktok.access_token" }),
        (SocialNetwork.Facebook, "networks.facebook.enabled", new[] { "facebook.access_token", "facebook.page_id" }),
    };

    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;
    private readonly IAiService _aiService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProcessorJob> _logger;
    private readonly Random _random = new();

    public ProcessorJob(
        AfiliadoBotDbContext dbContext,
        IMediaStorage mediaStorage,
        IAiService aiService,
        HttpClient httpClient,
        ILogger<ProcessorJob> logger)
    {
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
        _aiService = aiService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var products = await _dbContext.Products
            .Where(p => p.Status == ProductStatus.Queued)
            .OrderByDescending(p => p.AiScore)
            .ToListAsync(ct);

        if (products.Count == 0)
            return;

        var settingsMap = await LoadAppSettingsAsync(ct);
        var slots = BuildRoundRobinSlots(products.Count);

        for (var i = 0; i < products.Count; i++)
        {
            var product = products[i];

            product.MarkAsProcessing();
            await _dbContext.SaveChangesAsync(ct);

            await DownloadMediaAsync(product, ct);

            // Categorizacao por dicionario (CategoryDetector) saiu deste job e passou a rodar
            // nos collectors, na criacao do Product (Issue #167, Sub-A/#168). O fallback via IA
            // roda aqui, ANTES do slug (Issue #167 — Sub-B/#169, CA 3.1, design.md §3.6): so
            // produtos ainda "Geral" pos-coleta (dicionario nao achou match) e ja aprovados
            // (Status == Queued, garantido pela query do topo deste metodo) sao elegiveis.
            await EnsureCategoryFallbackAsync(product, ct);

            EnsureSlug(product);

            var linkOk = await EnsureAffiliateLinkAsync(product, ct);
            if (!linkOk)
            {
                await _dbContext.SaveChangesAsync(ct);
                continue;
            }

            var queuedCount = await CreatePublicationQueueEntriesAsync(product, settingsMap, slots[i], ct);

            if (queuedCount == 0)
            {
                // Item A4 (Issue #133 / #145): nenhuma rede qualificou (nenhuma habilitada com
                // credenciais completas) — nao marcar como Published incondicionalmente.
                // Reaproveita ProductStatus.Error (sem introduzir novo status de dominio).
                product.MarkAsError("Nenhuma rede social habilitada com credenciais validas para publicar este produto.");
            }
            else
            {
                product.MarkAsPublished();
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task DownloadMediaAsync(Product product, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.MediaUrl))
            return;

        var (localPath, mediaType) = await _mediaStorage.DownloadAsync(product.MediaUrl, ct);
        product.SetLocalMedia(localPath, mediaType);

        if (localPath is null)
        {
            _logger.LogWarning(
                "ProcessorJob: falha ao baixar midia do produto {ProductId}. Produto segue sem midia local.",
                product.Id);
        }
    }

    /// <summary>
    /// Fallback de categorizacao via IA (Issue #167 — Sub-B/#169, CA 3.1-3.3). So aciona
    /// ClassifyCategoryAsync quando o dicionario (CategoryDetector, na coleta) ainda nao
    /// classificou o produto (Category == "Geral") — CA 3.3. O filtro Status == Queued (CA 3.2)
    /// ja e garantido pela query do topo de ExecuteAsync (produtos rejeitados nunca chegam neste
    /// ponto do loop), entao nao repete a checagem aqui (design.md §3.6).
    /// Classification null (orcamento estourado — CA 4.3 — ou erro/timeout da chamada) mantem o
    /// produto em "Geral", sem excecao, sem bloquear o restante do loop (mesma postura ja usada
    /// para GenerateCaptionAsync/ScoreProductAsync).
    /// </summary>
    private async Task EnsureCategoryFallbackAsync(Product product, CancellationToken ct)
    {
        if (!string.Equals(product.Category, "Geral", StringComparison.OrdinalIgnoreCase))
            return;

        _logger.LogInformation(
            "ProcessorJob: acionando fallback de categorizacao via IA para o produto {ProductId}.",
            product.Id);

        var classification = await _aiService.ClassifyCategoryAsync(product, ct);
        if (classification is not null)
        {
            product.SetCategory(classification.Category, classification.Subcategory);
            _logger.LogInformation(
                "ProcessorJob: fallback classificou o produto {ProductId} como {Category}/{Subcategory}.",
                product.Id, classification.Category, classification.Subcategory);
        }
        else
        {
            _logger.LogInformation(
                "ProcessorJob: fallback nao classificou o produto {ProductId} (orcamento indisponivel ou falha na chamada) — categoria permanece Geral.",
                product.Id);
        }
    }

    private static void EnsureSlug(Product product)
    {
        if (!string.IsNullOrWhiteSpace(product.Slug))
            return;

        var slug = $"{Slugify(product.Title)}-{product.Id.ToString()[..6]}";
        product.SetSlugIfEmpty(slug);
    }

    private static string Slugify(string title)
    {
        var sb = new StringBuilder();
        foreach (var c in title.ToLowerInvariant().Trim())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        var slug = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "produto" : slug;
    }

    /// <summary>
    /// Garante o link de afiliado do MercadoLivre quando ausente. Amazon/Shopee ou produtos
    /// com AffiliateLink ja preenchido nao sofrem chamada HTTP adicional (CA13).
    /// </summary>
    /// <returns>false quando a chamada falhou e o produto foi marcado como Error (CA6, CA14).</returns>
    private async Task<bool> EnsureAffiliateLinkAsync(Product product, CancellationToken ct)
    {
        if (product.Platform != Platform.MercadoLivre || !string.IsNullOrWhiteSpace(product.AffiliateLink))
            return true;

        if (string.IsNullOrWhiteSpace(product.SourceUrl))
        {
            _logger.LogWarning(
                "ProcessorJob: SourceUrl ausente para o produto {ProductId}. Nao e possivel gerar link de afiliado ML.",
                product.Id);
            product.MarkAsError("SourceUrl ausente — nao e possivel gerar link de afiliado ML");
            return false;
        }

        var permalink = product.SourceUrl;

        var accessToken = (await _dbContext.AppSettings
            .Where(s => s.Key == "mercadolivre.access_token")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct)) ?? string.Empty;

        var request = new HttpRequestMessage(HttpMethod.Post, AffiliateLinkUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { url = permalink }),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ProcessorJob: falha de rede ao gerar link de afiliado ML para o produto {ProductId}.", product.Id);
            product.MarkAsError($"Falha ao gerar link de afiliado ML: {ex.Message}");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ProcessorJob: resposta HTTP {StatusCode} ao gerar link de afiliado ML para o produto {ProductId}.",
                (int)response.StatusCode, product.Id);
            product.MarkAsError($"Falha ao gerar link de afiliado ML: HTTP {(int)response.StatusCode}");
            return false;
        }

        string? link;
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            link = doc.RootElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProcessorJob: resposta invalida ao gerar link de afiliado ML para o produto {ProductId}.", product.Id);
            product.MarkAsError($"Falha ao gerar link de afiliado ML: resposta invalida ({ex.Message})");
            return false;
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            product.MarkAsError("Falha ao gerar link de afiliado ML: resposta sem url");
            return false;
        }

        product.SetAffiliateLink(link);
        return true;
    }

    /// <returns>
    /// Quantidade de entradas de <see cref="PublicationQueue"/> efetivamente adicionadas ao
    /// contexto para o produto (item A4 — usado por <see cref="ExecuteAsync"/> para decidir
    /// entre <c>MarkAsPublished</c> e <c>MarkAsError</c>).
    /// </returns>
    private async Task<int> CreatePublicationQueueEntriesAsync(
        Product product,
        IReadOnlyDictionary<string, string> settingsMap,
        DateTime scheduledAt,
        CancellationToken ct)
    {
        var queuedCount = 0;

        foreach (var (network, enabledKey, credentialKeys) in NetworkSettings)
        {
            if (!IsTrue(settingsMap, enabledKey))
                continue;

            if (!HasCredentials(settingsMap, credentialKeys))
            {
                _logger.LogWarning(
                    "ProcessorJob: rede {Network} habilitada mas sem credenciais configuradas. Pulando para o produto {ProductId}.",
                    network, product.Id);
                continue;
            }

            // Fix retroativo (Issue #8 / #65, generalizado na Issue #9 / #73): Youtube e Instagram
            // exigem video disponivel — produto sem MediaType="video" (ou sem MediaLocalPath/
            // MediaUrl) nunca e enfileirado para essas redes. Demais redes seguem a regra atual,
            // inalterada (CA16-CA18).
            if ((network == SocialNetwork.Youtube || network == SocialNetwork.Instagram) && !HasVideoAvailable(product))
            {
                _logger.LogInformation(
                    "ProcessorJob: produto {ProductId} sem midia de video disponivel. Rede {Network} nao sera enfileirada.",
                    product.Id, network);
                continue;
            }

            var caption = await _aiService.GenerateCaptionAsync(product, network, ct);

            var entry = new PublicationQueue(product.Id, network, scheduledAt, caption);

            if (network == SocialNetwork.Facebook)
                entry.MarkAsManualPending();

            _dbContext.PublicationQueues.Add(entry);
            queuedCount++;
        }

        return queuedCount;
    }

    /// <summary>
    /// Youtube e Instagram exigem video disponivel (Issue #8 / #65 e Issue #9 / #73, CA16/CA17):
    /// considera "com video" quando MediaType == "video" e ao menos uma das fontes
    /// (MediaLocalPath ou MediaUrl) esta preenchida.
    /// </summary>
    private static bool HasVideoAvailable(Product product)
    {
        return string.Equals(product.MediaType, "video", StringComparison.OrdinalIgnoreCase) &&
               (!string.IsNullOrWhiteSpace(product.MediaLocalPath) || !string.IsNullOrWhiteSpace(product.MediaUrl));
    }

    private static bool IsTrue(IReadOnlyDictionary<string, string> settingsMap, string key)
    {
        return settingsMap.TryGetValue(key, out var value) &&
               bool.TryParse(value, out var parsed) &&
               parsed;
    }

    private static bool HasCredentials(IReadOnlyDictionary<string, string> settingsMap, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!settingsMap.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                return false;
        }

        return true;
    }

    private async Task<Dictionary<string, string>> LoadAppSettingsAsync(CancellationToken ct)
    {
        var keys = NetworkSettings
            .SelectMany(n => new[] { n.EnabledKey }.Concat(n.CredentialKeys))
            .ToArray();

        return await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
    }

    /// <summary>
    /// Calcula os horarios de ScheduledAt (round-robin) para todo o lote do ciclo atual.
    /// Indice 0 -> 9h de hoje (ou proximo slot futuro), 1 -> 12h, ..., 5 -> 9h do dia seguinte.
    /// Offset aleatorio de 0-10 minutos por slot.
    /// </summary>
    private DateTime[] BuildRoundRobinSlots(int count)
    {
        var slots = new DateTime[count];
        var today = DateTime.UtcNow.Date;

        for (var i = 0; i < count; i++)
        {
            var dayOffset = i / RoundRobinHours.Length;
            var hour = RoundRobinHours[i % RoundRobinHours.Length];
            var offsetMinutes = _random.Next(0, 11);

            slots[i] = today.AddDays(dayOffset).AddHours(hour).AddMinutes(offsetMinutes);
        }

        return slots;
    }
}
