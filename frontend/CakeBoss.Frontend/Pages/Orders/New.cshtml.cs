using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Shared;
using CakeBoss.Frontend.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CakeBoss.Frontend.Pages.Orders;

/// <summary>Formulário público de pedido. Fino de propósito: toda regra fica no backend.</summary>
public class NewModel(OrderService orders, IOptions<SiteOptions> site) : PageModel
{
    [BindProperty]
    public OrderForm Form { get; set; } = new();

    public SiteOptions Site => site.Value;

    public bool Enabled => site.Value.OrdersEnabled;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        // Robôs preenchem o campo escondido; a pessoa nunca o vê. Eles não ganham pedido nem mensagem de erro.
        if (!string.IsNullOrWhiteSpace(Form.Website)) return RedirectToPage("/Index");

        try
        {
            var placed = await orders.PlaceOrderAsync(Form.ToCommand(), ct);
            return RedirectToPage("Confirmation", new { code = placed.Code });
        }
        catch (DomainException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }
}
