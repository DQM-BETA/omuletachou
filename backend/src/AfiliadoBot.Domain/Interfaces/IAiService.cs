using AfiliadoBot.Domain.DTOs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Interfaces;

public interface IAiService
{
    Task<ProductScore> ScoreProductAsync(Product product, CancellationToken ct = default);
    Task<string> GenerateCaptionAsync(Product product, SocialNetwork network, CancellationToken ct = default);

    /// <summary>
    /// Fallback de categorizacao via IA (Issue #167 — Sub-B/#169), chamado pelo ProcessorJob
    /// apenas quando o dicionario (CategoryDetector, na coleta) nao classificou o produto
    /// (Category == "Geral"). Retorna null quando o orcamento mensal estourou ou a chamada
    /// falhar/der timeout — nesses casos o produto permanece "Geral", sem excecao propagada.
    /// </summary>
    Task<CategoryClassification?> ClassifyCategoryAsync(Product product, CancellationToken ct = default);
}
