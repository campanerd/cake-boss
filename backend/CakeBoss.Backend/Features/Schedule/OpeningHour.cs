using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Schedule;

/// <summary>Janela de funcionamento de um dia da semana. Dia sem registro = fechado.</summary>
public class OpeningHour
{
    private OpeningHour() { }

    public int Id { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }

    public static OpeningHour Create(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        var hour = new OpeningHour { DayOfWeek = dayOfWeek };
        hour.Update(opensAt, closesAt);
        return hour;
    }

    public void Update(TimeOnly opensAt, TimeOnly closesAt)
    {
        if (opensAt >= closesAt)
            throw new DomainException("A abertura deve ser antes do fechamento.");

        OpensAt = opensAt;
        ClosesAt = closesAt;
    }
}
