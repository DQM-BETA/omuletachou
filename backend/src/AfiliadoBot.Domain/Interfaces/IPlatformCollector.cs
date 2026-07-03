using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Interfaces;

public interface IPlatformCollector
{
    Platform Platform { get; }
    Task<IEnumerable<Product>> CollectAsync(CancellationToken ct = default);
}
