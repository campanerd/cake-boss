using System.Globalization;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Shared;
using CakeBoss.Frontend.Pages.Orders;

namespace CakeBoss.Frontend.Tests.Orders;

public class OrderFormTests
{
    private static OrderForm ValidForm() => new()
    {
        CustomerName = "Maria Silva",
        CustomerPhone = "(11) 91234-5678",
        CustomerEmail = "maria@example.com",
        Batter = "Chocolate",
        Filling1 = "Brigadeiro",
        Frosting = "Chantilly",
        WeightKg = "2,5",
        Decoration = "Tema unicórnio",
        DeliveryType = DeliveryType.Pickup,
        Date = new DateOnly(2026, 11, 27),
        Time = new TimeOnly(15, 0),
        PaymentMethod = PaymentMethod.Pix,
        Notes = "Sem nozes"
    };

    [Fact]
    public void ToCommand_MapsEveryField()
    {
        var command = ValidForm().ToCommand();

        Assert.Equal("Maria Silva", command.CustomerName);
        Assert.Equal("(11) 91234-5678", command.CustomerPhone);
        Assert.Equal("maria@example.com", command.CustomerEmail);
        Assert.Equal(DeliveryType.Pickup, command.DeliveryType);
        Assert.Equal(new DateOnly(2026, 11, 27), command.Date);
        Assert.Equal(new TimeOnly(15, 0), command.Time);
        Assert.Equal(PaymentMethod.Pix, command.PaymentMethod);
        Assert.Equal("Sem nozes", command.Notes);
        Assert.Empty(command.Photos);

        var item = Assert.Single(command.Items);
        Assert.Equal("Chocolate", item.Batter);
        Assert.Equal("Chantilly", item.Frosting);
        Assert.Equal(2.5m, item.WeightKg);
        Assert.Equal("Tema unicórnio", item.Decoration);
    }

    [Fact]
    public void ToCommand_KeepsOnlyTheFilledFillings_InOrder()
    {
        var form = ValidForm();
        form.Filling1 = "Brigadeiro";
        form.Filling2 = "   ";
        form.Filling3 = "Ninho";

        var item = Assert.Single(form.ToCommand().Items);

        Assert.Equal(["Brigadeiro", "Ninho"], item.Fillings);
    }

    [Theory]
    [InlineData("2,5", 2.5)]
    [InlineData("2.5", 2.5)]
    [InlineData("  3  ", 3)]
    [InlineData("10", 10)]
    [InlineData("0,5", 0.5)]
    public void ToCommand_ParsesTheWeightWithCommaOrDot(string text, double expected)
    {
        var form = ValidForm();
        form.WeightKg = text;

        Assert.Equal((decimal)expected, Assert.Single(form.ToCommand().Items).WeightKg);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void ToCommand_ParsesTheWeightRegardlessOfTheServerCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var form = ValidForm();
            form.WeightKg = "2.5";

            Assert.Equal(2.5m, Assert.Single(form.ToCommand().Items).WeightKg);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("dois")]
    public void ToCommand_WithAWeightThatIsNotANumber_Throws(string? text)
    {
        var form = ValidForm();
        form.WeightKg = text;

        var exception = Assert.Throws<DomainException>(() => form.ToCommand());

        Assert.Contains("kg", exception.Message);
    }

    [Fact]
    public void ToCommand_WithoutDate_Throws()
    {
        var form = ValidForm();
        form.Date = null;

        Assert.Throws<DomainException>(() => form.ToCommand());
    }

    [Fact]
    public void ToCommand_WithoutTime_Throws()
    {
        var form = ValidForm();
        form.Time = null;

        Assert.Throws<DomainException>(() => form.ToCommand());
    }

    [Fact]
    public void ToCommand_DoesNotValidateBusinessRules_ThatIsTheDomainsJob()
    {
        var form = ValidForm();
        form.CustomerName = "";
        form.Batter = null;

        var command = form.ToCommand();

        Assert.Equal("", command.CustomerName);
        Assert.Null(Assert.Single(command.Items).Batter);
    }

    [Fact]
    public void NewForm_StartsWithPickupAndPix()
    {
        var form = new OrderForm();

        Assert.Equal(DeliveryType.Pickup, form.DeliveryType);
        Assert.Equal(PaymentMethod.Pix, form.PaymentMethod);
    }
}
