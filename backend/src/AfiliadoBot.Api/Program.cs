using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Services;
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

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

public partial class Program { }
