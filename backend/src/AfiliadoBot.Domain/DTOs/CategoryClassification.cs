namespace AfiliadoBot.Domain.DTOs;

/// <summary>
/// Resultado do fallback de categorizacao via IA (Issue #167 — Sub-B/#169). Category vem sempre
/// preenchida quando o classificador retorna algo utilizavel; Subcategory pode ser null (ex.:
/// categoria "Geral" nao tem subcategoria, ou a IA nao retornou uma).
/// </summary>
public record CategoryClassification(string Category, string? Subcategory);
