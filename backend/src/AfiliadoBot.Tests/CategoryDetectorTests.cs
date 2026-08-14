using AfiliadoBot.Domain.Services;
using FluentAssertions;

namespace AfiliadoBot.Tests;

public class CategoryDetectorTests
{
    [Fact]
    public void Detect_RetornaEletronicos_QuandoTituloContemFone()
    {
        var (categoria, subcategoria) = CategoryDetector.Detect("Fone de Ouvido Bluetooth");
        categoria.Should().Be("Eletrônicos");
        subcategoria.Should().Be("Áudio");
    }

    [Fact]
    public void Detect_RetornaCasaECozinha_QuandoTituloContemAirfryer()
    {
        var (categoria, subcategoria) = CategoryDetector.Detect("Airfryer Digital 5L");
        categoria.Should().Be("Casa e Cozinha");
        subcategoria.Should().Be("Eletroportáteis");
    }

    [Fact]
    public void Detect_RetornaGeral_QuandoNenhumaPalavraChaveBate()
    {
        var (categoria, subcategoria) = CategoryDetector.Detect("Produto Generico Sem Categoria Definida");
        categoria.Should().Be("Geral");
        subcategoria.Should().BeNull();
    }

    [Fact]
    public void Detect_EhCaseInsensitive()
    {
        var (categoria, subcategoria) = CategoryDetector.Detect("FONE DE OUVIDO BLUETOOTH");
        categoria.Should().Be("Eletrônicos");
        subcategoria.Should().Be("Áudio");
    }

    [Fact]
    public void Detect_RetornaGeral_QuandoTituloVazio()
    {
        var (categoria, subcategoria) = CategoryDetector.Detect(string.Empty);
        categoria.Should().Be("Geral");
        subcategoria.Should().BeNull();
    }

    [Theory]
    // Eletrodomésticos
    [InlineData("Geladeira Frost Free 400L", "Eletrodomésticos", "Refrigeração")]
    [InlineData("Máquina de Lavar 12kg", "Eletrodomésticos", "Lavanderia")]
    [InlineData("Fogão 5 Bocas Inox", "Eletrodomésticos", "Fogões e Fornos")]
    [InlineData("Micro-ondas 30L Branco", "Eletrodomésticos", "Micro-ondas")]
    [InlineData("Adega Climatizada 12 Garrafas", "Eletrodomésticos", "Adega e Bebidas")]
    // Climatização
    [InlineData("Ar-Condicionado Split 12000 BTUs", "Climatização", "Ar-condicionado")]
    [InlineData("Ventilador de Mesa Turbo", "Climatização", "Ventilação")]
    [InlineData("Aquecedor a Gás de Passagem", "Climatização", "Aquecimento")]
    [InlineData("Umidificador de Ar Ultrassônico", "Climatização", "Umidificação")]
    // Ferramentas
    [InlineData("Furadeira de Impacto 750W", "Ferramentas", "Ferramentas Elétricas")]
    [InlineData("Jogo de Chaves Combinadas 12 Peças", "Ferramentas", "Ferramentas Manuais")]
    [InlineData("Trena a Laser 40m", "Ferramentas", "Medição e Precisão")]
    [InlineData("Cortador de Grama a Gasolina", "Ferramentas", "Jardim e Área Externa")]
    // Eletrônicos
    [InlineData("Smartphone 128GB Tela 6.5", "Eletrônicos", "Celulares e Smartphones")]
    [InlineData("Notebook 8GB RAM SSD 256GB", "Eletrônicos", "Informática")]
    [InlineData("Smart TV 50 Polegadas 4K", "Eletrônicos", "TV e Imagem")]
    [InlineData("Câmera Digital Profissional", "Eletrônicos", "Fotografia")]
    // Casa e Cozinha
    [InlineData("Jogo de Panelas Antiaderente 5 Peças", "Casa e Cozinha", "Panelas e Utensílios")]
    [InlineData("Aspirador de Pó Vertical", "Casa e Cozinha", "Limpeza Doméstica")]
    [InlineData("Travesseiro Nasa Viscoelástico", "Casa e Cozinha", "Cama, Mesa e Banho")]
    [InlineData("Sofá Retrátil 3 Lugares", "Casa e Cozinha", "Decoração e Móveis")]
    // Beleza
    [InlineData("Perfume Importado 100ml", "Beleza", "Perfumaria")]
    [InlineData("Batom Matte Longa Duração", "Beleza", "Maquiagem")]
    [InlineData("Creme Facial Hidratante Noturno", "Beleza", "Cuidados com a Pele")]
    [InlineData("Shampoo Reconstrução Capilar", "Beleza", "Cuidados com o Cabelo")]
    // Moda
    [InlineData("Vestido Longo Estampado", "Moda", "Roupas Femininas")]
    [InlineData("Camisa Social Slim Fit", "Moda", "Roupas Masculinas")]
    [InlineData("Tênis Esportivo Confortável", "Moda", "Calçados")]
    [InlineData("Bolsa Transversal de Couro", "Moda", "Acessórios")]
    // Brinquedos
    [InlineData("Boneca Interativa Fala Frases", "Brinquedos", "Bonecas e Bonecos")]
    [InlineData("Lego Classic 500 Peças", "Brinquedos", "Blocos de Montar")]
    [InlineData("Jogo de Tabuleiro Família", "Brinquedos", "Jogos e Brincadeiras")]
    [InlineData("Carrinho de Brinquedo Controle Remoto", "Brinquedos", "Veículos de Brinquedo")]
    public void Detect_RetornaCategoriaESubcategoria_ParaCadaCategoriaDoDicionario(
        string titulo, string categoriaEsperada, string subcategoriaEsperada)
    {
        var (categoria, subcategoria) = CategoryDetector.Detect(titulo);

        categoria.Should().Be(categoriaEsperada);
        subcategoria.Should().Be(subcategoriaEsperada);
    }
}
