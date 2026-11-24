using System.Text;
using CakeBoss.Backend.Features.Admin;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Frontend.Commands;

/// <summary>
/// Gera o hash da senha da dona, para colar em Admin__PasswordHash (Render) ou nos user-secrets:
///   dotnet run --project frontend/CakeBoss.Frontend -- hash-password
/// A senha é digitada no terminal de quem roda o comando (sem aparecer na tela) e nunca é guardada nem enviada a lugar nenhum.
/// </summary>
public static class HashPasswordCommand
{
    /// <param name="readPassword">Mostra o texto recebido e devolve o que a pessoa digitou (separado para poder ser testado).</param>
    /// <returns>Código de saída do processo: 0 quando gerou o hash.</returns>
    public static int Run(Func<string, string?> readPassword, TextWriter output, TextWriter error)
    {
        var password = readPassword($"Nova senha ({AdminPasswordHasher.MinPasswordLength} caracteres ou mais): ");
        var confirmation = readPassword("Repita a senha: ");

        if (password != confirmation)
        {
            error.WriteLine("As senhas não conferem. Nada foi gerado.");
            return 1;
        }

        string hash;
        try
        {
            hash = AdminPasswordHasher.Hash(password);
        }
        catch (DomainException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }

        output.WriteLine();
        output.WriteLine("Cole este valor em Admin__PasswordHash (variável de ambiente no Render ou user-secrets):");
        output.WriteLine(hash);
        return 0;
    }

    /// <summary>Lê a senha do terminal sem mostrar o que é digitado. Se a entrada vier de um arquivo ou pipe, lê a linha normalmente.</summary>
    public static string? ReadHidden(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected) return Console.ReadLine();

        var typed = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter) break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0) typed.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar)) typed.Append(key.KeyChar);
        }

        Console.WriteLine();
        return typed.ToString();
    }
}
