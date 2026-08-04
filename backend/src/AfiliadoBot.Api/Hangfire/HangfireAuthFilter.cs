using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AfiliadoBot.Infrastructure.Data;
using global::Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Hangfire;

/// <summary>
/// Autorizacao simples do dashboard Hangfire (/hangfire): compara a senha configurada em
/// app_settings ("hangfire.dashboard_password") com o parametro de query string "?password=".
/// Se a chave estiver vazia, o acesso e sempre negado (Issue #7, CA23/CA24).
/// Limitacao conhecida: senha trafega em texto puro na query string (sem HTTPS obrigatorio
/// nesta versao) — aceitavel para o escopo desta issue (uso interno/administrativo).
///
/// Hardening (Issue #133 / #145, item A2):
/// 1. Comparacao de senha em tempo constante (<see cref="CryptographicOperations.FixedTimeEquals"/>)
///    para nao vazar, via timing, quantos bytes iniciais da senha configurada coincidem com o
///    valor informado.
/// 2. Lockout simples por IP: 5 tentativas com senha incorreta em uma janela de 5 minutos
///    bloqueiam o mesmo IP ate a janela expirar, mesmo que a tentativa seguinte use a senha
///    correta. Implementado com um <see cref="ConcurrentDictionary{TKey,TValue}"/> estatico em
///    memoria (nao um Controller/middleware do pipeline ASP.NET — o Hangfire Dashboard usa seu
///    proprio <see cref="IDashboardAuthorizationFilter"/>, entao [EnableRateLimiting]/
///    UseRateLimiter() nao se aplica aqui). Estado nao sobrevive a restart nem e compartilhado
///    entre multiplas instancias do container "api" — aceitavel para o cenario atual (1 unico
///    container).
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    internal const int MaxAttempts = 5;
    internal static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(5);

    private static readonly ConcurrentDictionary<string, LockoutState> AttemptsByIp = new();

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (IsLockedOut(clientKey))
            return false;

        var dbContext = httpContext.RequestServices.GetRequiredService<AfiliadoBotDbContext>();

        var configuredPassword = dbContext.AppSettings
            .Where(s => s.Key == "hangfire.dashboard_password")
            .Select(s => s.Value)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(configuredPassword))
            return false;

        var providedPassword = httpContext.Request.Query["password"].ToString();

        if (string.IsNullOrEmpty(providedPassword) || !FixedTimeEquals(providedPassword, configuredPassword))
        {
            RegisterFailedAttempt(clientKey);
            return false;
        }

        // Sucesso: nao mantem o contador de falhas anteriores para o mesmo IP.
        AttemptsByIp.TryRemove(clientKey, out _);
        return true;
    }

    /// <summary>
    /// Compara duas strings em tempo constante independente do tamanho, evitando vazar via
    /// timing tanto o prefixo coincidente quanto o comprimento da senha configurada: ambas sao
    /// hasheadas (SHA-256) antes da comparacao byte-a-byte, produzindo sempre buffers de tamanho
    /// fixo (32 bytes).
    /// </summary>
    private static bool FixedTimeEquals(string provided, string configured)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));

        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }

    private static bool IsLockedOut(string clientKey)
    {
        if (!AttemptsByIp.TryGetValue(clientKey, out var state))
            return false;

        if (DateTime.UtcNow - state.WindowStart >= LockoutWindow)
        {
            // Janela expirada: remove o estado antigo, nao esta mais bloqueado.
            AttemptsByIp.TryRemove(clientKey, out _);
            return false;
        }

        return state.Attempts >= MaxAttempts;
    }

    private static void RegisterFailedAttempt(string clientKey)
    {
        AttemptsByIp.AddOrUpdate(
            clientKey,
            _ => new LockoutState(1, DateTime.UtcNow),
            (_, existing) =>
            {
                if (DateTime.UtcNow - existing.WindowStart >= LockoutWindow)
                    return new LockoutState(1, DateTime.UtcNow);

                return existing with { Attempts = existing.Attempts + 1 };
            });
    }

    /// <summary>
    /// Reseta o estado de lockout em memoria — uso exclusivo dos testes (isolamento entre
    /// casos, evita que um teste vaze contadores de tentativas para outro via o mesmo IP
    /// simulado/"unknown").
    /// </summary>
    internal static void ResetLockoutStateForTests() => AttemptsByIp.Clear();

    private sealed record LockoutState(int Attempts, DateTime WindowStart);
}
