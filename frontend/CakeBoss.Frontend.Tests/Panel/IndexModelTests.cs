using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Frontend.Pages.Panel;

namespace CakeBoss.Frontend.Tests.Panel;

public class IndexModelTests
{
    // "Hoje" nos testes; os pedidos são, por padrão, para depois de amanhã.
    private static readonly DateOnly Today = new(2026, 11, 26);

    private static OrderListItem Item(string code, OrderStatus status, DateOnly? date = null) =>
        new(Guid.NewGuid(), code, status, "Maria Silva", date ?? new DateOnly(2026, 11, 28), new TimeOnly(10, 0),
            DeliveryType.Pickup, 2m, new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc), null);

    [Fact]
    public void GroupByStage_SplitsTheListIntoTheScreenBlocks()
    {
        var items = new[]
        {
            Item("PEND1", OrderStatus.Pending),
            Item("APPR1", OrderStatus.Approved),
            Item("EXPI1", OrderStatus.Expired),
            Item("CONF1", OrderStatus.Confirmed),
            Item("DONE1", OrderStatus.Completed),
            Item("REJE1", OrderStatus.Rejected),
            Item("CANC1", OrderStatus.Cancelled)
        };

        var groups = IndexModel.GroupByStage(items, Today);

        Assert.Equal(["PEND1"], groups.Pending.Select(o => o.Code));
        Assert.Equal(["APPR1"], groups.WaitingDeposit.Select(o => o.Code));
        Assert.Equal(["EXPI1"], groups.DepositExpired.Select(o => o.Code));
        Assert.Equal(["CONF1"], groups.Confirmed.Select(o => o.Code));
        Assert.Equal(["DONE1", "REJE1", "CANC1"], groups.Closed.Select(o => o.Code));
    }

    // Expirado cuja data ainda não passou (inclusive hoje) dá para reabrir: fica à vista. Os de datas passadas são histórico.
    [Fact]
    public void GroupByStage_ExpiredOrdersGoToTheirOwnBlockOnlyWhileTheirDateHasNotPassed()
    {
        var items = new[]
        {
            Item("TODAY", OrderStatus.Expired, Today),
            Item("LATER", OrderStatus.Expired, Today.AddDays(5)),
            Item("PAST", OrderStatus.Expired, Today.AddDays(-1))
        };

        var groups = IndexModel.GroupByStage(items, Today);

        Assert.Equal(["TODAY", "LATER"], groups.DepositExpired.Select(o => o.Code));
        Assert.Equal(["PAST"], groups.Closed.Select(o => o.Code));
    }

    // A ordem (a data mais próxima primeiro, os encerrados do mais recente) vem do backend: aqui ela não pode mudar.
    [Fact]
    public void GroupByStage_KeepsTheOrderTheBackendGave_InsideEachBlock()
    {
        var items = new[]
        {
            Item("B", OrderStatus.Pending),
            Item("A", OrderStatus.Pending),
            Item("D", OrderStatus.Completed),
            Item("C", OrderStatus.Completed)
        };

        var groups = IndexModel.GroupByStage(items, Today);

        Assert.Equal(["B", "A"], groups.Pending.Select(o => o.Code));
        Assert.Equal(["D", "C"], groups.Closed.Select(o => o.Code));
    }

    [Fact]
    public void GroupByStage_LosesNoOrder()
    {
        var items = Enum.GetValues<OrderStatus>()
            .SelectMany(s => new[] { Item(s + "-past", s, Today.AddDays(-3)), Item(s + "-next", s) })
            .ToList();

        var groups = IndexModel.GroupByStage(items, Today);

        Assert.Equal(
            items.Count,
            groups.Pending.Count + groups.WaitingDeposit.Count + groups.DepositExpired.Count
            + groups.Confirmed.Count + groups.Closed.Count);
    }

    [Fact]
    public void GroupByStage_WithNoOrders_IsEmpty()
    {
        Assert.True(IndexModel.GroupByStage([], Today).IsEmpty);
        Assert.False(IndexModel.GroupByStage([Item("X", OrderStatus.Pending)], Today).IsEmpty);
        Assert.False(IndexModel.GroupByStage([Item("Y", OrderStatus.Expired)], Today).IsEmpty);
    }
}
