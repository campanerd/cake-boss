using CakeBoss.Frontend.Site;

namespace CakeBoss.Frontend.Tests.Site;

public class BuildInfoTests
{
    [Theory]
    [InlineData("4f1c2a9d8b7e6f5a4c3b2a1908f7e6d5c4b3a291", "4f1c2a9d8b7e6f5a4c3b2a1908f7e6d5c4b3a291")] // 40 caracteres
    [InlineData("4f1c2a9", "4f1c2a9")] // curto, 7 caracteres
    [InlineData("  4F1C2A9D8B  ", "4f1c2a9d8b")] // espaços e maiúsculas
    public void NormalizeCommit_AcceptsAGitCommit(string value, string expected)
    {
        Assert.Equal(expected, BuildInfo.NormalizeCommit(value));
    }

    // O valor sai numa rota pública: nada que não pareça um commit pode ser repetido na resposta.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc123")] // curto demais
    [InlineData("4f1c2a9d8b7e6f5a4c3b2a1908f7e6d5c4b3a2911")] // 41 caracteres
    [InlineData("zzzzzzzzzz")] // não é hexadecimal
    [InlineData("4f1c2a9\" onload=\"x")] // tentativa de injeção
    [InlineData("main")]
    public void NormalizeCommit_TurnsAnythingElseIntoUnknown(string? value)
    {
        Assert.Equal(BuildInfo.Unknown, BuildInfo.NormalizeCommit(value));
    }
}
