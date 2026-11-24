using CakeBoss.Frontend.Pages.Panel;

namespace CakeBoss.Frontend.Tests.Panel;

public class LoginModelTests
{
    // Depois de entrar, só se volta para uma página do próprio painel.
    [Theory]
    [InlineData("/painel", "/painel")]
    [InlineData("/painel/pedidos", "/painel/pedidos")]
    [InlineData("/painel/pedidos/ABC123?aba=2", "/painel/pedidos/ABC123?aba=2")]
    [InlineData("/painel?x=1", "/painel?x=1")]
    public void SafeReturnUrl_KeepsPagesOfThePanel(string returnUrl, string expected)
    {
        Assert.Equal(expected, LoginModel.SafeReturnUrl(returnUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://site-falso.com/painel")]
    [InlineData("//site-falso.com")]
    [InlineData("/\\site-falso.com")]
    [InlineData("/painel\\..\\..")]
    [InlineData("/pedido")]
    [InlineData("/")]
    [InlineData("painel")]
    [InlineData("/painelqualquercoisa")]
    [InlineData("/Painel")]
    public void SafeReturnUrl_SendsAnythingElseToThePanel(string? returnUrl)
    {
        Assert.Equal("/painel", LoginModel.SafeReturnUrl(returnUrl));
    }

    // Voltar para a própria tela de entrar faria um laço.
    [Theory]
    [InlineData("/painel/entrar")]
    [InlineData("/painel/entrar?ReturnUrl=%2Fpainel")]
    public void SafeReturnUrl_NeverReturnsToTheLoginPage(string returnUrl)
    {
        Assert.Equal("/painel", LoginModel.SafeReturnUrl(returnUrl));
    }
}
