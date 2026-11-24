using CakeBoss.Backend.Shared;
using Microsoft.AspNetCore.Identity;

namespace CakeBoss.Backend.Features.Admin;

/// <summary>
/// Gera e confere o hash da senha da dona. Usa o hasher da Microsoft (PBKDF2 com sal aleatório e o formato guarda a versão
/// e o número de iterações), em vez de um formato inventado aqui.
/// </summary>
public static class AdminPasswordHasher
{
    public const int MinPasswordLength = 12;

    /// <summary>Teto para a senha não virar um jeito de gastar CPU com entradas enormes.</summary>
    public const int MaxPasswordLength = 200;

    private static readonly PasswordHasher<Account> Hasher = new();
    private static readonly Account Owner = new();

    /// <exception cref="DomainException">Quando a senha é curta demais ou longa demais.</exception>
    public static string Hash(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new DomainException($"A senha precisa ter pelo menos {MinPasswordLength} caracteres.");

        if (password.Length > MaxPasswordLength)
            throw new DomainException($"A senha pode ter no máximo {MaxPasswordLength} caracteres.");

        return Hasher.HashPassword(Owner, password);
    }

    /// <summary>
    /// Confere a senha contra o hash. Devolve false (nunca lança) para hash vazio, hash malformado ou senha vazia ou longa demais.
    /// </summary>
    public static bool Verify(string? hash, string? password)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrEmpty(password) || password.Length > MaxPasswordLength)
            return false;

        try
        {
            return Hasher.VerifyHashedPassword(Owner, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Hash que não é Base64 (colado errado na variável de ambiente, por exemplo): é só "não confere".
            return false;
        }
    }

    // O hasher pede um tipo de usuário; aqui só existe uma conta, então basta um marcador.
    private sealed class Account;
}
