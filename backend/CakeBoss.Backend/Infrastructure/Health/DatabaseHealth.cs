using CakeBoss.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Infrastructure.Health;

/// <summary>
/// Confere se o banco responde. Serve ao endpoint /health, que um ping externo chama a cada ~10 minutos:
/// mantém o app acordado e gera atividade no Supabase, que pausa o projeto após ~7 dias sem uso.
/// </summary>
public class DatabaseHealth(AppDbContext db)
{
    /// <summary>Lança exceção se o banco não responder.</summary>
    public async Task CheckAsync(CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
    }
}
