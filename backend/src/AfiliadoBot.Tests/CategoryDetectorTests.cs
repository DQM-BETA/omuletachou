using AfiliadoBot.Application;
using FluentAssertions;

namespace AfiliadoBot.Tests;

public class CategoryDetectorTests
{
    [Fact]
    public void Detect_RetornaEletronicos_QuandoTituloContemFone()
    {
        var categoria = CategoryDetector.Detect("Fone de Ouvido Bluetooth");
        categoria.Should().Be("Eletrônicos");
    }

    [Fact]
    public void Detect_RetornaCasaECozinha_QuandoTituloContemAirfryer()
    {
        var categoria = CategoryDetector.Detect("Airfryer Digital 5L");
        categoria.Should().Be("Casa e Cozinha");
    }

    [Fact]
    public void Detect_RetornaGeral_QuandoNenhumaPalavraChaveBate()
    {
        var categoria = CategoryDetector.Detect("Produto Generico Sem Categoria Definida");
        categoria.Should().Be("Geral");
    }

    [Fact]
    public void Detect_EhCaseInsensitive()
    {
        var categoria = CategoryDetector.Detect("FONE DE OUVIDO BLUETOOTH");
        categoria.Should().Be("Eletrônicos");
    }

    [Fact]
    public void Detect_RetornaGeral_QuandoTituloVazio()
    {
        var categoria = CategoryDetector.Detect(string.Empty);
        categoria.Should().Be("Geral");
    }
}
