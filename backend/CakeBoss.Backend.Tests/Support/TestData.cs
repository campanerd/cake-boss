using CakeBoss.Backend.Features.Customers;
using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Backend.Tests.Support;

internal static class TestData
{
    public static readonly DateTime NowUtc = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>Sem telefone informado, gera um número diferente a cada chamada (o telefone é único no banco).</summary>
    public static Customer NewCustomer(string? phone = null) =>
        Customer.Create("Maria Silva", phone ?? $"119{Random.Shared.Next(10_000_000, 99_999_999)}", null, NowUtc);

    public static OrderItem NewItem(decimal weightKg = 2m) =>
        OrderItem.Create(
            "Chocolate",
            ["Brigadeiro", "Ninho com morango"],
            "Chantilly",
            weightKg,
            "Tema unicórnio, tons de rosa e lilás, escrita: Parabéns Ana");

    public static OrderPhoto NewPhoto(string name = "ref.jpg") =>
        OrderPhoto.Create($"orders/{name}", name, "image/jpeg", 1024, NowUtc);

    public static Order NewOrder(
        DeliveryType deliveryType = DeliveryType.Pickup,
        string? address = null,
        DateOnly? date = null,
        TimeOnly? time = null,
        IEnumerable<OrderItem>? items = null,
        IEnumerable<OrderPhoto>? photos = null) =>
        Order.Create(
            NewCustomer(),
            deliveryType,
            address,
            date ?? new DateOnly(2026, 11, 24),
            time ?? new TimeOnly(15, 0),
            PaymentMethod.Pix,
            null,
            items ?? new[] { NewItem() },
            photos ?? Array.Empty<OrderPhoto>(),
            NowUtc);

    public static Order NewApprovedOrder(DeliveryType deliveryType = DeliveryType.Pickup, string? address = null)
    {
        var order = NewOrder(deliveryType, address);
        order.Approve(150m, 0m, 50m, false, NowUtc);
        return order;
    }

    public static Order NewConfirmedOrder()
    {
        var order = NewApprovedOrder();
        order.ConfirmDeposit(NowUtc);
        return order;
    }
}
