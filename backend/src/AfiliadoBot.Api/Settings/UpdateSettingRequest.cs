namespace AfiliadoBot.Api.Settings;

/// <summary>Corpo de <c>PUT /api/settings/{key}</c> (Issue #11 / Sub-C, CA-C4).</summary>
public record UpdateSettingRequest(string? Value);
