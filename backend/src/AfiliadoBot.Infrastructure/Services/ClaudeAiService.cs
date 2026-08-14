using System.Text.Json;
using System.Text.RegularExpressions;
using AfiliadoBot.Domain.DTOs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Domain.Services;

namespace AfiliadoBot.Infrastructure.Services;

public class ClaudeAiService : IAiService
{
    private readonly IAnthropicClientWrapper _client;
    private readonly IClaudeBudgetService _budgetService;
    private readonly int _minScore;
    private readonly int _minScoreFallback;

    private static readonly Regex JsonExtractRegex = new(
        @"\{[^{}]*""score""\s*:\s*(\d+)[^{}]*""reason""\s*:\s*""([^""]*)""|" +
        @"\{[^{}]*""reason""\s*:\s*""([^""]*)""[^{}]*""score""\s*:\s*(\d+)[^{}]*\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public ClaudeAiService(
        IAnthropicClientWrapper client,
        IClaudeBudgetService budgetService,
        int minScore = 6,
        int minScoreFallback = 5)
    {
        _client = client;
        _budgetService = budgetService;
        _minScore = minScore;
        _minScoreFallback = minScoreFallback;
    }

    public async Task<ProductScore> ScoreProductAsync(Product product, CancellationToken ct = default)
    {
        const string systemPrompt = """
            Voce e um avaliador de produtos afiliados. Avalie o produto com base nos seguintes criterios:
            - Desconto real minimo de 15%; precos inflados penalizam
            - Categorias preferidas: eletronicos, casa/cozinha, beleza, brinquedos, moda
            - Titulo sem nome descritivo (so codigo de modelo) penaliza
            - Preco final acima de R$ 2.000 penaliza
            - Prazo de entrega longo penaliza

            Responda APENAS com JSON no formato: {"score": <0-10>, "reason": "<texto curto>"}
            Nao inclua nenhum texto adicional antes ou depois do JSON.
            """;

        Exception? lastException = null;
        int[] delays = { 1000, 2000, 4000 };

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (attempt > 0)
                    await Task.Delay(delays[attempt - 1], ct);

                var userMessage = $"""
                    Produto para avaliar:
                    Titulo: {product.Title}
                    Preco de venda: R$ {product.SalePrice:F2}
                    Preco original: R$ {product.OriginalPrice:F2}
                    Desconto: {product.DiscountPct:F0}%
                    Categoria: {product.Category}
                    Plataforma: {product.Platform}
                    """;

                var response = await _client.CompleteAsync(systemPrompt, userMessage, ct);
                var (score, reason) = ParseScoreResponse(response.Text);
                bool approve = score >= _minScore;
                return new ProductScore(score, reason, approve);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        // Fallback apos 3 falhas
        return new ProductScore(_minScoreFallback, "Claude API unavailable", false);
    }

    public async Task<string> GenerateCaptionAsync(Product product, SocialNetwork network, CancellationToken ct = default)
    {
        var networkInstructions = network switch
        {
            SocialNetwork.Telegram => "Seja direto e objetivo. Coloque o preco em destaque. Maximo 200 caracteres antes do link.",
            SocialNetwork.Instagram => "Tom emocional e inspirador. Inclua 3 a 5 hashtags relevantes. CTA forte no final. Pode usar emojis.",
            SocialNetwork.TikTok => "Linguagem jovem e descontraida. Urgencia sem exageros. Maximo 150 caracteres.",
            SocialNetwork.Youtube => "Foco nos beneficios do produto. Otimizado para SEO. Entre 200 e 400 caracteres.",
            SocialNetwork.Facebook => "Tom conversacional, como uma dica entre amigos. Sem hashtags.",
            _ => "Seja objetivo e informativo."
        };

        var systemPrompt = $"""
            Voce e Mulet, um brasileiro de 30 anos do Rio de Janeiro, entusiasta de tecnologia e economia.
            Seu tom e amigavel, como se estivesse dando uma dica para um grupo de amigos ou familia.

            PROIBIDO em qualquer legenda:
            - Mencionar comissao ou vinculo de afiliado
            - Usar "oferta imperdivel"
            - Usar superlativos sem base factual

            Instrucoes para {network}:
            {networkInstructions}

            Gere APENAS a legenda, sem introducao ou explicacao.
            """;

        var userMessage = $"""
            Produto: {product.Title}
            Preco: R$ {product.SalePrice:F2}
            Desconto: {product.DiscountPct:F0}% OFF
            Categoria: {product.Category}
            Link: {product.AffiliateLink}
            Rede social alvo: {network}
            """;

        try
        {
            var response = await _client.CompleteAsync(systemPrompt, userMessage, ct);
            return response.Text;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return $"Achei essa oferta: {product.Title} por R$ {product.SalePrice:F2} ({product.DiscountPct:F0}% OFF) {product.AffiliateLink}";
        }
    }

