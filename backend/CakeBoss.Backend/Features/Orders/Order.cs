using System.Security.Cryptography;
using CakeBoss.Backend.Features.Customers;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Orders;

/// <summary>
/// Raiz do agregado de pedido: itens e fotos só são alterados por aqui, e toda mudança de status
/// passa pelos métodos abaixo, que garantem as transições válidas.
/// </summary>
public class Order
{
    public const int MaxPhotos = 2;

    /// <summary>Status que ocupam o horário na agenda. O índice único do banco usa a mesma lista.</summary>
    public static readonly OrderStatus[] SlotBlockingStatuses =
        [OrderStatus.Approved, OrderStatus.Confirmed, OrderStatus.Completed];

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem 0/O/1/I

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderPhoto> _photos = [];

    private Order() { }

    public Guid Id { get; private set; }

    /// <summary>Código público e difícil de adivinhar, usado no link de acompanhamento do cliente.</summary>
    public string Code { get; private set; } = "";

    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;

    public OrderStatus Status { get; private set; }
    public DeliveryType DeliveryType { get; private set; }
    public string? DeliveryAddress { get; private set; }

    /// <summary>Data e hora (horário de Brasília) da retirada ou entrega.</summary>
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }
    public string? Notes { get; private set; }

    // Preenchidos pela dona na aprovação.
    public decimal? Price { get; private set; }
    public decimal? DeliveryFee { get; private set; }
    public decimal? DepositAmount { get; private set; }
    public DateTime? DepositPaidAt { get; private set; }

    /// <summary>A dona marca na aprovação os pedidos grandes ou de decoração complexa que ocupam o dia inteiro.</summary>
    public bool BlocksWholeDay { get; private set; }

    public string? RejectionReason { get; private set; }
    public string? CancellationReason { get; private set; }
    public bool DepositKept { get; private set; }

    /// <summary>Id do evento criado no Google Agenda (para atualizar/remover depois).</summary>
    public string? GoogleEventId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;
    public IReadOnlyCollection<OrderPhoto> Photos => _photos;

    /// <summary>Soma dos quilos de todos os bolos (entra na conta da capacidade diária).</summary>
    public decimal TotalWeightKg => _items.Sum(i => i.WeightKg);

    public decimal Total => (Price ?? 0) + (DeliveryFee ?? 0);
    public decimal RemainingAmount => DepositPaidAt is null ? Total : Total - (DepositAmount ?? 0);

    /// <summary>
    /// Cria um pedido Pending. Não valida expediente, bloqueios, antecedência nem capacidade:
    /// isso depende da configuração da agenda e é feito pelo cálculo de disponibilidade antes de chamar aqui.
    /// </summary>
    public static Order Create(
        Customer customer,
        DeliveryType deliveryType,
        string? deliveryAddress,
        DateOnly date,
        TimeOnly time,
        PaymentMethod paymentMethod,
        string? notes,
        IEnumerable<OrderItem> items,
        IEnumerable<OrderPhoto> photos,
        DateTime nowUtc)
    {
        var itemList = items.ToList();
        var photoList = photos.ToList();

        if (itemList.Count == 0)
            throw new DomainException("Inclua ao menos um bolo no pedido.");
        if (photoList.Count > MaxPhotos)
            throw new DomainException($"Envie no máximo {MaxPhotos} fotos de referência.");

        var address = TextCleaner.Clean(deliveryAddress, 300);
        if (deliveryType == DeliveryType.Delivery && address is null)
            throw new DomainException("Informe o endereço de entrega.");

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            Code = RandomNumberGenerator.GetString(CodeAlphabet, 8),
            CustomerId = customer.Id,
            Customer = customer,
            Status = OrderStatus.Pending,
            DeliveryType = deliveryType,
            DeliveryAddress = deliveryType == DeliveryType.Delivery ? address : null,
            Date = date,
            Time = time,
            PaymentMethod = paymentMethod,
            Notes = TextCleaner.Clean(notes, 1000),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        order._items.AddRange(itemList);
        order._photos.AddRange(photoList);
        return order;
    }

    /// <summary>A dona aceita o pedido e informa os valores. Passa a ocupar o horário na agenda.</summary>
    public void Approve(decimal price, decimal deliveryFee, decimal depositAmount, bool blocksWholeDay, DateTime nowUtc)
    {
        RequireStatus("aprovar", OrderStatus.Pending);

        if (price <= 0)
            throw new DomainException("Informe o valor do pedido.");
        if (deliveryFee < 0)
            throw new DomainException("A taxa de entrega não pode ser negativa.");
        if (deliveryFee > 0 && DeliveryType != DeliveryType.Delivery)
            throw new DomainException("Taxa de entrega só se aplica a pedidos com entrega.");
        if (depositAmount < 0 || depositAmount > price + deliveryFee)
            throw new DomainException("O sinal deve estar entre zero e o total do pedido.");

        Price = price;
        DeliveryFee = deliveryFee;
        DepositAmount = depositAmount;
        BlocksWholeDay = blocksWholeDay;
        Status = OrderStatus.Approved;
        UpdatedAt = nowUtc;
    }

    public void Reject(string? reason, DateTime nowUtc)
    {
        RequireStatus("recusar", OrderStatus.Pending);

        RejectionReason = TextCleaner.Clean(reason, 500);
        Status = OrderStatus.Rejected;
        UpdatedAt = nowUtc;
    }

    /// <summary>A dona confirma que recebeu o sinal por Pix.</summary>
    public void ConfirmDeposit(DateTime nowUtc)
    {
        RequireStatus("confirmar o sinal de", OrderStatus.Approved);

        DepositPaidAt = nowUtc;
        Status = OrderStatus.Confirmed;
        UpdatedAt = nowUtc;
    }

    public void Complete(DateTime nowUtc)
    {
        RequireStatus("concluir", OrderStatus.Confirmed);

        Status = OrderStatus.Completed;
        UpdatedAt = nowUtc;
    }

    /// <param name="keepDeposit">Se a dona fica com o sinal (só tem efeito se o sinal já foi pago).</param>
    public void Cancel(string? reason, bool keepDeposit, DateTime nowUtc)
    {
        RequireStatus("cancelar", OrderStatus.Pending, OrderStatus.Approved, OrderStatus.Confirmed);

        CancellationReason = TextCleaner.Clean(reason, 500);
        DepositKept = keepDeposit && DepositPaidAt is not null;
        Status = OrderStatus.Cancelled;
        UpdatedAt = nowUtc;
    }

    public void SetGoogleEventId(string? eventId)
    {
        GoogleEventId = TextCleaner.Clean(eventId, 200);
    }

    private void RequireStatus(string action, params OrderStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException($"Não é possível {action} um pedido com status \"{StatusLabel(Status)}\".");
    }

    private static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pendente",
        OrderStatus.Approved => "Aprovado",
        OrderStatus.Confirmed => "Confirmado",
        OrderStatus.Completed => "Concluído",
        OrderStatus.Rejected => "Recusado",
        OrderStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };
}
