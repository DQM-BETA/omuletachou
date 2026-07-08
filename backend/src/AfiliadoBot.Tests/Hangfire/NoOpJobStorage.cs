using global::Hangfire;
using global::Hangfire.Storage;

namespace AfiliadoBot.Tests.HangfireTests;

/// <summary>
/// JobStorage minimo apenas para satisfazer a assinatura de <c>AspNetCoreDashboardContext</c>
/// nos testes de <c>HangfireAuthFilter</c> — nunca e efetivamente usado para acessar dados
/// (o filtro so le o HttpContext), por isso os metodos abstratos lancam se chamados.
/// </summary>
public class NoOpJobStorage : JobStorage
{
    public static readonly NoOpJobStorage Instance = new();

    public override IMonitoringApi GetMonitoringApi() => throw new NotSupportedException();

    public override IStorageConnection GetConnection() => throw new NotSupportedException();
}
