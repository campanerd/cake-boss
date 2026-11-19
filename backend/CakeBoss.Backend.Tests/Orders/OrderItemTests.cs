using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Tests.Orders;

public class OrderItemTests
{
    private static OrderItem Create(
        string? batter = "Chocolate",
        string?[]? fillings = null,
        string? frosting = "Chantilly",
        decimal weightKg = 2m,
        string? decoration = "Tema unicórnio") =>
        OrderItem.Create(batter, fillings ?? ["Brigadeiro"], frosting, weightKg, decoration);

    [Fact]
    public void Create_TrimsTextAndKeepsValues()
    {
        var item = Create("  Chocolate  ", ["  Brigadeiro  "], " Chantilly ", 2.5m, "  Tema unicórnio  ");

        Assert.Equal("Chocolate", item.Batter);
        Assert.Equal(["Brigadeiro"], item.Fillings);
        Assert.Equal("Chantilly", item.Frosting);
        Assert.Equal(2.5m, item.WeightKg);
        Assert.Equal("Tema unicórnio", item.Decoration);
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    [Fact]
    public void Create_AllowsCombiningFillings_KeepingOrder()
    {
        var item = Create(fillings: ["Brigadeiro", "Ninho", "Morango"]);

        Assert.Equal(["Brigadeiro", "Ninho", "Morango"], item.Fillings);
    }

    [Fact]
    public void Create_IgnoresBlankAndDuplicateFillings()
    {
        var item = Create(fillings: ["Brigadeiro", "  ", null, "brigadeiro", "Ninho"]);

        Assert.Equal(["Brigadeiro", "Ninho"], item.Fillings);
    }

    [Fact]
    public void Create_WithoutAnyFilling_Throws()
    {
        Assert.Throws<DomainException>(() => Create(fillings: []));
        Assert.Throws<DomainException>(() => Create(fillings: ["  ", null]));
    }

    [Fact]
    public void Create_WithMoreThanMaxFillings_Throws()
    {
        var fillings = Enumerable.Range(1, OrderItem.MaxFillings + 1).Select(i => $"Recheio {i}").ToArray();

        Assert.Throws<DomainException>(() => Create(fillings: fillings));
    }

    [Fact]
    public void Create_WithMaxFillings_Succeeds()
    {
        var fillings = Enumerable.Range(1, OrderItem.MaxFillings).Select(i => $"Recheio {i}").ToArray();

        var item = Create(fillings: fillings);

        Assert.Equal(OrderItem.MaxFillings, item.Fillings.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutBatter_Throws(string? batter)
    {
        Assert.Throws<DomainException>(() => Create(batter: batter));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutFrosting_Throws(string? frosting)
    {
        Assert.Throws<DomainException>(() => Create(frosting: frosting));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutDecoration_Throws(string? decoration)
    {
        Assert.Throws<DomainException>(() => Create(decoration: decoration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50.01)]
    public void Create_WithInvalidWeight_Throws(double weightKg)
    {
        Assert.Throws<DomainException>(() => Create(weightKg: (decimal)weightKg));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(50)]
    public void Create_WithBoundaryWeight_Succeeds(double weightKg)
    {
        var item = Create(weightKg: (decimal)weightKg);

        Assert.Equal((decimal)weightKg, item.WeightKg);
    }

    [Fact]
    public void Create_WithTooLongDecoration_Throws()
    {
        Assert.Throws<DomainException>(() => Create(decoration: new string('a', 1001)));
    }
}
