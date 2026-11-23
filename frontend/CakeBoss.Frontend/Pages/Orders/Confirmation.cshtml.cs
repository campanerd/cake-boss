using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Frontend.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CakeBoss.Frontend.Pages.Orders;

/// <summary>Página que o cliente vê logo depois de enviar o pedido.</summary>
public class ConfirmationModel(OrderService orders, IOptions<SiteOptions> site) : PageModel
{
    public SiteOptions Site => site.Value;

    public OrderTrackingView? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(string code, CancellationToken ct)
    {
        if (!Site.OrdersEnabled) return NotFound();

        Order = await orders.GetByCodeAsync(code, ct);
        return Order is null ? NotFound() : Page();
    }
}
