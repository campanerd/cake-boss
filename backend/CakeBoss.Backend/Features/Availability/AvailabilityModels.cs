using CakeBoss.Backend.Features.Schedule;

namespace CakeBoss.Backend.Features.Availability;

/// <summary>Um pedido que ocupa a agenda: só o que importa para calcular os horários livres.</summary>
public sealed record OccupiedSlot(DateOnly Date, TimeOnly Time, decimal WeightKg, bool BlocksWholeDay);

/// <summary>Tudo o que o cálculo precisa saber, sem depender do banco nem do relógio.</summary>
/// <param name="LocalNow">Data e hora atuais no horário de Brasília.</param>
/// <param name="Occupied">Pedidos aprovados, confirmados ou concluídos.</param>
public sealed record AvailabilityInput(
    DateTime LocalNow,
    ScheduleSettings Settings,
    IReadOnlyCollection<OpeningHour> OpeningHours,
    IReadOnlyCollection<ScheduleBlock> Blocks,
    IReadOnlyCollection<OccupiedSlot> Occupied);
