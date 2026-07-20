using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using FluentAssertions;

namespace AfiliadoBot.Tests.Domain;

public class ProductTests
{
    private static Product CriarProdutoValido(
        decimal salePrice = 100m,
        decimal discountPct = 10m,
        string? affiliateLink = "https://amzn.to/xyz") =>
        new Product(
            title: "Produto Teste",
            description: "Descricao",
            salePrice: salePrice,
            originalPrice: 120m,
            discountPct: discountPct,
            affiliateLink: affiliateLink,
            slug: "produto-teste",
            category: "Eletronicos",
            platform: Platform.Amazon);

    [Fact]
    public void Constructor_ThrowsWhen_SalePriceNegative()
    {
        var act = () => CriarProdutoValido(salePrice: -1m);
        act.Should().Throw<ArgumentException>().WithParameterName("salePrice");
    }

    [Fact]
    public void Constructor_ThrowsWhen_DiscountPctOutOfRange()
    {
        var act = () => CriarProdutoValido(discountPct: 101m);
        act.Should().Throw<ArgumentException>().WithParameterName("discountPct");
    }

    [Fact]
    public void Constructor_AllowsNullOrEmpty_AffiliateLink()
    {
        var act = () => CriarProdutoValido(affiliateLink: "");
        act.Should().NotThrow();

        var product = CriarProdutoValido(affiliateLink: null);
        product.AffiliateLink.Should().BeNull();
    }

    [Fact]
    public void SetAffiliateLink_UpdatesLink_WhenValid()
    {
        var product = CriarProdutoValido(affiliateLink: null);
        product.SetAffiliateLink("https://amzn.to/novo");
        product.AffiliateLink.Should().Be("https://amzn.to/novo");
    }

    [Fact]
    public void SetAffiliateLink_ThrowsWhen_LinkEmpty()
    {
        var product = CriarProdutoValido(affiliateLink: null);
        var act = () => product.SetAffiliateLink("");
        act.Should().Throw<ArgumentException>().WithParameterName("link");
    }

    [Fact]
    public void Constructor_ValidArgs_CreatesSemErro()
    {
        var product = CriarProdutoValido();
        product.Status.Should().Be(ProductStatus.Pending);
        product.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateAiResult_SetsQueued_WhenScoreAboveThreshold()
    {
        var product = CriarProdutoValido();
        product.UpdateAiResult(score: Product.AiScoreThreshold, reason: "Bom", caption: "Caption");
        product.Status.Should().Be(ProductStatus.Queued);
    }

    [Fact]
    public void UpdateAiResult_SetsRejected_WhenScoreBelowThreshold()
    {
        var product = CriarProdutoValido();
        product.UpdateAiResult(score: Product.AiScoreThreshold - 1, reason: "Ruim", caption: "Caption");
        product.Status.Should().Be(ProductStatus.Rejected);
    }

    [Fact]
    public void MarkAsPublished_ChangesStatus()
    {
        var product = CriarProdutoValido();
        product.UpdateAiResult(score: Product.AiScoreThreshold, reason: "Ok", caption: "Caption");
        product.MarkAsPublished();
        product.Status.Should().Be(ProductStatus.Published);
    }

    [Fact]
    public void UpdateStatusManually_SetsRejected_WhenValueIsRejected()
    {
        var product = CriarProdutoValido();
        product.UpdateStatusManually(ProductStatus.Rejected);
        product.Status.Should().Be(ProductStatus.Rejected);
    }

    [Fact]
    public void UpdateStatusManually_SetsPending_WhenValueIsPending()
    {
        var product = CriarProdutoValido();
        product.UpdateStatusManually(ProductStatus.Rejected);
        product.UpdateStatusManually(ProductStatus.Pending);
        product.Status.Should().Be(ProductStatus.Pending);
    }

    [Theory]
    [InlineData(ProductStatus.Queued)]
    [InlineData(ProductStatus.Published)]
    [InlineData(ProductStatus.Processing)]
    [InlineData(ProductStatus.Error)]
    public void UpdateStatusManually_ThrowsWhen_StatusNotPendingOrRejected(ProductStatus status)
    {
        var product = CriarProdutoValido();
        var act = () => product.UpdateStatusManually(status);
        act.Should().Throw<ArgumentException>().WithParameterName("status");
    }
}
