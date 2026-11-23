using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Frontend.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CakeBoss.Frontend.Pages.Orders;

/// <summary>
/// Acompanhamento do pedido pelo código. Sem código, mostra o campo para digitá-lo.
/// Os dados vêm prontos do backend (sem telefone nem e-mail; valores só depois da aprovação).
/// </summary>
public class TrackModel(OrderService orders, IOptions<SiteOptions> site) : PageModel
{
    /// <summary>Etapas que o cliente vê, na ordem em que o pedido passa por elas.</summary>
    public static readonly string[] Steps = ["Enviado", "Aprovado", "Sinal pago", "Concluído"];

    public SiteOptions Site => site.Value;

    public OrderTrackingView? Order { get; private set; }

    /// <summary>O código digitado, para devolver no campo quando não for encontrado.</summary>
    public string? Code { get; private set; }

    public bool NotFoundCode { get; private set; }

    /// <summary>Até qual etapa o pedido chegou (0 a 3), ou null se foi recusado ou cancelado.</summary>
    public int? CurrentStep => Order?.Status switch
    {
        OrderStatus.Pending => 0,
        OrderStatus.Approved => 1,
        OrderStatus.Confirmed => 2,
        OrderStatus.Completed => 3,
        _ => null
    };

    public async Task<IActionResult> OnGetAsync(string? code, CancellationToken ct)
    {
        if (!Site.OrdersEnabled) return NotFound();
        if (string.IsNullOrWhiteSpace(code)) return Page();

        Code = code.Trim();
        Order = await orders.GetByCodeAsync(Code, ct);

        if (Order is null)
        {
            NotFoundCode = true;
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }

        // Veio do campo de busca (?code=...): leva para o endereço próprio do pedido, que o cliente pode guardar.
        if (Request.Query.ContainsKey("code") || Code != Order.Code)
            return RedirectToPage(new { code = Order.Code });

        return Page();
    }
}
