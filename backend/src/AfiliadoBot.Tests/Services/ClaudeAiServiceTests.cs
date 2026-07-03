using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AfiliadoBot.Tests.Services;

public class ClaudeAiServiceTests
{
    private static Product CreateProduct() => new Product(
        title: "Smartphone Samsung Galaxy A55",
        description: "Smartphone top de linha",
        salePrice: 999.90m,
        originalPrice: 1299.90m,
        discountPct: 23m,
        affiliateLink: "https://amzn.to/test",
        slug: "samsung-galaxy-a55",
        category: "Eletronicos",
        platform: Platform.Amazon);

    [Fact]
    public async Task ScoreProductAsync_RetornaApprove_QuandoScoreAcimaDoThreshold()
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 8, \"reason\": \"Otimo desconto\"}");

        var service = new ClaudeAiService(mockWrapper.Object, minScore: 6, minScoreFallback: 5);

        // Act
        var result = await service.ScoreProductAsync(CreateProduct());

        // Assert
        result.Score.Should().Be(8);
        result.Approve.Should().BeTrue();
    }

    [Fact]
    public async Task ScoreProductAsync_RetornaReject_QuandoScoreAbaixoDoThreshold()
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 4, \"reason\": \"Desconto baixo\"}");

        var service = new ClaudeAiService(mockWrapper.Object, minScore: 6, minScoreFallback: 5);

        // Act
        var result = await service.ScoreProductAsync(CreateProduct());

        // Assert
        result.Score.Should().Be(4);
        result.Approve.Should().BeFalse();
    }

    [Fact]
    public async Task ScoreProductAsync_ParseResilienteComTextoExtra()
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Claro! {\"score\":8,\"reason\":\"otimo\"}");

        var service = new ClaudeAiService(mockWrapper.Object, minScore: 6, minScoreFallback: 5);

        // Act
        var result = await service.ScoreProductAsync(CreateProduct());

        // Assert
        result.Score.Should().Be(8);
        result.Reason.Should().Be("otimo");
    }

    [Fact]
    public async Task ScoreProductAsync_UsaFallback_QuandoApiIndisponivel()
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API indisponivel"));

        var service = new ClaudeAiService(mockWrapper.Object, minScore: 6, minScoreFallback: 5);

        // Act
        var result = await service.ScoreProductAsync(CreateProduct());

        // Assert
        result.Score.Should().Be(5);
        result.Approve.Should().BeFalse();
        result.Reason.Should().Be("Claude API unavailable");
    }

    [Fact]
    public async Task GenerateCaptionAsync_RetornaLegenda_QuandoApiDisponivel()
    {
        // Arrange
        var expectedCaption = "Encontrei essa oferta incrivel: Samsung Galaxy A55 por R$999,90!";
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCaption);

        var service = new ClaudeAiService(mockWrapper.Object);

        // Act
        var result = await service.GenerateCaptionAsync(CreateProduct(), SocialNetwork.Telegram);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(expectedCaption);
    }

    [Fact]
    public async Task GenerateCaptionAsync_RetornaTemplate_QuandoApiFalha()
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API indisponivel"));

        var service = new ClaudeAiService(mockWrapper.Object);
        var product = CreateProduct();

        // Act
        var result = await service.GenerateCaptionAsync(product, SocialNetwork.Telegram);

        // Assert
        result.Should().Contain(product.Title);
        result.Should().Contain(product.SalePrice.ToString("F2"));
    }

    [Theory]
    [InlineData(SocialNetwork.Telegram)]
    [InlineData(SocialNetwork.Instagram)]
    [InlineData(SocialNetwork.TikTok)]
    [InlineData(SocialNetwork.Youtube)]
    [InlineData(SocialNetwork.Facebook)]
    public async Task GenerateCaptionAsync_SuportaTodasAsRedes_SemExcecao(SocialNetwork network)
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Legenda gerada com sucesso!");

        var service = new ClaudeAiService(mockWrapper.Object);

        // Act
        var act = async () => await service.GenerateCaptionAsync(CreateProduct(), network);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GenerateCaptionAsync_Instagram_ContemHashtagsEEmojis()
    {
        // Arrange
        var captionWithHashtagsAndEmoji = "Produto top! Aproveite essa oferta! #eletronicos #samsung #oferta #tech #preco";
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(captionWithHashtagsAndEmoji);

        var service = new ClaudeAiService(mockWrapper.Object);

        // Act
        var result = await service.GenerateCaptionAsync(CreateProduct(), SocialNetwork.Instagram);

        // Assert
        result.Should().Contain("#");
    }
}
