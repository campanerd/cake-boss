using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Customers;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CakeBoss.Backend.Features.Ordering;

/// <summary>
/// Casos de uso do cliente: fazer um pedido e acompanhá-lo pelo código público.
/// Fica numa feature à parte para não criar ciclo: depende de Orders, Customers e Availability.
/// </summary>
public class OrderService(AppDbContext db, AvailabilityService availability, TimeProvider clock)
{
    private const string CustomerPhoneConstraint = "IX_Customers_Phone";

    /// <summary>
    /// Cria um pedido pendente. O cliente é identificado pelo telefone: se já existir, é reaproveitado (nome atualizado; o e-mail só
    /// muda se vier um novo). Lança <see cref="DomainException"/>, com mensagem em português, se algum dado for inválido ou se o horário
    /// não estiver disponível. Pedido pendente não ocupa o horário: só a aprovação da dona ocupa.
    /// </summary>
    public async Task<PlacedOrder> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // O telefone é único no banco: se dois envios do mesmo número novo chegarem juntos, o segundo falha ao gravar o cliente.
        // Então tenta uma vez de novo, e já encontra o cliente criado pelo primeiro.
        for (var attempt = 1; ; attempt++)
        {
            var order = await BuildOrderAsync(command, ct);
            await EnsureAvailableAsync(order, ct);

            db.Orders.Add(order);
            try
            {
                await db.SaveChangesAsync(ct);
                return new PlacedOrder(order.Id, order.Code, order.Date, order.Time);
            }
            catch (DbUpdateException exception) when (attempt == 1 && IsCustomerPhoneConflict(exception))
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Dados para a página de acompanhamento, ou null se o código não existir (ou não puder ser um código).</summary>
    public async Task<OrderTrackingView?> GetByCodeAsync(string? code, CancellationToken ct = default)
    {
        var normalized = Order.NormalizeCode(code);
        if (normalized is null) return null;

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Code == normalized, ct);

        if (order is null) return null;

        var items = order.Items
            .Select(i => new TrackedItem(i.Batter, i.Fillings.ToList(), i.Frosting, i.WeightKg, i.Decoration))
            .ToList();

        var approved = order.Price is not null;

        return new OrderTrackingView(
            order.Code,
            order.Status,
            order.Customer.Name,
            order.DeliveryType,
            order.DeliveryAddress,
            order.Date,
            order.Time,
            order.PaymentMethod,
            order.Notes,
            items,
            order.Price,
            order.DeliveryFee,
            order.DepositAmount,
            approved ? order.Total : null,
            approved ? order.RemainingAmount : null,
            order.DepositPaidAt is not null,
            order.RejectionReason,
            order.CreatedAt);
    }

    private async Task<Order> BuildOrderAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var name = command.CustomerName ?? "";
        var phone = Customer.NormalizePhone(command.CustomerPhone);

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Phone == phone, ct);
        if (customer is null)
        {
            customer = Customer.Create(name, phone, command.CustomerEmail, now);
        }
        else
        {
            var email = string.IsNullOrWhiteSpace(command.CustomerEmail) ? customer.Email : command.CustomerEmail;
            customer.Update(name, email);
        }

        var items = command.Items
            .Select(i => OrderItem.Create(i.Batter, i.Fillings, i.Frosting, i.WeightKg, i.Decoration))
            .ToList();
        var photos = command.Photos
            .Select(p => OrderPhoto.Create(p.StorageKey, p.OriginalName, p.ContentType, p.SizeBytes, now))
            .ToList();

        return Order.Create(
            customer,
            command.DeliveryType,
            command.DeliveryAddress,
            command.Date,
            command.Time,
            command.PaymentMethod,
            command.Notes,
            items,
            photos,
            now);
    }

    private async Task EnsureAvailableAsync(Order order, CancellationToken ct)
    {
        // Duas perguntas para dar a mensagem certa: primeiro o horário em si, depois o tamanho do pedido.
        if (!await availability.IsAvailableAsync(order.Date, order.Time, 0, ct))
            throw new DomainException("O horário escolhido não está disponível. Escolha outro dia ou horário.");

        if (!await availability.IsAvailableAsync(order.Date, order.Time, order.TotalWeightKg, ct))
            throw new DomainException("Esse dia não comporta o tamanho do pedido. Escolha outro dia ou reduza o tamanho.");
    }

    private static bool IsCustomerPhoneConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: CustomerPhoneConstraint
        };
}
