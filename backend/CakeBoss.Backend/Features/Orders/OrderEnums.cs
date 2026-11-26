namespace CakeBoss.Backend.Features.Orders;

/// <summary>
/// Ciclo de vida do pedido:
/// Pending → Approved (aguardando sinal) → Confirmed (sinal pago) → Completed.
/// Pending também pode virar Rejected; Pending/Approved/Confirmed podem virar Cancelled.
/// Approved vira Expired se o sinal não for pago no prazo, e a dona pode reabri-lo (volta a Approved).
/// </summary>
public enum OrderStatus
{
    Pending,
    Approved,
    Confirmed,
    Completed,
    Rejected,
    Cancelled,
    Expired
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
