using AfiliadoBot.Domain.Entities;

namespace AfiliadoBot.Domain.Interfaces;

public interface IAiService
{
    Task<(bool Approve, int Score, string Reason, string Caption)> EvaluateProductAsync(
        Product product, CancellationToken ct = default);
}
