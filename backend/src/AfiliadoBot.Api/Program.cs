using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Platforms;
using AfiliadoBot.Infrastructure.Services;
using AfiliadoBot.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHttpClient<IPlatformCollector, AmazonCollector>();
builder.Services.AddHttpClient<MercadoLivreCollector>();
builder.Services.AddHttpClient<ShopeeCollector>();

// Media storage (ProcessorJob, Issue #6)
builder.Services.AddHttpClient<IMediaStorage, LocalMediaStorage>();

// ProcessorJob (Issue #6)
builder.Services.AddHttpClient<ProcessorJob>();

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapPost("/api/jobs/collector/trigger", async (IPlatformCollector collector, CancellationToken ct) =>
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

app.Run();

public partial class Program { }
