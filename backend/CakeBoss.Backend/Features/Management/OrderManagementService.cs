using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Notifications;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CakeBoss.Backend.Features.Management;

/// <summary>
/// Casos de uso da dona no painel (o lado dela, como o <c>OrderService</c> é o do cliente): ver os pedidos, abrir cada um
/// e conduzi-lo pelo ciclo (aprovar, recusar, confirmar o sinal, concluir, cancelar).
/// Quem protege o acesso é o login do frontend; este serviço não sabe quem está chamando.
/// As ações lançam <see cref="DomainException"/>, com mensagem em português, quando não podem ser feitas.
/// </summary>
public class OrderManagementService(AppDbContext db, AvailabilityService availability, TimeProvider clock)
{
    private const string OccupiedSlotIndex = "UX_Orders_Occupied_Slot";

    /// <summary>Quantos pedidos já encerrados a lista traz. Os abertos vêm sempre todos.</summary>
    public const int DefaultClosedLimit = 50;

    /// <summary>
    /// Lista para o painel. Primeiro os pedidos em andamento (pendentes antes, depois aprovados e confirmados, do que está
    /// mais perto para o mais longe); depois os encerrados (concluídos, recusados, cancelados e expirados), do mais recente
    /// para o mais antigo. Antes de listar, fecha os pedidos cujo prazo do sinal venceu (<see cref="ExpireOverdueAsync"/>).
    /// </summary>
    public async Task<IReadOnlyList<OrderListItem>> ListAsync(
        int closedLimit = DefaultClosedLimit,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(closedLimit);

        await ExpireOverdueAsync(ct);

        OrderStatus[] open = [OrderStatus.Pending, OrderStatus.Approved, OrderStatus.Confirmed];
        OrderStatus[] closed = [OrderStatus.Completed, OrderStatus.Rejected, OrderStatus.Cancelled, OrderStatus.Expired];

        var openOrders = await db.Orders.AsNoTracking()
            .Where(o => open.Contains(o.Status))
            .OrderBy(o => o.Status == OrderStatus.Pending ? 0 : 1)
            .ThenBy(o => o.Date)
            .ThenBy(o => o.Time)
            .Select(o => new OrderListItem(
                o.Id, o.Code, o.Status, o.Customer.Name, o.Date, o.Time, o.DeliveryType,
                o.Items.Sum(i => i.WeightKg), o.CreatedAt, o.DepositDueAt))
            .ToListAsync(ct);

        var closedOrders = await db.Orders.AsNoTracking()
            .Where(o => closed.Contains(o.Status))
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Time)
            .Take(closedLimit)
            .Select(o => new OrderListItem(
                o.Id, o.Code, o.Status, o.Customer.Name, o.Date, o.Time, o.DeliveryType,
                o.Items.Sum(i => i.WeightKg), o.CreatedAt, o.DepositDueAt))
            .ToListAsync(ct);

