namespace CakeBoss.Backend.Features.Management;

/// <summary>
/// O que a dona combinou com o cliente ao aprovar o pedido. Os valores são livres: ela os negocia na conversa e só registra aqui.
/// </summary>
/// <param name="Price">Valor do bolo (sem a taxa de entrega).</param>
/// <param name="DeliveryFee">Taxa de entrega combinada; zero na retirada.</param>
/// <param name="DepositAmount">Sinal combinado, de zero até o total (valor + taxa).</param>
/// <param name="BlocksWholeDay">Se o pedido ocupa o dia inteiro (bolo grande ou decoração complexa).</param>
public sealed record ApproveOrderCommand(
    Guid OrderId,
    decimal Price,
    decimal DeliveryFee,
    decimal DepositAmount,
    bool BlocksWholeDay);
