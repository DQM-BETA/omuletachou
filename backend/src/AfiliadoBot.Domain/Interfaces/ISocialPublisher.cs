using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Interfaces;

public interface ISocialPublisher
{
    SocialNetwork Network { get; }
    Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default);
}
