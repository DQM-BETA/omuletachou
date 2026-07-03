using AfiliadoBot.Domain.DTOs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Interfaces;

public interface IAiService
{
    Task<ProductScore> ScoreProductAsync(Product product, CancellationToken ct = default);
    Task<string> GenerateCaptionAsync(Product product, SocialNetwork network, CancellationToken ct = default);
}
