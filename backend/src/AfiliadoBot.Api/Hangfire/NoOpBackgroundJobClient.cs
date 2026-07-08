using global::Hangfire;
using global::Hangfire.Common;
using global::Hangfire.States;

namespace AfiliadoBot.Api.Hangfire;

/// <summary>
/// Implementacao no-op de <see cref="IBackgroundJobClient"/>, usada apenas quando o Hangfire
/// esta desligado ("Hangfire:Enabled=false", ambiente de testes de integracao — ver
/// CustomWebApplicationFactory). Permite que classes como <c>CollectorJob</c> continuem
/// resolviveis via DI sem exigir um JobStorage/Postgres real.
/// </summary>
public class NoOpBackgroundJobClient : IBackgroundJobClient
{
    public string Create(Job job, IState state) => Guid.NewGuid().ToString();

    public bool ChangeState(string jobId, IState state, string? expectedState) => true;
}
