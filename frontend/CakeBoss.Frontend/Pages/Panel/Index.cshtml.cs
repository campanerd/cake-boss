using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Orders;
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
public class IndexModel(OrderManagementService orders) : PageModel
{
    public OrderGroups Groups { get; private set; } = new([], [], [], []);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Groups = GroupByStage(await orders.ListAsync(ct: ct));
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("Login");
    }

    /// <summary>Separa a lista nos blocos da tela, mantendo a ordem que o backend deu dentro de cada um.</summary>
    public static OrderGroups GroupByStage(IEnumerable<OrderListItem> items)
    {
        var list = items.ToList();

        return new OrderGroups(
            list.Where(o => o.Status == OrderStatus.Pending).ToList(),
            list.Where(o => o.Status == OrderStatus.Approved).ToList(),
            list.Where(o => o.Status == OrderStatus.Confirmed).ToList(),
            list.Where(o => o.Status is OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.Expired).ToList());
    }

    public sealed record OrderGroups(
        IReadOnlyList<OrderListItem> Pending,
        IReadOnlyList<OrderListItem> WaitingDeposit,
        IReadOnlyList<OrderListItem> Confirmed,
        IReadOnlyList<OrderListItem> Closed)
    {
        public bool IsEmpty => Pending.Count + WaitingDeposit.Count + Confirmed.Count + Closed.Count == 0;
    }
}
