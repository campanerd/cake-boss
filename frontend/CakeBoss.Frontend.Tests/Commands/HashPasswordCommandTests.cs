using CakeBoss.Backend.Features.Admin;
using CakeBoss.Frontend.Commands;

namespace CakeBoss.Frontend.Tests.Commands;

public class HashPasswordCommandTests
{
    private const string Password = "uma frase bem longa 2026";

    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    private static Func<string, string?> Typing(params string?[] answers)
    {
        var queue = new Queue<string?>(answers);
        return _ => queue.Dequeue();
    }

    private int Run(params string?[] answers) => HashPasswordCommand.Run(Typing(answers), output, error);

    [Fact]
    public void WithMatchingPasswords_PrintsAHashThatChecksOut()
    {
        var exitCode = Run(Password, Password);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());

        // O hash é a última linha impressa.
        var hash = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
        Assert.True(AdminPasswordHasher.Verify(hash, Password));
    }

    [Fact]
    public void TheOutput_NeverContainsThePassword()
    {
        Run(Password, Password);

        Assert.DoesNotContain(Password, output.ToString());
    }

    [Fact]
    public void WhenThePasswordsDiffer_ExplainsAndPrintsNoHash()
    {
        var exitCode = Run(Password, Password + "x");

        Assert.Equal(1, exitCode);
        Assert.Contains("não conferem", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("")]
    public void WithAShortPassword_ExplainsTheMinimumAndPrintsNoHash(string password)
    {
        var exitCode = Run(password, password);

        Assert.Equal(1, exitCode);
        Assert.Contains("pelo menos 12", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void WithoutAnyInput_FailsInsteadOfBreaking()
    {
        var exitCode = Run(null, null);

        Assert.Equal(1, exitCode);
        Assert.Empty(output.ToString());
    }
}
