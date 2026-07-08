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
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var dbContext = httpContext.RequestServices.GetRequiredService<AfiliadoBotDbContext>();

        var configuredPassword = dbContext.AppSettings
            .Where(s => s.Key == "hangfire.dashboard_password")
            .Select(s => s.Value)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(configuredPassword))
            return false;

        var providedPassword = httpContext.Request.Query["password"].ToString();

        return !string.IsNullOrEmpty(providedPassword) && providedPassword == configuredPassword;
    }
}
