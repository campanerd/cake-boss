using CakeBoss.Backend;
using CakeBoss.Backend.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// A connection string do Supabase NUNCA vai para o repositório:
//   desenvolvimento: dotnet user-secrets set "ConnectionStrings:Default" "<connection string>" --project frontend/CakeBoss.Frontend
//   produção:        variável de ambiente ConnectionStrings__Default
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is not configured. See the comment in Program.cs.");

builder.Services.AddBackend(connectionString);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

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
