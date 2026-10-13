using Docnaut.FranceConnector.TestTool.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Docnaut.FranceConnector.TestTool.Pages;

public sealed class LoginModel(ConnectorConsole console) : PageModel
{
    [BindProperty] public string Environment { get; set; } = "Sandbox";
    [BindProperty] public string ClientId { get; set; } = "";
    [BindProperty] public string ClientSecret { get; set; } = "";
    public string? Error { get; private set; }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Error = await console.LoginAsync(HttpContext.Session, Environment, ClientId, ClientSecret, cancellationToken);
        ClientSecret = ""; ClientId = "";
        ModelState.Clear();
        return Error == null ? RedirectToPage("/Index") : Page();
    }
}
