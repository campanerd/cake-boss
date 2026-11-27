using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CakeBoss.Frontend.Pages.Panel;

/// <summary>
/// Início do painel da dona: a lista de pedidos. Tudo em /painel exige login (regra no Program.cs), menos a tela de entrar.
/// Fino de propósito: a ordem e o limite da lista vêm do backend; aqui só se separa em blocos para mostrar.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(OrderManagementService orders, TimeProvider clock) : PageModel
{
    public OrderGroups Groups { get; private set; } = new([], [], [], [], []);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(BrazilTime.Now(clock));
        Groups = GroupByStage(await orders.ListAsync(ct: ct), today);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("Login");
    }

    /// <summary>
    /// Separa a lista nos blocos da tela, mantendo a ordem que o backend deu dentro de cada um. Os expirados cuja data ainda não
    /// passou (<paramref name="today"/>, em Brasília) ganham um bloco próprio, porque ainda dá para reabri-los; os antigos vão para os encerrados.
    /// </summary>
    public static OrderGroups GroupByStage(IEnumerable<OrderListItem> items, DateOnly today)
    {
        var list = items.ToList();
        bool Reopenable(OrderListItem o) => o.Status == OrderStatus.Expired && o.Date >= today;

        return new OrderGroups(
            list.Where(o => o.Status == OrderStatus.Pending).ToList(),
            list.Where(o => o.Status == OrderStatus.Approved).ToList(),
            list.Where(Reopenable).ToList(),
            list.Where(o => o.Status == OrderStatus.Confirmed).ToList(),
            list.Where(o => o.Status is OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Cancelled
                || (o.Status == OrderStatus.Expired && !Reopenable(o))).ToList());
    }

    public sealed record OrderGroups(
        IReadOnlyList<OrderListItem> Pending,
        IReadOnlyList<OrderListItem> WaitingDeposit,
        IReadOnlyList<OrderListItem> DepositExpired,
        IReadOnlyList<OrderListItem> Confirmed,
        IReadOnlyList<OrderListItem> Closed)
    {
        public bool IsEmpty =>
            Pending.Count + WaitingDeposit.Count + DepositExpired.Count + Confirmed.Count + Closed.Count == 0;
    }
}
