using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Backend.Features.Ordering;

/// <summary>Dados do formulário público de pedido. A validação das regras é feita pelo domínio.</summary>
public sealed record PlaceOrderCommand(
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    DeliveryType DeliveryType,
    string? DeliveryAddress,
    DateOnly Date,
    TimeOnly Time,
    PaymentMethod PaymentMethod,
    string? Notes,
    IReadOnlyList<PlaceOrderItem> Items,
    IReadOnlyList<PlaceOrderPhoto> Photos);

/// <summary>Um bolo pedido. Os recheios podem ser combinados.</summary>
public sealed record PlaceOrderItem(
    string? Batter,
    IReadOnlyList<string?> Fillings,
    string? Frosting,
    decimal WeightKg,
    string? Decoration);

/// <summary>Foto de referência já enviada ao storage: aqui só chega a referência.</summary>
public sealed record PlaceOrderPhoto(string StorageKey, string OriginalName, string ContentType, long SizeBytes);
