using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Frontend.Pages.Panel;

namespace CakeBoss.Frontend.Tests.Panel;

public class IndexModelTests
{
    private static OrderListItem Item(string code, OrderStatus status) =>
        new(Guid.NewGuid(), code, status, "Maria Silva", new DateOnly(2026, 11, 28), new TimeOnly(10, 0),
            DeliveryType.Pickup, 2m, new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void GroupByStage_SplitsTheListIntoTheScreenBlocks()
    {
        var items = new[]
        {
            Item("PEND1", OrderStatus.Pending),
            Item("APPR1", OrderStatus.Approved),
            Item("CONF1", OrderStatus.Confirmed),
            Item("DONE1", OrderStatus.Completed),
            Item("REJE1", OrderStatus.Rejected),
            Item("CANC1", OrderStatus.Cancelled)
        };

        var groups = IndexModel.GroupByStage(items);

        Assert.Equal(["PEND1"], groups.Pending.Select(o => o.Code));
        Assert.Equal(["APPR1"], groups.WaitingDeposit.Select(o => o.Code));
        Assert.Equal(["CONF1"], groups.Confirmed.Select(o => o.Code));
        Assert.Equal(["DONE1", "REJE1", "CANC1"], groups.Closed.Select(o => o.Code));
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

        var groups = IndexModel.GroupByStage(items);

        Assert.Equal(["B", "A"], groups.Pending.Select(o => o.Code));
        Assert.Equal(["D", "C"], groups.Closed.Select(o => o.Code));
    }

    [Fact]
    public void GroupByStage_LosesNoOrder()
    {
        var items = Enum.GetValues<OrderStatus>().Select(s => Item(s.ToString(), s)).ToList();

        var groups = IndexModel.GroupByStage(items);

        Assert.Equal(
            items.Count,
            groups.Pending.Count + groups.WaitingDeposit.Count + groups.Confirmed.Count + groups.Closed.Count);
    }

    [Fact]
    public void GroupByStage_WithNoOrders_IsEmpty()
    {
        Assert.True(IndexModel.GroupByStage([]).IsEmpty);
        Assert.False(IndexModel.GroupByStage([Item("X", OrderStatus.Pending)]).IsEmpty);
    }
}
