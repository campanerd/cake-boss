using CakeBoss.Backend;
using CakeBoss.Backend.Features.Admin;
using CakeBoss.Backend.Infrastructure.Health;
using CakeBoss.Frontend.Commands;
using CakeBoss.Frontend.Site;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

// Gera o hash da senha da dona, sem subir o site: dotnet run --project frontend/CakeBoss.Frontend -- hash-password
if (args is ["hash-password"])
{
    Environment.ExitCode = HashPasswordCommand.Run(HashPasswordCommand.ReadHidden, Console.Out, Console.Error);
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // Tudo em /painel exige login; só a tela de entrar fica aberta.
    options.Conventions.AuthorizeFolder("/Panel");
    options.Conventions.AllowAnonymousToPage("/Panel/Login");
});
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));

// E-mail e hash da senha da dona: user-secrets em desenvolvimento, variáveis Admin__Email e Admin__PasswordHash em produção.
// Sem eles, ninguém entra no painel.
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.Section));

// Sessão da dona: cookie sem "lembrar de mim", que expira após 12 horas sem uso. Lax (e não Strict) para que um link
// para o painel vindo de um e-mail ou do WhatsApp continue funcionando; os formulários têm antiforgery de qualquer jeito.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "cakeboss.panel";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest // em localhost o http puro precisa funcionar
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.LoginPath = "/painel/entrar";
        options.AccessDeniedPath = "/painel/entrar";
    });

// O Render recebe o HTTPS e repassa ao app por http, com o IP real do cliente em X-Forwarded-For. Sem isto o app enxergaria
// todo mundo com o IP do proxy (o atraso do login por IP não funcionaria) e acharia que a conexão é http.
// O IP do proxy muda, então não dá para listá-lo; fica valendo o padrão do ASP.NET de ler só o último valor do cabeçalho,
// o que o proxy mais próximo do app acrescentou, e não o que o cliente mandou.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// A connection string do Supabase NUNCA vai para o repositório:
//   desenvolvimento: dotnet user-secrets set "ConnectionStrings:Default" "<connection string>" --project frontend/CakeBoss.Frontend
//   produção:        variável de ambiente ConnectionStrings__Default
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is not configured. See the comment in Program.cs.");

builder.Services.AddBackend(connectionString);

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Qual commit está no ar (o Render informa em RENDER_GIT_COMMIT). O workflow de deploy espera por ele para confirmar que a
// versão nova subiu: o /health não serve, porque o Render mantém a versão antiga respondendo até a nova ficar pronta.
app.MapGet("/version", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { commit = BuildInfo.NormalizeCommit(Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")) });
});

// Chamado por um ping externo (cron-job.org ou UptimeRobot) a cada ~10 min: mantém o app acordado
// e gera atividade no banco, evitando a pausa do Supabase por inatividade.
app.MapGet("/health", async (DatabaseHealth health, ILogger<DatabaseHealth> logger, CancellationToken ct) =>
{
    try
    {
        await health.CheckAsync(ct);
        return Results.Ok("ok");
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.LogError(exception, "Database health check failed.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
