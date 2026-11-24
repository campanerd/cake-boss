using System.Security.Claims;
using CakeBoss.Backend.Features.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CakeBoss.Frontend.Pages.Panel;

/// <summary>Tela de login da dona. Fina de propósito: quem decide se pode entrar é o <see cref="AdminLoginService"/>.</summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LoginModel(AdminLoginService login, ILogger<LoginModel> logger) : PageModel
{
    public const string PanelPath = "/painel";
    private const string LoginPath = "/painel/entrar";

    [BindProperty]
    public LoginForm Form { get; set; } = new();

    /// <summary>Para onde voltar depois de entrar (o cookie expirou numa página do painel, por exemplo).</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl(ReturnUrl)) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        // Atrás do proxy do Render, o ForwardedHeaders do Program.cs já trocou o endereço do proxy pelo do cliente.
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = login.SignIn(clientIp, Form.Email, Form.Password);

        if (!result.Succeeded)
        {
            // Só o IP, nunca o e-mail nem a senha digitados: quem erra pode ter colado a senha no campo errado.
            logger.LogWarning("Falha de login no painel vinda de {ClientIp}.", clientIp);

            Form.Password = null;
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, result.Email!)], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return LocalRedirect(SafeReturnUrl(ReturnUrl));
    }

    /// <summary>
    /// Só aceita voltar para uma página do próprio painel. Qualquer outra coisa (outro site, "//site", caminho de outra área)
    /// cai no painel: um link de login falso não pode mandar a dona para fora depois de entrar.
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl.Contains('\\')) return PanelPath;
        if (!returnUrl.StartsWith(PanelPath, StringComparison.Ordinal)) return PanelPath;
        if (returnUrl.StartsWith(LoginPath, StringComparison.Ordinal)) return PanelPath;

        // "/painel" ou "/painel/...", "/painel?...": não "/painelqualquercoisa".
        var next = returnUrl.Length > PanelPath.Length ? returnUrl[PanelPath.Length] : '/';
        return next is '/' or '?' ? returnUrl : PanelPath;
    }

    public sealed class LoginForm
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }
}
