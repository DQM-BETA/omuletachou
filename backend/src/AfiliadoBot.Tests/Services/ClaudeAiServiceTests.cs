using AfiliadoBot.Domain.DTOs;
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

    private static Mock<IClaudeBudgetService> CreateBudgetServiceMock(bool available = true)
    {
        var mock = new Mock<IClaudeBudgetService>();
        mock.Setup(b => b.IsCategorizationBudgetAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(available);
        return mock;
    }

    [Fact]
    public async Task ScoreProductAsync_RetornaApprove_QuandoScoreAcimaDoThreshold()
    {
        // Arrange
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaudeCompletionResult("{\"score\": 8, \"reason\": \"Otimo desconto\"}", 100, 20));

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object, minScore: 6, minScoreFallback: 5);

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
            .ReturnsAsync(new ClaudeCompletionResult("{\"score\": 4, \"reason\": \"Desconto baixo\"}", 100, 20));

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object, minScore: 6, minScoreFallback: 5);

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
            .ReturnsAsync(new ClaudeCompletionResult("Claro! {\"score\":8,\"reason\":\"otimo\"}", 100, 20));

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object, minScore: 6, minScoreFallback: 5);

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

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object, minScore: 6, minScoreFallback: 5);

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
            .ReturnsAsync(new ClaudeCompletionResult(expectedCaption, 100, 20));

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object);

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

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object);
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
            .ReturnsAsync(new ClaudeCompletionResult("Legenda gerada com sucesso!", 100, 20));

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object);

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
            .ReturnsAsync(new ClaudeCompletionResult(captionWithHashtagsAndEmoji, 100, 20));

        var service = new ClaudeAiService(mockWrapper.Object, CreateBudgetServiceMock().Object);

        // Act
        var result = await service.GenerateCaptionAsync(CreateProduct(), SocialNetwork.Instagram);

        // Assert
        result.Should().Contain("#");
    }

    // ---- ClassifyCategoryAsync (Issue #167 — Sub-B/#169) ----

    [Fact]
    public async Task ClassifyCategoryAsync_RetornaNull_SemChamarApi_QuandoOrcamentoIndisponivel()
    {
        // CA 4.3: orcamento estourado -> nenhuma nova chamada de categorizacao e feita.
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        var budgetMock = CreateBudgetServiceMock(available: false);

        var service = new ClaudeAiService(mockWrapper.Object, budgetMock.Object);

        var result = await service.ClassifyCategoryAsync(CreateProduct());

        result.Should().BeNull();
        mockWrapper.Verify(
            w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        budgetMock.Verify(
            b => b.RecordUsageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyCategoryAsync_RetornaClassificacao_ERegistraUso_QuandoSucesso()
    {
        // CA 3.1/4.2: chamada bem-sucedida -> classificacao retornada e uso debitado com os
        // tokens reais reportados pela API (nao um valor estimado).
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaudeCompletionResult(
                "{\"category\": \"Eletrônicos\", \"subcategory\": \"Celulares e Smartphones\"}", 250, 40));
        var budgetMock = CreateBudgetServiceMock(available: true);

        var service = new ClaudeAiService(mockWrapper.Object, budgetMock.Object);

        var result = await service.ClassifyCategoryAsync(CreateProduct());

        result.Should().NotBeNull();
        result!.Category.Should().Be("Eletrônicos");
        result.Subcategory.Should().Be("Celulares e Smartphones");
        budgetMock.Verify(b => b.RecordUsageAsync(250, 40, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyCategoryAsync_RetornaNull_SemDebitarOrcamento_QuandoApiFalha()
    {
        // design.md §3.6: erro/timeout -> null, sem debitar orcamento (so chamadas
        // bem-sucedidas contam — CA 4.2).
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API indisponivel"));
        var budgetMock = CreateBudgetServiceMock(available: true);

        var service = new ClaudeAiService(mockWrapper.Object, budgetMock.Object);

        var result = await service.ClassifyCategoryAsync(CreateProduct());

        result.Should().BeNull();
        budgetMock.Verify(
            b => b.RecordUsageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyCategoryAsync_RetornaNull_SemDebitarOrcamento_QuandoRespostaNaoParseavel()
    {
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaudeCompletionResult("Isso nao e JSON nenhum.", 100, 20));
        var budgetMock = CreateBudgetServiceMock(available: true);

        var service = new ClaudeAiService(mockWrapper.Object, budgetMock.Object);

        var result = await service.ClassifyCategoryAsync(CreateProduct());

        result.Should().BeNull();
        budgetMock.Verify(
            b => b.RecordUsageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyCategoryAsync_AceitaSubcategoryNulo_QuandoRespostaEGeral()
    {
        var mockWrapper = new Mock<IAnthropicClientWrapper>();
        mockWrapper
            .Setup(w => w.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaudeCompletionResult("{\"category\": \"Geral\", \"subcategory\": null}", 100, 20));
        var budgetMock = CreateBudgetServiceMock(available: true);

        var service = new ClaudeAiService(mockWrapper.Object, budgetMock.Object);

        var result = await service.ClassifyCategoryAsync(CreateProduct());

        result.Should().NotBeNull();
        result!.Category.Should().Be("Geral");
        result.Subcategory.Should().BeNull();
    }
}
