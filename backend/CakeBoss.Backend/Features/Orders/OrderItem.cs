using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Orders;

/// <summary>Um bolo do pedido. Pertence ao agregado <see cref="Order"/>.</summary>
public class OrderItem
{
    /// <summary>Limite de segurança (não é regra da dona): o cliente pode combinar recheios, mas não infinitos.</summary>
    public const int MaxFillings = 5;

    private readonly List<string> _fillings = [];

    private OrderItem() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }

    /// <summary>Sabor da massa.</summary>
    public string Batter { get; private set; } = "";

    /// <summary>Um ou mais recheios (o cliente pode combinar).</summary>
    public IReadOnlyList<string> Fillings => _fillings;

    /// <summary>Tipo de cobertura.</summary>
    public string Frosting { get; private set; } = "";

    /// <summary>Tamanho em kg (o preço é por kg e a capacidade diária também).</summary>
    public decimal WeightKg { get; private set; }

    /// <summary>Tema, cores e escrita.</summary>
    public string Decoration { get; private set; } = "";

    public static OrderItem Create(
        string? batter,
        IEnumerable<string?> fillings,
        string? frosting,
        decimal weightKg,
        string? decoration)
    {
        var cleanBatter = TextCleaner.Clean(batter, 100)
                          ?? throw new DomainException("Informe o sabor da massa.");
        var cleanFrosting = TextCleaner.Clean(frosting, 100)
                            ?? throw new DomainException("Informe o tipo de cobertura.");
        var cleanDecoration = TextCleaner.Clean(decoration, 1000)
                              ?? throw new DomainException("Descreva a decoração (tema, cores e escrita).");

        if (weightKg is <= 0 or > 50)
            throw new DomainException("O tamanho deve ser maior que zero e no máximo 50 kg.");

        var cleanFillings = fillings
            .Select(f => TextCleaner.Clean(f, 100))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanFillings.Count == 0)
            throw new DomainException("Informe ao menos um recheio.");
        if (cleanFillings.Count > MaxFillings)
            throw new DomainException($"Escolha no máximo {MaxFillings} recheios.");

        var item = new OrderItem
        {
            Id = Guid.CreateVersion7(),
            Batter = cleanBatter,
            Frosting = cleanFrosting,
            WeightKg = weightKg,
            Decoration = cleanDecoration
        };

        item._fillings.AddRange(cleanFillings);
        return item;
    }
}
