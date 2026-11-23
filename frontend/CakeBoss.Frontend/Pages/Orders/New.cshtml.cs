using System.Globalization;
using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Shared;
using CakeBoss.Frontend.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CakeBoss.Frontend.Pages.Orders;

/// <summary>Formulário público de pedido. Fino de propósito: toda regra fica no backend.</summary>
public class NewModel(OrderService orders, AvailabilityService availability, IOptions<SiteOptions> site) : PageModel
{
    /// <summary>Quantos dias à frente o calendário do formulário mostra.</summary>
    public const int CalendarDays = 60;

    [BindProperty]
    public OrderForm Form { get; set; } = new();

    public SiteOptions Site => site.Value;

    public bool Enabled => site.Value.OrdersEnabled;

    public void OnGet()
    {
    }

    /// <summary>
    /// Dias e horários livres para o peso informado (GET /pedido?handler=Slots&amp;kg=2,5), lidos pelo calendário do formulário.
    /// Só repassa o que o backend calcula; peso vazio ou inválido conta como 0 (mostra tudo o que está livre).
    /// </summary>
    public async Task<IActionResult> OnGetSlotsAsync(string? kg, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var weight = OrderForm.TryParseWeight(kg, out var parsed) && parsed > 0 ? parsed : 0;
        var from = availability.Today;
        var days = await availability.GetFreeDaysAsync(from, from.AddDays(CalendarDays), weight, ct);

        return new JsonResult(new
        {
            days = days.Select(d => new
            {
                date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                label = FormOptions.FormatDate(d.Date),
                times = d.Times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))
            })
        });
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