        return [.. openOrders, .. closedOrders];
    }

    /// <summary>Detalhe de um pedido, ou null se não existir.</summary>
    public async Task<OrderDetails?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : ToDetails(order);
    }

    /// <summary>
    /// Aprova o pedido com o que ela combinou com o cliente. Antes de gravar, confere de novo se o horário e os kg ainda cabem
    /// (a agenda pode ter mudado desde o pedido); o pedido passa a ocupar o horário e a ter 5 horas para o sinal.
    /// </summary>
    public async Task ApproveAsync(ApproveOrderCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Antes de tudo, fecha os aprovados vencidos: para o índice único do banco eles ainda ocupam o horário.
        await ExpireOverdueAsync(ct);

        var order = await LoadForChangeAsync(command.OrderId, ct);

        // Primeiro a entidade, que confere o status e os valores; só depois a agenda (um pedido que já foi aprovado
        // apareceria como conflito com ele mesmo). Nada é gravado se alguma das duas recusar.
        order.Approve(command.Price, command.DeliveryFee, command.DepositAmount, command.BlocksWholeDay, Now());

        await SaveOccupyingSlotAsync(order, ct);
    }

    /// <summary>
    /// Reabre um pedido que expirou (por exemplo, o Pix chegou depois do prazo). Só funciona se o horário e os kg ainda
    /// estiverem livres; o pedido volta a Aprovado com um novo prazo de 5 horas.
    /// </summary>
    public async Task ReopenAsync(Guid orderId, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);

        var order = await LoadForChangeAsync(orderId, ct);
        order.Reopen(Now());

        await SaveOccupyingSlotAsync(order, ct);
    }

    /// <summary>
    /// Fecha os pedidos aprovados cujo prazo do sinal já venceu (viram Expirado) e devolve quantos foram. Roda ao listar, antes
    /// de aprovar ou reabrir (e, depois, no /health). A disponibilidade já ignora esses pedidos por conta própria: isto só
    /// arruma o registro e libera o índice único do banco. De propósito, NÃO roda ao confirmar o sinal: a palavra da dona
    /// sobre o Pix recebido vale mais que o relógio, enquanto o pedido ainda não foi fechado.
    /// </summary>
    public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
    {
        var now = Now();

        var overdue = await db.Orders
            .Where(o => o.Status == OrderStatus.Approved && o.DepositDueAt != null && o.DepositDueAt <= now)
            .ToListAsync(ct);

        foreach (var order in overdue)
            order.Expire(now);

        if (overdue.Count > 0)
            await db.SaveChangesAsync(ct);

        return overdue.Count;
    }

    public async Task RejectAsync(Guid orderId, string? reason, CancellationToken ct = default)
    {
        var order = await LoadForChangeAsync(orderId, ct);
        order.Reject(reason, Now());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A dona recebeu o sinal por Pix.</summary>
    public async Task ConfirmDepositAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadForChangeAsync(orderId, ct);
        order.ConfirmDeposit(Now());
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadForChangeAsync(orderId, ct);
        order.Complete(Now());
        await db.SaveChangesAsync(ct);
    }

    /// <param name="keepDeposit">Se a dona fica com o sinal (só vale se o sinal já foi pago).</param>
    public async Task CancelAsync(Guid orderId, string? reason, bool keepDeposit, CancellationToken ct = default)
    {
        var order = await LoadForChangeAsync(orderId, ct);
        order.Cancel(reason, keepDeposit, Now());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Confere a agenda e grava o pedido que passou a ocupar um horário (aprovado ou reaberto). Se houver conflito, descarta a
    /// alteração em memória: o pedido continua como estava, também para quem reutilizar este contexto.
    /// </summary>
    private async Task SaveOccupyingSlotAsync(Order order, CancellationToken ct)
    {
        var conflict = await availability.FindApprovalConflictAsync(
            order.Id, order.Date, order.Time, order.TotalWeightKg, order.BlocksWholeDay, ct);
        if (conflict is not null)
        {
            db.ChangeTracker.Clear();
            throw new DomainException(conflict);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsOccupiedSlotConflict(exception))
        {
            // Duas aprovações para o mesmo horário ao mesmo tempo: o índice único do banco barra a segunda.
            db.ChangeTracker.Clear();
            throw new DomainException("Outro pedido acabou de ocupar esse horário. Atualize a página e confira a agenda.");
        }
    }

    private async Task<Order> LoadForChangeAsync(Guid orderId, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new DomainException("Pedido não encontrado.");

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private static bool IsOccupiedSlotConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: OccupiedSlotIndex
        };

    private static OrderDetails ToDetails(Order order)
    {
        var approved = order.Price is not null;

        return new OrderDetails(
            order.Id,
            order.Code,
            order.Status,
            new CustomerView(order.Customer.Name, order.Customer.Phone, order.Customer.Email),
            order.DeliveryType,
            order.DeliveryAddress,
            order.Date,
            order.Time,
            order.PaymentMethod,
            order.Notes,
            order.Items
                .Select(i => new OrderItemView(i.Batter, i.Fillings.ToList(), i.Frosting, i.WeightKg, i.Decoration))
                .ToList(),
            order.TotalWeightKg,
            order.Price,
            order.DeliveryFee,
            order.DepositAmount,
            order.DepositPaidAt is not null,
            approved ? order.Total : null,
            approved ? order.RemainingAmount : null,
            order.BlocksWholeDay,
            order.RejectionReason,
            order.CancellationReason,
            order.DepositKept,
            order.CreatedAt,
            order.DepositDueAt,
            WhatsAppMessageBuilder.ForOrder(order));
    }
}
