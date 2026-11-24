namespace CakeBoss.Backend.Features.Admin;

/// <summary>
/// Credenciais da dona (um único usuário), lidas da seção "Admin" da configuração.
/// Nada daqui vai para o repositório nem para o banco:
///   desenvolvimento: dotnet user-secrets set "Admin:Email" "..." e "Admin:PasswordHash" "..."
///   produção:        variáveis de ambiente Admin__Email e Admin__PasswordHash
/// O hash é gerado por <see cref="AdminPasswordHasher.Hash"/>; a senha em si nunca é guardada.
/// </summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    /// <summary>Sem e-mail e hash configurados, ninguém entra no painel.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(PasswordHash);
}
