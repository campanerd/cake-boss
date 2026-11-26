using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Backend.Features.Ordering;

/// <summary>O que o cliente recebe depois de enviar o pedido.</summary>
public sealed record PlacedOrder(Guid Id, string Code, DateOnly Date, TimeOnly Time);

/// <summary>
/// O que a página de acompanhamento mostra. Não leva telefone nem e-mail: o link com o código é a única "senha" do cliente,
/// então só sai daqui o que ele mesmo informou ou o que a dona combinou com ele.
/// Os valores só aparecem depois que a dona aprova.
/// <c>DepositDueAt</c> (UTC) é até quando o sinal pode ser pago; só vem enquanto o pedido espera o sinal.
/// </summary>
public sealed record OrderTrackingView(
    string Code,
    OrderStatus Status,
    string CustomerName,
    DeliveryType DeliveryType,
    string? DeliveryAddress,
    DateOnly Date,
    TimeOnly Time,
    PaymentMethod PaymentMethod,
    string? Notes,
    IReadOnlyList<TrackedItem> Items,
    decimal? Price,
    decimal? DeliveryFee,
    decimal? DepositAmount,
    decimal? Total,
    decimal? RemainingAmount,
    bool DepositPaid,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime? DepositDueAt);

public sealed record TrackedItem(
    string Batter,
    IReadOnlyList<string> Fillings,
    string Frosting,
    decimal WeightKg,
    string Decoration);
