using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;

namespace AfiliadoBot.Api.Auth;

/// <summary>
/// Seed do usuario unico do sistema (Issue #11 / Sub-A, CA-A4/CA-A5). So cria o usuario se a
/// tabela estiver vazia (idempotente) e se email/senha de seed estiverem configurados via
/// variavel de ambiente (Seed__UserEmail / Seed__UserPassword) — nunca hardcoded, nunca em
/// texto plano (senha sempre persistida como hash bcrypt).
/// </summary>
public static class UserSeeder
{
    public static void SeedIfEmpty(AfiliadoBotDbContext db, string? email, string? password)
    {
        if (db.Users.Any())
            return;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        db.Users.Add(new User(email, passwordHash));
        db.SaveChanges();
    }
}
