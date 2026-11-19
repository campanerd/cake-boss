namespace CakeBoss.Backend.Features.Orders;

/// <summary>
/// Ciclo de vida do pedido:
/// Pending → Approved (aguardando sinal) → Confirmed (sinal pago) → Completed.
/// Pending também pode virar Rejected; Pending/Approved/Confirmed podem virar Cancelled.
/// </summary>
public enum OrderStatus
{
    Pending,
    Approved,
    Confirmed,
    Completed,
    Rejected,
    Cancelled
}

public enum DeliveryType
{
    Pickup,
    Delivery
}

/// <summary>Como o cliente pretende pagar (o sinal é sempre combinado por Pix após a aprovação).</summary>
public enum PaymentMethod
{
    Pix,
    Cash,
    Card
}
