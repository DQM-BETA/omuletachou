using AfiliadoBot.Domain.Entities;

namespace AfiliadoBot.Api.Auth;

public interface IJwtTokenService
{
    /// <summary>
    /// Emite um JWT HS256 assinado para o usuario, com expiracao configurada em JwtOptions.
    /// </summary>
    string GenerateToken(User user);
}
