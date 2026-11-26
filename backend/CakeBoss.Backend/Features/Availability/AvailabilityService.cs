using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Shared;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Features.Availability;

/// <summary>Carrega do banco o que o <see cref="AvailabilityCalculator"/> precisa e delega o cálculo a ele.</summary>
public class AvailabilityService(AppDbContext db, TimeProvider clock)
{
    private const int MaxDaysPerQuery = 366;

    /// <summary>Data de hoje no horário de Brasília.</summary>
    public DateOnly Today => DateOnly.FromDateTime(LocalNow());

    public async Task<IReadOnlyList<TimeOnly>> GetFreeTimesAsync(
        DateOnly date,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        var input = await LoadAsync(date, date, ct);
        return AvailabilityCalculator.FreeTimes(input, date, requestedKg);
    }

    public async Task<bool> IsAvailableAsync(
        DateOnly date,
        TimeOnly time,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        var freeTimes = await GetFreeTimesAsync(date, requestedKg, ct);
        return freeTimes.Contains(time);
    }

    public async Task<IReadOnlyList<DateOnly>> GetDaysWithFreeTimesAsync(
        DateOnly from,
        DateOnly to,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        var days = await GetFreeDaysAsync(from, to, requestedKg, ct);
        return days.Select(d => d.Date).ToList();
    }

    /// <summary>Dias livres do intervalo com os horários de cada um, para o calendário do formulário.</summary>
    public async Task<IReadOnlyList<AvailableDay>> GetFreeDaysAsync(
        DateOnly from,
        DateOnly to,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        if (to < from) return [];
        if (to.DayNumber - from.DayNumber > MaxDaysPerQuery)
            throw new ArgumentOutOfRangeException(nameof(to), $"Consulte no máximo {MaxDaysPerQuery} dias por vez.");

        var input = await LoadAsync(from, to, ct);
        return AvailabilityCalculator.FreeDays(input, from, to, requestedKg);
    }

    /// <summary>
    /// Motivo pelo qual a dona não pode aprovar o pedido pendente (horário tomado, dia bloqueado, kg estourados...), ou null se pode.
    /// Não exige a antecedência mínima (ver <see cref="AvailabilityCalculator.FindApprovalConflict"/>).
    /// O próprio pedido nunca conta como ocupante.
    /// </summary>
    public virtual async Task<string?> FindApprovalConflictAsync(
        Guid orderId,
        DateOnly date,
        TimeOnly time,
        decimal weightKg,
        bool blocksWholeDay,
        CancellationToken ct = default)
    {
        var input = await LoadAsync(date, date, ct, ignoreOrderId: orderId);
        return AvailabilityCalculator.FindApprovalConflict(input, date, time, weightKg, blocksWholeDay);
    }

    private DateTime LocalNow() => BrazilTime.Now(clock);

    private async Task<AvailabilityInput> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct, Guid? ignoreOrderId = null)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        var openingHours = await db.OpeningHours.AsNoTracking().ToListAsync(ct);
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);

        var blocks = await db.ScheduleBlocks.AsNoTracking()
            .Where(b => b.StartDate <= to && b.EndDate >= from)
            .ToListAsync(ct);

        var blockingStatuses = Order.SlotBlockingStatuses;

        // Pedido aprovado cujo prazo do sinal já venceu não segura mais o horário, mesmo que ainda não tenha sido fechado
        // (ver Order.IsDepositOverdue): o horário volta ao calendário na hora exata, sem depender de nenhuma rotina.
        var occupied = await db.Orders.AsNoTracking()
            .Where(o => o.Date >= from && o.Date <= to && blockingStatuses.Contains(o.Status) && o.Id != ignoreOrderId)
            .Where(o => !(o.Status == OrderStatus.Approved && o.DepositDueAt != null && o.DepositDueAt <= nowUtc))
            .Select(o => new OccupiedSlot(o.Date, o.Time, o.Items.Sum(i => i.WeightKg), o.BlocksWholeDay))
            .ToListAsync(ct);

        return new AvailabilityInput(LocalNow(), settings, openingHours, blocks, occupied);
    }
}
