using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using FluentAssertions;

namespace AfiliadoBot.Tests.Domain;

public class PublicationQueueTests
{
    private static PublicationQueue CriarQueue() =>
        new PublicationQueue(
            productId: Guid.NewGuid(),
            socialNetwork: SocialNetwork.Telegram,
            scheduledAt: DateTime.UtcNow.AddHours(1));

    [Fact]
    public void RegisterAttempt_SetsPublished_WhenSuccess()
    {
        var queue = CriarQueue();
        queue.RegisterAttempt(success: true);
        queue.Status.Should().Be(PublicationStatus.Published);
        queue.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public void RegisterAttempt_IncrementsRetryCount_WhenFailed()
    {
        var queue = CriarQueue();
        queue.RegisterAttempt(success: false, errorMessage: "Timeout");
        queue.Status.Should().Be(PublicationStatus.Failed);
        queue.RetryCount.Should().Be(1);
        queue.ErrorMessage.Should().Be("Timeout");
    }

    [Fact]
    public void CanRetry_ReturnsFalse_WhenRetryCountIs3()
    {
        var queue = CriarQueue();
        queue.RegisterAttempt(success: false, errorMessage: "err1");
        queue.RegisterAttempt(success: false, errorMessage: "err2");
        queue.RegisterAttempt(success: false, errorMessage: "err3");
        queue.RetryCount.Should().Be(3);
        queue.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_ReturnsFalse_WhenStatusIsNotFailed()
    {
        var queue = CriarQueue();
        // Status = Scheduled (inicial) => CanRetry deve ser false
        queue.Status.Should().Be(PublicationStatus.Scheduled);
        queue.CanRetry.Should().BeFalse();
    }
}
