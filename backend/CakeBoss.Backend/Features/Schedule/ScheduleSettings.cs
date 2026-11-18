using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Schedule;

/// <summary>Configurações da agenda (uma única linha, Id = 1). A dona ajusta pelo painel.</summary>
public class ScheduleSettings
{
    public const int SingletonId = 1;

    // Valores iniciais combinados com a dona (a antecedência é uma média e pode ser ajustada).
    public const int DefaultSlotMinutes = 60;
    public const int DefaultMinLeadDays = 3;
    public const decimal DefaultMaxKgPerDay = 20m;

    private ScheduleSettings() { }

    public int Id { get; private set; }

    /// <summary>Tamanho da janela reservada para cada retirada/entrega.</summary>
    public int SlotMinutes { get; private set; }

    /// <summary>Com quantos dias de antecedência, no mínimo, o cliente precisa pedir (0 = pode pedir para hoje).</summary>
    public int MinLeadDays { get; private set; }

    /// <summary>Quantos quilos de bolo a dona consegue produzir por dia.</summary>
    public decimal MaxKgPerDay { get; private set; }

    public static ScheduleSettings CreateDefault() => new()
    {
        Id = SingletonId,
        SlotMinutes = DefaultSlotMinutes,
        MinLeadDays = DefaultMinLeadDays,
        MaxKgPerDay = DefaultMaxKgPerDay
    };

    public void Update(int slotMinutes, int minLeadDays, decimal maxKgPerDay)
    {
        if (slotMinutes is < 15 or > 480)
            throw new DomainException("O intervalo deve ser entre 15 e 480 minutos.");
        if (minLeadDays is < 0 or > 365)
            throw new DomainException("A antecedência mínima deve ser entre 0 e 365 dias.");
        if (maxKgPerDay is <= 0 or > 1000)
            throw new DomainException("O limite diário deve ser maior que zero e no máximo 1000 kg.");

        SlotMinutes = slotMinutes;
        MinLeadDays = minLeadDays;
        MaxKgPerDay = maxKgPerDay;
    }
}
