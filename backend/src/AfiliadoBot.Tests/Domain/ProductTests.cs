using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using FluentAssertions;

namespace AfiliadoBot.Tests.Domain;

public class ProductTests
{
    private static Product CriarProdutoValido(
        decimal salePrice = 100m,
        decimal discountPct = 10m,
        string? affiliateLink = "https://amzn.to/xyz",
        string category = "Eletronicos",
        string? subcategory = null) =>
        new Product(
            title: "Produto Teste",
            description: "Descricao",
            salePrice: salePrice,
            originalPrice: 120m,
            discountPct: discountPct,
            affiliateLink: affiliateLink,
            slug: "produto-teste",
            category: category,
            platform: Platform.Amazon,
            subcategory: subcategory);

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

    // Issue #199 (segundo bug) — coluna "ai_reason" e varchar(300) (ProductConfiguration). A
    // resposta real da IA (Claude, nao mock) pode facilmente passar de 300 caracteres, derrubando
    // o SaveChangesAsync do ciclo inteiro de coleta (mesma causa raiz do bug do slug).

    [Fact]
    public void UpdateAiResult_TruncaAiReason_QuandoMaiorQue300Caracteres()
    {
        var product = CriarProdutoValido();
        var reasonLongo = new string('a', 350);

        product.UpdateAiResult(score: Product.AiScoreThreshold, reason: reasonLongo, caption: "Caption");

        product.AiReason.Should().NotBeNull();
        product.AiReason!.Length.Should().Be(300);
        product.AiReason.Should().Be(new string('a', 300));
    }

    [Fact]
    public void UpdateAiResult_NaoTruncaAiReason_QuandoDentroDoLimite()
    {
        var product = CriarProdutoValido();
        product.UpdateAiResult(score: Product.AiScoreThreshold, reason: "Motivo curto", caption: "Caption");

        product.AiReason.Should().Be("Motivo curto");
    }

    [Fact]
    public void MarkAsError_TruncaAiReason_QuandoMaiorQue300Caracteres()
    {
        var product = CriarProdutoValido();
        var reasonLongo = new string('b', 350);

        product.MarkAsError(reasonLongo);

        product.AiReason.Should().NotBeNull();
        product.AiReason!.Length.Should().Be(300);
        product.AiReason.Should().Be(new string('b', 300));
    }

    [Fact]
    public void MarkAsError_NaoTruncaAiReason_QuandoDentroDoLimite()
    {
        var product = CriarProdutoValido();
        product.MarkAsError("Erro curto");

        product.AiReason.Should().Be("Erro curto");
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

    [Fact]
    public void Constructor_SetaSubcategoria_QuandoInformada()
    {
        var product = CriarProdutoValido(category: "Eletrônicos", subcategory: "Áudio");
        product.Category.Should().Be("Eletrônicos");
        product.Subcategory.Should().Be("Áudio");
    }

    [Fact]
    public void Constructor_SubcategoriaNula_QuandoNaoInformada()
    {
        var product = CriarProdutoValido();
        product.Subcategory.Should().BeNull();
    }

    [Fact]
    public void SetCategory_Sobrescreve_QuandoCategoriaAtualEhGeral()
    {
        var product = CriarProdutoValido(category: "Geral");
        product.SetCategory("Eletrônicos", "Áudio");

        product.Category.Should().Be("Eletrônicos");
        product.Subcategory.Should().Be("Áudio");
    }

    [Fact]
    public void SetCategory_NaoSobrescreve_QuandoCategoriaAtualJaEspecifica()
    {
        var product = CriarProdutoValido(category: "Moda", subcategory: "Calçados");
        product.SetCategory("Eletrônicos", "Áudio");

        product.Category.Should().Be("Moda");
        product.Subcategory.Should().Be("Calçados");
    }

    [Fact]
    public void SetCategory_NaoAlteraNada_QuandoCategoriaNovaVazia()
    {
        var product = CriarProdutoValido(category: "Geral");
        product.SetCategory("", "Áudio");

        product.Category.Should().Be("Geral");
        product.Subcategory.Should().BeNull();
    }

    [Fact]
    public void SetCategory_SubcategoriaNula_QuandoSubcategoriaNaoInformada()
    {
        var product = CriarProdutoValido(category: "Geral");
        product.SetCategory("Eletrônicos");

        product.Category.Should().Be("Eletrônicos");
        product.Subcategory.Should().BeNull();
    }

    // Issue #182/#184 — fluxo semi-manual de link de afiliado ML.

    [Fact]
    public void MarkAsAwaitingAffiliateLink_SetsStatus_AwaitingAffiliateLink()
    {
        var product = CriarProdutoValido(affiliateLink: null);
        var updatedAtAntes = product.UpdatedAt;

        product.MarkAsAwaitingAffiliateLink();

        product.Status.Should().Be(ProductStatus.AwaitingAffiliateLink);
        product.UpdatedAt.Should().BeOnOrAfter(updatedAtAntes);
    }

    [Fact]
    public void ResolveAffiliateLink_PreenchLinkEVoltaParaQueued_QuandoLinkValido()
    {
        var product = CriarProdutoValido(affiliateLink: null);
        product.MarkAsAwaitingAffiliateLink();

        product.ResolveAffiliateLink("https://mercadolivre.com/sec/abc123");

        product.AffiliateLink.Should().Be("https://mercadolivre.com/sec/abc123");
        product.Status.Should().Be(ProductStatus.Queued);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveAffiliateLink_ThrowsWhen_LinkNuloOuVazio(string? link)
    {
        var product = CriarProdutoValido(affiliateLink: null);
        product.MarkAsAwaitingAffiliateLink();

        var act = () => product.ResolveAffiliateLink(link!);

        act.Should().Throw<ArgumentException>().WithParameterName("link");
        product.Status.Should().Be(ProductStatus.AwaitingAffiliateLink);
    }
}
