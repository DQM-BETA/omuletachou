namespace AfiliadoBot.Api.Settings;

/// <summary>
/// Representa uma linha de <c>app_settings</c> na resposta HTTP (Issue #11 / Sub-C).
/// <see cref="Value"/> ja vem mascarado quando <see cref="Key"/> e sensivel (CA-C1/CA-C2) —
/// nunca o valor puro de uma chave sensivel chega ate aqui.
/// </summary>
public record SettingDto(string Key, string? Value);
