using Docnaut.FranceConnector.Sdk;
using Docnaut.FranceConnector.TestTool.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;

namespace Docnaut.FranceConnector.TestTool.Pages;

public sealed class IndexModel(ConnectorConsole console) : PageModel
{
    [BindProperty] public ConsoleInput Input { get; set; } = new();
    public IReadOnlyList<ApiOperation> Operations => console.Operations;
    public ApiOperation? Selected { get; private set; }
    public string? Environment { get; private set; }
    public string? Error { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; private set; }
    public IActionResult OnGet(string? operation)
    {
        Environment = console.Environment(HttpContext.Session);
        if (Environment == null) return RedirectToPage("/Login");
        Selected = console.Operation(operation) ?? Operations.FirstOrDefault();
        if (Selected != null) Input.OperationId = Selected.Id;
        return Page();
    }
    public async Task<IActionResult> OnPostExecuteAsync(CancellationToken cancellationToken)
    {
        Environment = console.Environment(HttpContext.Session);
        if (Environment == null) return RedirectToPage("/Login");
        Selected = console.Operation(Input.OperationId);
        var result = await console.ExecuteAsync(HttpContext.Session, Input, cancellationToken);
        Error = result.Error;
        if (result.Response is { } response)
        {
            if (response.IsSuccess && response.Content != null)
            {
                Response.OnCompleted(() => { response.Dispose(); return Task.CompletedTask; });
                string contentType = response.Headers.TryGetValue("Content-Type", out string? mediaType)
                    && MediaTypeHeaderValue.TryParse(mediaType, out var parsedType)
                    ? parsedType.ToString() : "application/octet-stream";
                string fileName = "artifact";
                if (response.Headers.TryGetValue("Content-Disposition", out string? disposition)
                    && ContentDispositionHeaderValue.TryParse(disposition, out var parsedDisposition))
                {
                    string? candidate = parsedDisposition.FileNameStar.HasValue
                        ? parsedDisposition.FileNameStar.Value : parsedDisposition.FileName.Value;
                    candidate = candidate?.Trim('"').Replace('\\', '/').Split('/').LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(candidate) && candidate is not "." and not ".."
                        && candidate.Length <= 200 && !candidate.Any(char.IsControl))
                        fileName = candidate;
                }
                return File(response.Content, contentType, fileName);
            }
            using (response)
            {
                ResponseStatusCode = response.StatusCode; ResponseBody = response.Body; ResponseHeaders = response.Headers;
            }
        }
        Input.Body = null; Input.Upload = null;
        ModelState.Clear();
        return Page();
    }
    public IActionResult OnPostLogout()
    {
        console.Logout(HttpContext.Session);
        return RedirectToPage("/Login");
    }
}
