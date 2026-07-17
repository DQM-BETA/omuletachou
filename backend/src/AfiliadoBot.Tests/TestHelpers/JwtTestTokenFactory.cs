using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AfiliadoBot.Tests.TestHelpers;

/// <summary>
/// Gera JWTs validos assinados com <see cref="CustomWebApplicationFactory.TestSigningKey"/>,
/// reutilizavel por qualquer suite de teste que precise chamar um endpoint protegido por
/// [Authorize] (Issue #11 / Sub-C em diante — evita duplicar a logica de emissao de token
/// presente em AuthControllerTests.BuildToken em cada nova suite).
/// </summary>
public static class JwtTestTokenFactory
{
    public static string CreateValidToken(int userId = 1, string email = "operador@omuletachou.com.br")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "omuletachou-api",
            audience: "omuletachou-dashboard",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
