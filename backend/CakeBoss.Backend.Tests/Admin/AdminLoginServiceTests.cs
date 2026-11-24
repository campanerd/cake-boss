using CakeBoss.Backend.Features.Admin;
using CakeBoss.Backend.Tests.Support;
using Microsoft.Extensions.Options;

namespace CakeBoss.Backend.Tests.Admin;

public class AdminLoginServiceTests
{
    private const string Email = "dona@confeitaria.com";
    private const string Password = "uma frase bem longa 2026";
    private const string Client = "1.2.3.4";

    // O hash custa algumas dezenas de milissegundos: gera uma vez só para a classe inteira.
    private static readonly string Hash = AdminPasswordHasher.Hash(Password);

    private readonly FixedTimeProvider clock = new(new DateTimeOffset(2026, 11, 24, 15, 0, 0, TimeSpan.Zero));
    private readonly LoginThrottle throttle;

    public AdminLoginServiceTests() => throttle = new LoginThrottle(clock);

    private AdminLoginService Service(AdminOptions? options = null) =>
        new(Options.Create(options ?? new AdminOptions { Email = Email, PasswordHash = Hash }), throttle);

    // ---------- Conferência de e-mail e senha ----------

    [Fact]
    public void WithTheRightEmailAndPassword_SignsIn()
    {
        var result = Service().SignIn(Client, Email, Password);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Email, result.Email);
    }

    [Theory]
    [InlineData("DONA@Confeitaria.com")]
    [InlineData("  dona@confeitaria.com  ")]
    public void TheEmail_IgnoresCaseAndSurroundingSpaces(string email)
    {
        Assert.True(Service().SignIn(Client, email, Password).Succeeded);
    }

    [Fact]
    public void ThePassword_IsCaseSensitive_AndNotTrimmed()
    {
        var service = Service();

        Assert.False(service.SignIn(Client, Email, Password.ToUpperInvariant()).Succeeded);
        Assert.False(service.SignIn("outro", Email, Password + " ").Succeeded);
    }

    [Fact]
    public void WithAWrongPassword_FailsWithTheGenericMessage()
    {
        var result = Service().SignIn(Client, Email, "senha errada mesmo");

        Assert.False(result.Succeeded);
        Assert.Equal(AdminLoginService.InvalidCredentialsMessage, result.Error);
        Assert.Null(result.Email);
    }

    [Fact]
    public void WithAWrongEmail_FailsWithTheSameMessage_SoItGivesNoClue()
    {
        var wrongPassword = Service().SignIn(Client, Email, "senha errada mesmo");
        var wrongEmail = Service().SignIn("outro", "outra@pessoa.com", Password);

        Assert.False(wrongEmail.Succeeded);
        Assert.Equal(wrongPassword.Error, wrongEmail.Error);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(Email, null)]
    [InlineData(null, Password)]
    public void WithMissingFields_FailsWithoutThrowing(string? email, string? password)
    {
        var result = Service().SignIn(Client, email, password);

        Assert.False(result.Succeeded);
        Assert.Equal(AdminLoginService.InvalidCredentialsMessage, result.Error);
    }

    // ---------- Painel sem configuração ----------

    [Fact]
    public void WithoutConfiguration_NobodyGetsIn()
    {
        var result = Service(new AdminOptions()).SignIn(Client, Email, Password);

        Assert.False(result.Succeeded);
        Assert.Equal(AdminLoginService.InvalidCredentialsMessage, result.Error);
    }

    [Fact]
    public void WithOnlyTheEmailConfigured_NobodyGetsIn()
    {
        Assert.False(Service(new AdminOptions { Email = Email }).SignIn(Client, Email, Password).Succeeded);
    }

    [Fact]
    public void WithOnlyTheHashConfigured_NobodyGetsIn()
    {
        Assert.False(Service(new AdminOptions { PasswordHash = Hash }).SignIn(Client, Email, Password).Succeeded);
    }

    [Fact]
    public void WithAMalformedHashConfigured_FailsInsteadOfBreaking()
    {
        var options = new AdminOptions { Email = Email, PasswordHash = "colei errado na variável!!" };

        Assert.False(Service(options).SignIn(Client, Email, Password).Succeeded);
    }

    // ---------- Atraso crescente ----------

    [Fact]
    public void AfterThreeFailures_EvenTheRightPasswordIsRefusedForAWhile()
    {
        var service = Service();
        for (var i = 0; i < LoginThrottle.FreeFailures; i++)
            service.SignIn(Client, Email, "senha errada mesmo");

        var result = service.SignIn(Client, Email, Password);

        Assert.False(result.Succeeded);
        Assert.Equal("Muitas tentativas. Tente de novo em 1 segundo.", result.Error);
    }

    [Fact]
    public void WhenTheWaitEnds_TheRightPasswordWorksAgain()
    {
        var service = Service();
        for (var i = 0; i < LoginThrottle.FreeFailures; i++)
            service.SignIn(Client, Email, "senha errada mesmo");

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(service.SignIn(Client, Email, Password).Succeeded);
    }

    [Fact]
    public void TheMessageSaysSeconds_ThenMinutes()
    {
        var service = Service();
        for (var i = 0; i < 6; i++) throttle.RecordFailure(Client); // espera de 8 s

        Assert.Equal("Muitas tentativas. Tente de novo em 8 segundos.", service.SignIn(Client, Email, Password).Error);

        for (var i = 0; i < 40; i++) throttle.RecordFailure(Client); // no teto: 15 min

        Assert.Equal("Muitas tentativas. Tente de novo em 15 minutos.", service.SignIn(Client, Email, Password).Error);
    }

    [Fact]
    public void AttemptsDuringTheWait_DoNotMakeItLonger()
    {
        var service = Service();
        for (var i = 0; i < LoginThrottle.FreeFailures; i++)
            service.SignIn(Client, Email, "senha errada mesmo");

        var first = service.SignIn(Client, Email, "senha errada mesmo");
        var second = service.SignIn(Client, Email, "senha errada mesmo");

        Assert.Equal(first.Error, second.Error);
        Assert.Contains("1 segundo", second.Error);
    }

    [Fact]
    public void ASuccessfulLogin_ResetsTheCount()
    {
        var service = Service();
        service.SignIn(Client, Email, "senha errada mesmo");
        service.SignIn(Client, Email, "senha errada mesmo");

        Assert.True(service.SignIn(Client, Email, Password).Succeeded);

        // Recomeçou: mais dois erros seguem sem espera.
        service.SignIn(Client, Email, "senha errada mesmo");
        service.SignIn(Client, Email, "senha errada mesmo");
        Assert.True(service.SignIn(Client, Email, Password).Succeeded);
    }

    [Fact]
    public void WhoFails_IsWhoWaits_AnotherClientIsNotLockedOut()
    {
        var service = Service();
        for (var i = 0; i < 6; i++)
            service.SignIn("atacante", Email, "senha errada mesmo");

        Assert.False(service.SignIn("atacante", Email, Password).Succeeded);
        Assert.True(service.SignIn("dona", Email, Password).Succeeded);
    }
}
