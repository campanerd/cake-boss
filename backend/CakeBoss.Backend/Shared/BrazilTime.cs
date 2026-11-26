namespace CakeBoss.Backend.Shared;

/// <summary>
/// Horário de Brasília (<c>America/Sao_Paulo</c>), no qual a confeitaria trabalha: datas e horas dos pedidos e a agenda.
/// Os registros de auditoria e os prazos ficam em UTC no banco; aqui está a conversão para mostrá-los à dona e ao cliente.
/// </summary>
public static class BrazilTime
{
    private static readonly Lazy<TimeZoneInfo> Zone = new(Resolve);

    /// <summary>Converte um instante em UTC para o horário de Brasília.</summary>
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone.Value);

    /// <summary>Data e hora de agora em Brasília, pelo relógio informado.</summary>
    public static DateTime Now(TimeProvider clock) => FromUtc(clock.GetUtcNow().UtcDateTime);

    private static TimeZoneInfo Resolve()
    {
        // O Linux (Render) usa o nome da IANA; o Windows usa o próprio.
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Local;
    }
}
