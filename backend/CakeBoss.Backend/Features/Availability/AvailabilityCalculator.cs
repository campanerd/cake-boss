namespace CakeBoss.Backend.Features.Availability;

/// <summary>
/// Decide quais horários o cliente pode escolher. É lógica pura (sem banco, sem relógio): tudo vem em <see cref="AvailabilityInput"/>.
/// Pedidos pendentes NÃO ocupam horário nem capacidade; só os que estão em <see cref="AvailabilityInput.Occupied"/>.
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>
    /// Horários livres de um dia para um pedido de <paramref name="requestedKg"/> quilos (use 0 se o peso ainda não é conhecido).
    /// O dia fica sem horários se: for anterior à antecedência mínima, a confeitaria estiver fechada, houver pedido que ocupa o dia
    /// inteiro, a capacidade em kg estiver esgotada (ou o pedido não couber no que sobrou).
    /// </summary>
    public static IReadOnlyList<TimeOnly> FreeTimes(AvailabilityInput input, DateOnly date, decimal requestedKg = 0)
    {
        if (requestedKg < 0)
            throw new ArgumentOutOfRangeException(nameof(requestedKg), "O peso não pode ser negativo.");

        var today = DateOnly.FromDateTime(input.LocalNow);
        if (date < today.AddDays(input.Settings.MinLeadDays)) return [];

        var window = input.OpeningHours.FirstOrDefault(h => h.DayOfWeek == date.DayOfWeek);
        if (window is null) return [];

        var occupiedToday = input.Occupied.Where(o => o.Date == date).ToList();
        if (occupiedToday.Any(o => o.BlocksWholeDay)) return [];

        var remainingKg = input.Settings.MaxKgPerDay - occupiedToday.Sum(o => o.WeightKg);
        if (remainingKg <= 0 || requestedKg > remainingKg) return [];

        var occupiedTimes = occupiedToday.Select(o => o.Time).ToHashSet();
        var step = TimeSpan.FromMinutes(input.Settings.SlotMinutes);
        var closing = window.ClosesAt.ToTimeSpan();
        var currentTime = TimeOnly.FromDateTime(input.LocalNow);
        var free = new List<TimeOnly>();

        // O slot inteiro precisa caber dentro do expediente.
        for (var start = window.OpensAt.ToTimeSpan(); start + step <= closing; start += step)
        {
            var time = TimeOnly.FromTimeSpan(start);

            if (date == today && time <= currentTime) continue;
            if (occupiedTimes.Contains(time)) continue;
            if (input.Blocks.Any(b => b.Covers(date, time))) continue;

            free.Add(time);
        }

        return free;
    }

    /// <summary>
    /// Confere se a dona ainda pode aprovar um pedido pendente naquele dia e horário. Devolve o motivo (em português) se não puder,
    /// ou null se estiver tudo certo. Em <see cref="AvailabilityInput.Occupied"/> não deve constar o próprio pedido.
    /// Diferente de <see cref="FreeTimes"/>, NÃO exige a antecedência mínima: ela vale para o cliente na hora de pedir, e o pedido
    /// pode ter ficado pendente por dias, negociando, até chegar perto da data. Confere só o que realmente conflita.
    /// </summary>
    public static string? FindApprovalConflict(
        AvailabilityInput input,
        DateOnly date,
        TimeOnly time,
        decimal weightKg,
        bool blocksWholeDay)
    {
        var today = DateOnly.FromDateTime(input.LocalNow);
        if (date < today) return "A data do pedido já passou.";
        if (date == today && time <= TimeOnly.FromDateTime(input.LocalNow)) return "O horário do pedido já passou.";

        var window = input.OpeningHours.FirstOrDefault(h => h.DayOfWeek == date.DayOfWeek);
        if (window is null) return "A confeitaria não abre nesse dia da semana.";

        var step = TimeSpan.FromMinutes(input.Settings.SlotMinutes);
        if (time < window.OpensAt || time.ToTimeSpan() + step > window.ClosesAt.ToTimeSpan())
            return "O horário do pedido está fora do expediente.";

        if (input.Blocks.Any(b => b.Covers(date, time)))
            return "Esse dia ou horário está bloqueado na agenda.";

        var occupiedToday = input.Occupied.Where(o => o.Date == date).ToList();

        if (occupiedToday.Any(o => o.BlocksWholeDay))
            return "Outro pedido aprovado já ocupa o dia inteiro.";

        if (occupiedToday.Any(o => o.Time == time))
            return "Já existe um pedido aprovado nesse horário.";

        if (blocksWholeDay && occupiedToday.Count > 0)
            return "Já há pedido aprovado nesse dia; este não pode ocupar o dia inteiro.";

        var occupiedKg = occupiedToday.Sum(o => o.WeightKg);
        if (weightKg > input.Settings.MaxKgPerDay - occupiedKg)
        {
            return $"Este pedido não cabe no limite do dia: já há {FormatKg(occupiedKg)} kg aprovados " +
                   $"e o máximo é {FormatKg(input.Settings.MaxKgPerDay)} kg.";
        }

        return null;
    }

    private static string FormatKg(decimal kg) =>
        kg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Dias do intervalo (inclusive) que ainda têm ao menos um horário livre. Serve para montar o calendário.</summary>
    public static IReadOnlyList<DateOnly> DaysWithFreeTimes(
        AvailabilityInput input,
        DateOnly from,
        DateOnly to,
        decimal requestedKg = 0) =>
        FreeDays(input, from, to, requestedKg).Select(d => d.Date).ToList();

    /// <summary>
    /// Dias do intervalo (inclusive) que ainda têm horário livre, cada um com os seus horários.
    /// É o que o formulário usa: o cliente só vê o que pode escolher para o peso informado.
    /// </summary>
    public static IReadOnlyList<AvailableDay> FreeDays(
        AvailabilityInput input,
        DateOnly from,
        DateOnly to,
        decimal requestedKg = 0)
    {
        var days = new List<AvailableDay>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var times = FreeTimes(input, day, requestedKg);
            if (times.Count > 0)
                days.Add(new AvailableDay(day, times));
        }

        return days;
    }
}
