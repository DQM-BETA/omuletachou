using AfiliadoBot.Api.Hangfire;
using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Platforms;
using AfiliadoBot.Infrastructure.Integrations.Social;
using AfiliadoBot.Infrastructure.Services;
using AfiliadoBot.Infrastructure.Storage;
using global::Hangfire;
using global::Hangfire.Dashboard;
using global::Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Hangfire e desligado nos testes de integracao (WebApplicationFactory) via
// "Hangfire:Enabled=false" para evitar tentativa de conexao real ao Postgres na
// inicializacao do host de teste (AddHangfire/UsePostgreSqlStorage conecta de forma
// sincrona para preparar o schema).
var hangfireEnabled = builder.Configuration.GetValue("Hangfire:Enabled", true);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AfiliadoBotDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AI Service
builder.Services.AddScoped<IAnthropicClientWrapper>(sp =>
{
    var apiKey = builder.Configuration["Claude:ApiKey"] ?? string.Empty;
    var model = builder.Configuration["Claude:Model"] ?? string.Empty;
    return new AnthropicClientWrapper(apiKey, model);
});
builder.Services.AddScoped<IAiService>(sp =>
{
    var wrapper = sp.GetRequiredService<IAnthropicClientWrapper>();
    return new ClaudeAiService(wrapper);
});

// Collectors
// Fix DI (Issue #7 / #59): os 3 collectors precisam ser resolviveis via IPlatformCollector
// para que IEnumerable<IPlatformCollector> (usado pelo CollectorJob) enumere os 3 — antes,
// apenas o AmazonCollector estava vinculado a interface, ML e Shopee so pelo tipo concreto.
builder.Services.AddHttpClient<AmazonCollector>();
builder.Services.AddHttpClient<IPlatformCollector, AmazonCollector>();
builder.Services.AddHttpClient<MercadoLivreCollector>();
builder.Services.AddHttpClient<IPlatformCollector, MercadoLivreCollector>();
builder.Services.AddHttpClient<ShopeeCollector>();
builder.Services.AddHttpClient<IPlatformCollector, ShopeeCollector>();

// Media storage (ProcessorJob, Issue #6)
builder.Services.AddHttpClient<IMediaStorage, LocalMediaStorage>();

// ProcessorJob (Issue #6)
builder.Services.AddHttpClient<ProcessorJob>();

// CollectorJob (Issue #7 / #59)
builder.Services.AddScoped<CollectorJob>();

// Publishers (Issue #7 / #60)
builder.Services.AddHttpClient<ISocialPublisher, TelegramPublisher>();

// Publishers (Issue #8 / #65)
builder.Services.AddHttpClient<ISocialPublisher, YoutubePublisher>();

// Publishers (Issue #9 / #73)
builder.Services.AddHttpClient<ISocialPublisher, InstagramPublisher>();

// PublisherJob (Issue #7 / #60)
builder.Services.AddScoped<PublisherJob>();

// Hangfire (Issue #7 / #59)
if (hangfireEnabled)
{
    builder.Services.AddHangfire(config => config
        .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);
}
else
{
    // Sem Hangfire configurado (testes), CollectorJob ainda precisa de um IBackgroundJobClient
    // resolvivel via DI — usamos um no-op (ver NoOpBackgroundJobClient).
    builder.Services.AddSingleton<IBackgroundJobClient, NoOpBackgroundJobClient>();
}

var app = builder.Build();

// Apply pending migrations on startup (skipped when host uses non-relational provider, e.g. EF InMemory in tests)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();

        if (hangfireEnabled)
        {
            var dashboardPassword = db.AppSettings
                .Where(s => s.Key == "hangfire.dashboard_password")
                .Select(s => s.Value)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(dashboardPassword))
            {
                app.Logger.LogWarning(
                    "Hangfire: 'hangfire.dashboard_password' esta vazio em app_settings. " +
                    "O dashboard /hangfire ficara bloqueado ate que a senha seja configurada.");
            }

            var collectorCron = db.AppSettings
                .Where(s => s.Key == "schedule.collector_cron")
                .Select(s => s.Value)
                .FirstOrDefault();

            // IRecurringJobManager (via DI, resolvido do escopo) em vez da API estatica
            // RecurringJob.AddOrUpdate — a estatica depende de JobStorage.Current, que so e
            // inicializado quando o container de DI resolve JobStorage pela primeira vez.
            var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
            recurringJobManager.AddOrUpdate<CollectorJob>(
                "collector-job",
                j => j.ExecuteAsync(CancellationToken.None),
                string.IsNullOrWhiteSpace(collectorCron) ? "0 6 * * *" : collectorCron);

            var publisherCron = db.AppSettings
                .Where(s => s.Key == "schedule.publisher_cron")
                .Select(s => s.Value)
                .FirstOrDefault();

            recurringJobManager.AddOrUpdate<PublisherJob>(
                "publisher-job",
                j => j.ExecuteAsync(CancellationToken.None),
                string.IsNullOrWhiteSpace(publisherCron) ? "0 9,12,15,18,20 * * *" : publisherCron);
        }
    }
}

// Midia publica (Issue #9 / #73): expoe o mesmo diretorio fisico usado pelo LocalMediaStorage
// em /media, necessario para o InstagramPublisher montar video_url publicamente acessivel.
// CreateDirectory garante que o path exista mesmo antes do primeiro download (evita excecao do
// PhysicalFileProvider em ambientes novos).
Directory.CreateDirectory(LocalMediaStorage.MediaDirectory);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(LocalMediaStorage.MediaDirectory),
    RequestPath = "/media",
});

if (hangfireEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthFilter() }
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapPost("/api/jobs/collector/trigger", async (CollectorJob job, CancellationToken ct) =>
{
    await job.ExecuteAsync(ct);
    return Results.Ok();
});

app.MapPost("/api/jobs/collector/amazon/trigger", async (AmazonCollector collector, CancellationToken ct) =>
{
    var products = await collector.CollectAsync(ct);
    return Results.Ok(new { count = products.Count() });
});

app.MapPost("/api/jobs/collector/mercadolivre/trigger", async (MercadoLivreCollector collector, CancellationToken ct) =>
{
    var products = await collector.CollectAsync(ct);
    return Results.Ok(new { count = products.Count() });
});

app.MapPost("/api/jobs/collector/shopee/trigger", async (ShopeeCollector collector, CancellationToken ct) =>
{
    var products = await collector.CollectAsync(ct);
    return Results.Ok(new { count = products.Count() });
});

app.MapPost("/api/jobs/processor/trigger", async (ProcessorJob job, CancellationToken ct) =>
{
    await job.ExecuteAsync(ct);
    return Results.Ok();
});

app.MapPost("/api/jobs/publisher/trigger", async (PublisherJob job, CancellationToken ct) =>
{
    await job.ExecuteAsync(ct);
    return Results.Ok();
});

app.Run();

public partial class Program { }
