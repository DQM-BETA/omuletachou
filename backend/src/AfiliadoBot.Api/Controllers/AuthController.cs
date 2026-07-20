using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AfiliadoBot.Api.Auth;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Autenticacao (Issue #11 / Sub-A). POST /api/auth/login (publico) emite o JWT;
/// GET /api/auth/me (protegido) e o endpoint de smoke-test que prova o middleware
/// [Authorize] funcionando ponta a ponta (desbloqueia Sub-B a Sub-E).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;
    private readonly IJwtTokenService _tokenService;

    public AuthController(AfiliadoBotDbContext db, IJwtTokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // Mensagem generica em qualquer caso de falha (CA-A2/CA-A3) — nunca indica se o erro
        // foi no email ou na senha (evita enumeracao de usuarios). Nunca logar a senha em
        // texto plano nem o JWT completo.
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { message = "Credenciais invalidas." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Credenciais invalidas." });

        var token = _tokenService.GenerateToken(user);
        return Ok(new LoginResponse(token));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        return Ok(new { email });
    }
}
