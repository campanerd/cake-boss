using CakeBoss.Backend.Features.Management;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CakeBoss.Frontend.Pages.Panel.Orders;

/// <summary>Um pedido aberto pela dona: contato, o que o cliente pediu, valores e o botão que chama o cliente no WhatsApp.</summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetailsModel(OrderManagementService orders) : PageModel
{
    public OrderDetails? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Order = await orders.GetAsync(id, ct);
        return Order is null ? NotFound() : Page();
    }
}
