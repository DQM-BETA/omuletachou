using AfiliadoBot.Domain.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Application.Jobs;

/// <summary>
/// Job (Hangfire) que orquestra a coleta de produtos das plataformas configuradas
/// (Amazon, MercadoLivre, Shopee), executando cada <see cref="IPlatformCollector"/>
/// em sequencia. Falha isolada em um collector nao impede os demais (Issue #7, CA1-CA4).
/// Ao final, encadeia o <see cref="ProcessorJob"/> via <see cref="BackgroundJob.Enqueue"/>
/// desde que ao menos uma plataforma tenha coletado com sucesso.
/// </summary>
public class CollectorJob
{
    private readonly IEnumerable<IPlatformCollector> _collectors;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<CollectorJob> _logger;

    public CollectorJob(
        IEnumerable<IPlatformCollector> collectors,
        IBackgroundJobClient backgroundJobClient,
        ILogger<CollectorJob> logger)
    {
        _collectors = collectors;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var anySuccess = false;

        foreach (var collector in _collectors)
        {
            try
            {
                var products = await collector.CollectAsync(ct);
                var count = products.Count();

                _logger.LogInformation(
                    "CollectorJob: {Platform} coletou {Count} produtos.",
                    collector.Platform,
                    count);

                anySuccess = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "CollectorJob: falha ao coletar de {Platform}.",
                    collector.Platform);
            }
        }

        if (anySuccess)
        {
            _backgroundJobClient.Enqueue<ProcessorJob>(j => j.ExecuteAsync(CancellationToken.None));
        }
        else
        {
            _logger.LogWarning("CollectorJob: todos os collectors falharam. ProcessorJob nao sera enfileirado.");
        }
    }
}