    /// <summary>
    /// Fallback de categorizacao via IA (Issue #167 — Sub-B/#169, design.md §3.6,
    /// especificacao-tecnica.md §7). Chamado pelo ProcessorJob so quando o dicionario
    /// (CategoryDetector, na coleta) nao classificou o produto (Category == "Geral").
    /// Retorna null (sem excecao) quando: orcamento do mes estourou (CA 4.3), a chamada Claude
    /// falha/da timeout, ou a resposta nao pode ser parseada — em qualquer um desses casos o
    /// produto permanece "Geral" e o orcamento so e debitado em caso de sucesso real (CA 4.2).
    /// </summary>
    public async Task<CategoryClassification?> ClassifyCategoryAsync(Product product, CancellationToken ct = default)
    {
        if (!await _budgetService.IsCategorizationBudgetAvailableAsync(ct))
            return null;

        var taxonomia = string.Join(
            "\n",
            CategoryDetector.Categorias.Select(kv => $"- {kv.Key}: {string.Join(", ", kv.Value)}"));

        var systemPrompt = $$"""
            Voce e um classificador de produtos de e-commerce afiliado. Classifique o produto em
            UMA categoria e UMA subcategoria da lista fechada abaixo — nunca invente uma
            categoria/subcategoria fora da lista. Se nenhuma categoria da lista se encaixar bem,
            responda com categoria "Geral" e subcategoria null.

            Categorias e subcategorias disponiveis:
            {{taxonomia}}

            Responda APENAS com JSON no formato: {"category": "<categoria>", "subcategory": "<subcategoria ou null>"}
            Nao inclua nenhum texto adicional antes ou depois do JSON.
            """;

        var userMessage = $"""
            Produto para classificar:
            Titulo: {product.Title}
            Descricao: {product.Description}
            """;

        ClaudeCompletionResult response;
        try
        {
            response = await _client.CompleteAsync(systemPrompt, userMessage, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        var classification = TryParseCategoryResponse(response.Text);
        if (classification is null)
            return null;

        // So debita orcamento apos sucesso real (CA 4.2 — "chamada executada com sucesso").
        await _budgetService.RecordUsageAsync(response.InputTokens, response.OutputTokens, ct);

        return classification;
    }

    private static CategoryClassification? TryParseCategoryResponse(string response)
    {
        var match = Regex.Match(response, @"\{[^{}]*\}", RegexOptions.Singleline);
        if (!match.Success)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(match.Value);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var categoryProp))
                return null;

            var category = categoryProp.GetString();
            if (string.IsNullOrWhiteSpace(category))
                return null;

            string? subcategory = null;
            if (root.TryGetProperty("subcategory", out var subcategoryProp) &&
                subcategoryProp.ValueKind == JsonValueKind.String)
            {
                subcategory = subcategoryProp.GetString();
            }

            return new CategoryClassification(category, subcategory);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (int score, string reason) ParseScoreResponse(string response)
    {
        // Regex para extrair bloco JSON mesmo com texto adicional
        var match = Regex.Match(response,
            @"\{[^{}]*\}",
            RegexOptions.Singleline);

        if (match.Success)
        {
            try
            {
                using var doc = JsonDocument.Parse(match.Value);
                var root = doc.RootElement;

                if (root.TryGetProperty("score", out var scoreProp) &&
                    root.TryGetProperty("reason", out var reasonProp))
                {
                    int score = scoreProp.GetInt32();
                    string reason = reasonProp.GetString() ?? string.Empty;
                    return (Math.Clamp(score, 0, 10), reason);
                }
            }
            catch (JsonException)
            {
                // Fallthrough para tentativa de regex de campos
            }
        }

        // Fallback por regex de campos individuais
        var scoreMatch = Regex.Match(response, @"""score""\s*:\s*(\d+)");
        var reasonMatch = Regex.Match(response, @"""reason""\s*:\s*""([^""]*)""");

        if (scoreMatch.Success && reasonMatch.Success)
        {
            int score = int.Parse(scoreMatch.Groups[1].Value);
            string reason = reasonMatch.Groups[1].Value;
            return (Math.Clamp(score, 0, 10), reason);
        }

        throw new FormatException($"Nao foi possivel extrair score/reason da resposta: {response}");
    }
}
