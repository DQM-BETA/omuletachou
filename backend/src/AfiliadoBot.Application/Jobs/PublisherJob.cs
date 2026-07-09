using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Application.Jobs;

/// <summary>
/// Job (Hangfire) que processa a fila de publicacao (<see cref="PublicationQueue"/>):
/// busca itens Scheduled vencidos ou Failed com CanRetry=true, ordenados por
/// ScheduledAt/CreatedAt ASC, resolve o <see cref="ISocialPublisher"/> da rede e publica.
/// Itens ManualPending nunca sao selecionados (Issue #7, CA7-CA22).
/// </summary>
public class PublisherJob
{
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IEnumerable<ISocialPublisher> _publishers;
    private readonly ILogger<PublisherJob> _logger;

    public PublisherJob(
        AfiliadoBotDbContext dbContext,
        IEnumerable<ISocialPublisher> publishers,
        ILogger<PublisherJob> logger)
    {
        _dbContext = dbContext;
        _publishers = publishers;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var items = await _dbContext.PublicationQueues
            .Include(p => p.Product)
            .Where(p =>
                (p.Status == PublicationStatus.Scheduled && p.ScheduledAt <= now) ||
                (p.Status == PublicationStatus.Failed && p.RetryCount < 3))
            .OrderBy(p => p.ScheduledAt)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

        if (items.Count == 0)
            return;

        foreach (var item in items)
        {
            var publisher = _publishers.FirstOrDefault(p => p.Network == item.SocialNetwork);

            if (publisher is null)
            {
                _logger.LogWarning(
                    "PublisherJob: nenhum ISocialPublisher registrado para a rede {Network}. Item {ItemId} ignorado.",
                    item.SocialNetwork, item.Id);
                continue;
            }

            try
            {
                var retryCountAntes = item.RetryCount;
                var success = await publisher.PublishAsync(item, ct);

                if (success)
                {
                    // Sucesso sempre registrado incondicionalmente.
                    item.RegisterAttempt(true);
                }
                else if (item.RetryCount == retryCountAntes)
                {
                    // Publisher NAO se auto-registrou (ex.: TelegramPublisher) — registra a
                    // falha com a mensagem generica, comportamento inalterado (CA21).
                    item.RegisterAttempt(false, "Falha ao publicar (retorno negativo do publisher).");
                }
                // Senao: publisher ja se auto-registrou (ex.: YoutubePublisher.FailPermanently,
                // RetryCount mudou) — preserva a ErrorMessage especifica ja setada (CA16).
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "PublisherJob: falha ao publicar o item {ItemId} na rede {Network}.",
                    item.Id, item.SocialNetwork);
                item.RegisterAttempt(false, ex.Message);
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
