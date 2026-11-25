using CakeBoss.Frontend.Pages.Panel.Orders;

namespace CakeBoss.Frontend.Tests.Panel;

public class ApproveFormTests
{
    [Theory]
    [InlineData("150", 150)]
    [InlineData("150,50", 150.5)]
    [InlineData("150.50", 150.5)] // ponto com 2 dígitos depois: decimal
    [InlineData("150.5", 150.5)]
    [InlineData("1.500", 1500)] // ponto com 3 dígitos depois: milhar
    [InlineData("1.500,00", 1500)]
    [InlineData("12.345.678,90", 12345678.9)]
    [InlineData("R$ 150", 150)]
    [InlineData("r$150,00", 150)]
    [InlineData("  80  ", 80)]
    [InlineData("0", 0)]
    [InlineData(",5", 0.5)]
    public void TryParseMoney_ReadsTheWayItIsTypedInBrazil(string text, double expected)
    {
        Assert.True(ApproveForm.TryParseMoney(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    // Em branco vale zero: o backend recusa o valor do pedido zerado com a mensagem certa, e sinal e taxa vazios são zero mesmo.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("R$")]
    public void TryParseMoney_Blank_IsZero(string? text)
    {
        Assert.True(ApproveForm.TryParseMoney(text, out var value));
        Assert.Equal(0m, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("-50")] // sem sinal negativo
    [InlineData("+50")]
    [InlineData("150,555")] // mais de 2 casas
    [InlineData("1,2,3")]
    [InlineData("150,50,00")]
    [InlineData("100000000")] // acima do que o banco guarda
    [InlineData("1e3")]
    public void TryParseMoney_RejectsWhatIsNotAMoneyValue(string text)
    {
        Assert.False(ApproveForm.TryParseMoney(text, out _));
    }

    [Fact]
    public void TryToCommand_BuildsTheCommandFromTheTypedValues()
    {
        var id = Guid.NewGuid();
        var form = new ApproveForm { Price = "150,00", DeliveryFee = "20", Deposit = "70,5", BlocksWholeDay = true };

        Assert.True(form.TryToCommand(id, out var command, out var error));

        Assert.Null(error);
        Assert.NotNull(command);
        Assert.Equal(id, command.OrderId);
        Assert.Equal(150m, command.Price);
        Assert.Equal(20m, command.DeliveryFee);
        Assert.Equal(70.5m, command.DepositAmount);
        Assert.True(command.BlocksWholeDay);
    }

    [Fact]
    public void TryToCommand_WithEmptyFeeAndDeposit_UsesZero()
    {
        var form = new ApproveForm { Price = "150" };

        Assert.True(form.TryToCommand(Guid.NewGuid(), out var command, out _));

        Assert.Equal(0m, command!.DeliveryFee);
        Assert.Equal(0m, command.DepositAmount);
        Assert.False(command.BlocksWholeDay);
    }

    [Theory]
    [InlineData("xx", null, null, "o valor do bolo")]
    [InlineData("150", "xx", null, "a taxa de entrega")]
    [InlineData("150", "10", "xx", "o sinal")]
    public void TryToCommand_WithAnUnreadableValue_NamesTheField(string price, string? fee, string? deposit, string field)
    {
        var form = new ApproveForm { Price = price, DeliveryFee = fee, Deposit = deposit };

        Assert.False(form.TryToCommand(Guid.NewGuid(), out var command, out var error));

        Assert.Null(command);
        Assert.Equal($"Não entendi {field}. Use só números, como 150 ou 150,50.", error);
    }
}
