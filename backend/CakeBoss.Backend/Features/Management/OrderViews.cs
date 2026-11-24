using CakeBoss.Backend.Features.Notifications;
using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Backend.Features.Management;

/// <summary>Uma linha da lista de pedidos do painel.</summary>
public sealed record OrderListItem(
    Guid Id,
    string Code,
    OrderStatus Status,
    string CustomerName,
    DateOnly Date,
    TimeOnly Time,
    DeliveryType DeliveryType,
    decimal TotalWeightKg,
    DateTime CreatedAt);

/// <summary>
/// Tudo o que a dona vê ao abrir um pedido. Diferente da página do cliente, mostra os dados de contato,
/// e traz a mensagem de WhatsApp pronta para ela negociar.
/// </summary>
public sealed record OrderDetails(
    Guid Id,
    string Code,
    OrderStatus Status,
    CustomerView Customer,
    DeliveryType DeliveryType,
    string? DeliveryAddress,
    DateOnly Date,
    TimeOnly Time,
    PaymentMethod PaymentMethod,
    string? Notes,
    IReadOnlyList<OrderItemView> Items,
    decimal TotalWeightKg,
    decimal? Price,
    decimal? DeliveryFee,
    decimal? DepositAmount,
    bool DepositPaid,
    decimal? Total,
    decimal? RemainingAmount,
    bool BlocksWholeDay,
    string? RejectionReason,
    string? CancellationReason,
    bool DepositKept,
    DateTime CreatedAt,
    WhatsAppMessage WhatsApp);

/// <summary>O telefone vem só com dígitos (DDD e número, sem o 55).</summary>
public sealed record CustomerView(string Name, string Phone, string? Email);

public sealed record OrderItemView(
    string Batter,
    IReadOnlyList<string> Fillings,
    string Frosting,
    decimal WeightKg,
    string Decoration);
