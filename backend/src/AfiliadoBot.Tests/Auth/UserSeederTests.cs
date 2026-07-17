using AfiliadoBot.Api.Auth;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Tests.Auth;

/// <summary>
/// CA-A4/CA-A5 — seed do usuario via variavel de ambiente, idempotente (so roda se a tabela
/// estiver vazia), senha sempre persistida como hash bcrypt.
/// </summary>
public class UserSeederTests
{
    private static AfiliadoBotDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase("UserSeederTests_" + Guid.NewGuid())
            .Options);

    [Fact]
    public void SeedIfEmpty_TabelaVazia_ComEmailESenha_CriaUsuarioComHashBcrypt()
    {
        using var db = CreateDb();

        UserSeeder.SeedIfEmpty(db, "operador@omuletachou.com.br", "SenhaForte#2026");

        var user = db.Users.Single();
        user.Email.Should().Be("operador@omuletachou.com.br");
        user.PasswordHash.Should().NotBe("SenhaForte#2026");
        user.PasswordHash.Should().StartWith("$2");
        BCrypt.Net.BCrypt.Verify("SenhaForte#2026", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public void SeedIfEmpty_TabelaJaComUsuario_NaoCriaSegundoUsuario_Idempotente()
    {
        using var db = CreateDb();
        UserSeeder.SeedIfEmpty(db, "primeiro@omuletachou.com.br", "SenhaForte#2026");

        UserSeeder.SeedIfEmpty(db, "segundo@omuletachou.com.br", "OutraSenha#2026");

        db.Users.Should().HaveCount(1);
        db.Users.Single().Email.Should().Be("primeiro@omuletachou.com.br");
    }

    [Fact]
    public void SeedIfEmpty_SemEmailOuSenhaConfigurados_NaoCriaUsuario()
    {
        using var db = CreateDb();

        UserSeeder.SeedIfEmpty(db, email: null, password: null);

        db.Users.Should().BeEmpty();
    }
}
