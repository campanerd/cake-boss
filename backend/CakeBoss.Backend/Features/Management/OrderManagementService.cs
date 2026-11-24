using CakeBoss.Backend.Features.Notifications;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Features.Management;

/// <summary>
/// Casos de uso da dona no painel (o lado dela, como o <c>OrderService</c> é o do cliente): ver os pedidos e abrir cada um.
/// Quem protege o acesso é o login do frontend; este serviço não sabe quem está chamando.
/// </summary>
public class OrderManagementService(AppDbContext db)
{
    /// <summary>Quantos pedidos já encerrados a lista traz. Os abertos vêm sempre todos.</summary>
    public const int DefaultClosedLimit = 50;

    /// <summary>
    /// Lista para o painel. Primeiro os pedidos em andamento (pendentes antes, depois aprovados e confirmados, do que está
    /// mais perto para o mais longe); depois os encerrados (concluídos, recusados e cancelados), do mais recente para o mais antigo.
    /// </summary>
    public async Task<IReadOnlyList<OrderListItem>> ListAsync(
        int closedLimit = DefaultClosedLimit,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(closedLimit);

        OrderStatus[] open = [OrderStatus.Pending, OrderStatus.Approved, OrderStatus.Confirmed];
        OrderStatus[] closed = [OrderStatus.Completed, OrderStatus.Rejected, OrderStatus.Cancelled];

        var openOrders = await db.Orders.AsNoTracking()
            .Where(o => open.Contains(o.Status))
            .OrderBy(o => o.Status == OrderStatus.Pending ? 0 : 1)
            .ThenBy(o => o.Date)
            .ThenBy(o => o.Time)
            .Select(o => new OrderListItem(
                o.Id, o.Code, o.Status, o.Customer.Name, o.Date, o.Time, o.DeliveryType,
                o.Items.Sum(i => i.WeightKg), o.CreatedAt))
            .ToListAsync(ct);

        var closedOrders = await db.Orders.AsNoTracking()
            .Where(o => closed.Contains(o.Status))
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Time)
            .Take(closedLimit)
            .Select(o => new OrderListItem(
                o.Id, o.Code, o.Status, o.Customer.Name, o.Date, o.Time, o.DeliveryType,
                o.Items.Sum(i => i.WeightKg), o.CreatedAt))
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
            WhatsAppMessageBuilder.ForOrder(order));
    }
}
