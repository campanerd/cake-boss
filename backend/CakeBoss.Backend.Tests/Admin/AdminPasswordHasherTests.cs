using CakeBoss.Backend.Features.Admin;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Tests.Admin;

public class AdminPasswordHasherTests
{
    private const string Password = "uma frase bem longa 2026";

    [Fact]
    public void Verify_WithTheRightPassword_ReturnsTrue()
    {
        var hash = AdminPasswordHasher.Hash(Password);

        Assert.True(AdminPasswordHasher.Verify(hash, Password));
    }

    [Theory]
    [InlineData("uma frase bem longa 2027")]
    [InlineData("UMA FRASE BEM LONGA 2026")]
    [InlineData("uma frase bem longa 2026 ")]
    public void Verify_WithAnotherPassword_ReturnsFalse(string attempt)
    {
        var hash = AdminPasswordHasher.Hash(Password);

        Assert.False(AdminPasswordHasher.Verify(hash, attempt));
    }

    [Fact]
    public void Hash_OfTheSamePasswordTwice_DiffersButBothVerify()
    {
        var first = AdminPasswordHasher.Hash(Password);
        var second = AdminPasswordHasher.Hash(Password);

        Assert.NotEqual(first, second); // sal aleatório
        Assert.True(AdminPasswordHasher.Verify(first, Password));
        Assert.True(AdminPasswordHasher.Verify(second, Password));
    }

    [Fact]
    public void Hash_IsPlainBase64_SoItIsSafeInAnEnvironmentVariable()
    {
        var hash = AdminPasswordHasher.Hash(Password);

        Assert.DoesNotContain(Password, hash);
        Assert.NotEmpty(Convert.FromBase64String(hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curta")]
    [InlineData("onze chars!")] // 11 caracteres: um a menos que o mínimo
    public void Hash_WithAShortPassword_Throws(string? password)
    {
        var exception = Assert.Throws<DomainException>(() => AdminPasswordHasher.Hash(password));
        Assert.Contains("pelo menos 12", exception.Message);
    }

    [Fact]
    public void Hash_AcceptsExactlyTheMinimumLength()
    {
        var password = new string('a', AdminPasswordHasher.MinPasswordLength);

        Assert.True(AdminPasswordHasher.Verify(AdminPasswordHasher.Hash(password), password));
    }

    [Fact]
    public void Hash_WithATooLongPassword_Throws()
    {
        var password = new string('a', AdminPasswordHasher.MaxPasswordLength + 1);

        var exception = Assert.Throws<DomainException>(() => AdminPasswordHasher.Hash(password));
        Assert.Contains("no máximo", exception.Message);
    }

    [Fact]
    public void Verify_WithATooLongPassword_ReturnsFalseWithoutHashingIt()
    {
        var hash = AdminPasswordHasher.Hash(Password);

        Assert.False(AdminPasswordHasher.Verify(hash, new string('a', AdminPasswordHasher.MaxPasswordLength + 1)));
    }

    [Theory]
    [InlineData(null, "alguma senha")]
    [InlineData("", "alguma senha")]
    [InlineData("   ", "alguma senha")]
    public void Verify_WithoutAHash_ReturnsFalse(string? hash, string password)
    {
        Assert.False(AdminPasswordHasher.Verify(hash, password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_WithoutAPassword_ReturnsFalse(string? password)
    {
        var hash = AdminPasswordHasher.Hash(Password);

        Assert.False(AdminPasswordHasher.Verify(hash, password));
    }

    // Variável de ambiente colada errada não pode derrubar o login: só "não confere".
    [Theory]
    [InlineData("isto não é um hash!!")]
    [InlineData("AAAA")]
    [InlineData("QQ==")]
    [InlineData("%%%%")]
    public void Verify_WithAMalformedHash_ReturnsFalseAndDoesNotThrow(string hash)
    {
        Assert.False(AdminPasswordHasher.Verify(hash, Password));
    }
}
