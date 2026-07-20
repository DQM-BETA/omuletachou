using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AfiliadoBot.Tests.Auth;

/// <summary>
/// Cobre CA-A1/A2/A3 (login) e CA-A6/A7/A8/A9 (middleware [Authorize] via o smoke-test
/// GET /api/auth/me) — Issue #11 / Sub-A.
/// </summary>
public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SeededEmail = "operador@omuletachou.com.br";
    private const string SeededPassword = "SenhaForte#2026";

    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedUserAsync(HttpClient client)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        if (!await db.Users.AnyAsync(u => u.Email == SeededEmail))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(SeededPassword, workFactor: 12);
            db.Users.Add(new User(SeededEmail, hash));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_Retorna200ComTokenValidoExpirando24h()
    {
        var client = _factory.CreateClient();
        await SeedUserAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedExpiry = DateTime.UtcNow.AddHours(24);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task Login_ComSenhaIncorreta_Retorna401()
    {
        var client = _factory.CreateClient();
        await SeedUserAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = "senha-errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ComEmailInexistente_Retorna401ComMensagemGenerica()
    {
        var client = _factory.CreateClient();
        await SeedUserAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nao-existe@omuletachou.com.br", password = "qualquer" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("Credenciais invalidas.");
    }

    [Fact]
    public async Task Me_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_ComTokenValido_RetornaEmailDoUsuarioAutenticado()
    {
        var client = _factory.CreateClient();
        await SeedUserAsync(client);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(SeededEmail);
    }

    [Fact]
    public async Task Me_ComTokenExpirado_Retorna401()
    {
        var client = _factory.CreateClient();

        var expiredToken = BuildToken(expires: DateTime.UtcNow.AddHours(-1), signingKey: CustomWebApplicationFactory.TestSigningKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_ComTokenAssinaturaInvalida_Retorna401()
    {
        var client = _factory.CreateClient();

        var invalidToken = BuildToken(
            expires: DateTime.UtcNow.AddHours(1),
            signingKey: "chave-completamente-diferente-nao-confiavel-32-bytes-minimo-000");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string BuildToken(DateTime expires, string signingKey)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "1"),
            new Claim(JwtRegisteredClaimNames.Email, SeededEmail),
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "omuletachou-api",
            audience: "omuletachou-dashboard",
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
