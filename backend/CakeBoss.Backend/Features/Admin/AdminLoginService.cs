using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace CakeBoss.Backend.Features.Admin;

/// <summary>Resultado de uma tentativa de login. <see cref="Error"/> já vem em português, pronto para exibir.</summary>
public sealed record AdminLoginResult(bool Succeeded, string? Error, string? Email)
{
    public static AdminLoginResult Success(string email) => new(true, null, email);

    public static AdminLoginResult Failed(string error) => new(false, error, null);
}

/// <summary>
/// Login da dona. Confere e-mail e senha contra a configuração e aplica o <see cref="LoginThrottle"/>.
/// Quem cria o cookie de sessão é o frontend; aqui fica só a decisão "pode entrar ou não".
/// </summary>
public sealed class AdminLoginService(IOptions<AdminOptions> options, LoginThrottle throttle)
{
    /// <summary>Mensagem única para e-mail errado, senha errada ou painel sem configuração: não dá pista do que falhou.</summary>
    public const string InvalidCredentialsMessage = "E-mail ou senha incorretos.";

    /// <param name="clientKey">Quem está tentando (o frontend passa o IP), para o atraso crescente.</param>
    public AdminLoginResult SignIn(string? clientKey, string? email, string? password)
    {
        var wait = throttle.RetryAfter(clientKey);
        if (wait is not null)
            return AdminLoginResult.Failed($"Muitas tentativas. Tente de novo em {DescribeWait(wait.Value)}.");

        var admin = options.Value;

        // A senha é conferida mesmo quando o e-mail está errado: o tempo de resposta não revela qual dos dois falhou.
        var passwordMatches = AdminPasswordHasher.Verify(admin.PasswordHash, password);
        var emailMatches = admin.IsConfigured && EmailMatches(admin.Email!, email);

        if (passwordMatches && emailMatches)
        {
            throttle.RecordSuccess(clientKey);
            return AdminLoginResult.Success(admin.Email!.Trim());
        }

        throttle.RecordFailure(clientKey);
        return AdminLoginResult.Failed(InvalidCredentialsMessage);
    }

    private static bool EmailMatches(string expected, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided)) return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected.Trim().ToLowerInvariant());
        var providedBytes = Encoding.UTF8.GetBytes(provided.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    // "8 segundos", "1 segundo", "3 minutos": sempre arredonda para cima, para ninguém tentar um instante cedo demais.
    private static string DescribeWait(TimeSpan wait)
    {
        var seconds = (int)Math.Ceiling(wait.TotalSeconds);
        if (seconds < 60) return seconds == 1 ? "1 segundo" : $"{seconds} segundos";

        var minutes = (int)Math.Ceiling(seconds / 60.0);
        return minutes == 1 ? "1 minuto" : $"{minutes} minutos";
    }
}
