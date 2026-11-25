using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CakeBoss.Frontend.Pages.Panel.Orders;

/// <summary>
/// Um pedido aberto pela dona: contato, o que o cliente pediu, valores, o botão que chama o cliente no WhatsApp e as ações
/// do ciclo (aprovar, recusar, confirmar o sinal, concluir, cancelar). Fina de propósito: as regras estão no backend.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetailsModel(OrderManagementService orders) : PageModel
{
    public const string MessageKey = "PanelMessage";

    public OrderDetails? Order { get; private set; }

    /// <summary>Aviso de que a ação deu certo; vem do redirecionamento depois do POST.</summary>
    public string? Message { get; private set; }

    [BindProperty]
    public ApproveForm Approve { get; set; } = new();

    [BindProperty]
    public ReasonForm Reject { get; set; } = new();

    [BindProperty]
    public CancelForm Cancel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Message = TempData[MessageKey] as string;
        return await ShowAsync(id, ct);
    }

    public Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        if (!Approve.TryToCommand(id, out var command, out var error))
        {
            ModelState.AddModelError(string.Empty, error!);
            return ShowAsync(id, ct);
        }

        return RunAsync(id, "Pedido aprovado. Agora é esperar o sinal.", () => orders.ApproveAsync(command!, ct), ct);
    }

    public Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct) =>
        RunAsync(id, "Pedido recusado.", () => orders.RejectAsync(id, Reject.Reason, ct), ct);

    public Task<IActionResult> OnPostConfirmDepositAsync(Guid id, CancellationToken ct) =>
        RunAsync(id, "Sinal confirmado. O pedido está garantido na agenda.", () => orders.ConfirmDepositAsync(id, ct), ct);

    public Task<IActionResult> OnPostCompleteAsync(Guid id, CancellationToken ct) =>
        RunAsync(id, "Pedido concluído.", () => orders.CompleteAsync(id, ct), ct);

    public Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct) =>
        RunAsync(id, "Pedido cancelado. O horário voltou a ficar livre.", () => orders.CancelAsync(id, Cancel.Reason, Cancel.KeepDeposit, ct), ct);

    /// <summary>
    /// Faz a ação. Se o backend recusar (status errado, horário tomado...), mostra o motivo na mesma tela; se der certo,
    /// volta ao detalhe (POST/redirect/GET) para que recarregar a página não repita a ação.
    /// </summary>
    private async Task<IActionResult> RunAsync(Guid id, string success, Func<Task> action, CancellationToken ct)
    {
        try
        {
            await action();
        }
        catch (DomainException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ShowAsync(id, ct);
        }

        TempData[MessageKey] = success;
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> ShowAsync(Guid id, CancellationToken ct)
    {
        Order = await orders.GetAsync(id, ct);
        return Order is null ? NotFound() : Page();
    }

    public sealed class ReasonForm
    {
        public string? Reason { get; set; }
    }

    public sealed class CancelForm
    {
        public string? Reason { get; set; }

        /// <summary>A dona fica com o sinal (só vale se ele já foi pago; o backend confere).</summary>
        public bool KeepDeposit { get; set; }
    }
}
