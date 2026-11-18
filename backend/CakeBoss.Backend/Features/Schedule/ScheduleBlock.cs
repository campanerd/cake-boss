using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Schedule;

/// <summary>
/// Período em que a dona não aceita pedidos (folga, férias, feriado, compromisso).
/// Sem horários = dias inteiros; com horários, bloqueia só essa faixa em cada dia do período.
/// </summary>
public class ScheduleBlock
{
    private ScheduleBlock() { }

    public Guid Id { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public string? Reason { get; private set; }

    public static ScheduleBlock Create(
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null,
        string? reason = null)
    {
        if (endDate < startDate)
            throw new DomainException("A data final não pode ser antes da inicial.");
        if (startTime.HasValue != endTime.HasValue)
            throw new DomainException("Informe os dois horários ou nenhum.");
        if (startTime >= endTime)
            throw new DomainException("O horário inicial deve ser antes do final.");

        return new ScheduleBlock
        {
            Id = Guid.CreateVersion7(),
            StartDate = startDate,
            EndDate = endDate,
            StartTime = startTime,
            EndTime = endTime,
            Reason = TextCleaner.Clean(reason, 200)
        };
    }

    /// <summary>Indica se este bloqueio impede um pedido na data e hora informadas.</summary>
    public bool Covers(DateOnly date, TimeOnly time)
    {
        if (date < StartDate || date > EndDate) return false;
        if (StartTime is null || EndTime is null) return true;
        return time >= StartTime && time < EndTime;
    }
}
