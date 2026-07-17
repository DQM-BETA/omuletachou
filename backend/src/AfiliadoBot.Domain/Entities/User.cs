namespace AfiliadoBot.Domain.Entities;

/// <summary>
/// Usuario unico do sistema (operador do dashboard administrativo), Issue #11 / Sub-A.
/// Sem multi-tenant, sem roles/permissoes (CA-A conforme proposal.md).
/// </summary>
public class User
{
    public int Id { get; private set; }
    public string Email { get; private set; } = string.Empty;

    // Hash bcrypt (formato $2a$...); nunca a senha em texto plano (CA-A5).
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Construtor para EF Core
    private User() { }

    public User(string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email nao pode ser nulo ou vazio.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash nao pode ser nulo ou vazio.", nameof(passwordHash));

        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }
}
