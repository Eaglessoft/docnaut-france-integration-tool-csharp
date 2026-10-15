using Docnaut.FranceConnector.Sdk;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var root = FindRoot();
var calls = new ConcurrentQueue<Call>();
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var fixture = builder.Build();
fixture.MapPost("/{environment}/token", async (HttpContext context, string environment) =>
{
    var form = await context.Request.ReadFormAsync();
    Check(form["grant_type"] == "client_credentials", "OAuth uses client credentials");
    if (form["client_id"] != "test-client" || form["client_secret"] != "test-secret")
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "invalid_client" });
        return;
    }
    await context.Response.WriteAsJsonAsync(new { access_token = environment + "-access-token",
        token_type = "Bearer", expires_in = 300 });
});
fixture.Map("/{environment}/client/api/v1/{**path}", async (HttpContext context) =>
{
    var environment = context.Request.RouteValues["environment"]!.ToString()!;
    var path = context.Request.RouteValues["path"]?.ToString() ?? "";
    if (context.Request.Headers.Authorization != "Bearer " + environment + "-access-token")
    {
        context.Response.StatusCode = 401;
        return;
    }
    var body = "";
    var fileName = "";
    var fileContentType = "";
    if (context.Request.HasFormContentType)
    {
        var form = await context.Request.ReadFormAsync();
        body = form["metadata"].ToString();
        var file = form.Files.FirstOrDefault();
        if (file is not null)
        {
            fileName = file.FileName;
            fileContentType = file.ContentType;
            using var reader = new StreamReader(file.OpenReadStream());
            body += await reader.ReadToEndAsync();
        }
    }
    else body = await new StreamReader(context.Request.Body).ReadToEndAsync();
    calls.Enqueue(new Call(environment, context.Request.Method, path, body,
        context.Request.Headers["Idempotency-Key"].ToString(), fileName, fileContentType));
    if (path == "documents/00000000-0000-4000-8000-000000000099")
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { code = "FC-REQUEST-1001", message = "Fixture validation error" });
    }
    else if (path.EndsWith("/access"))
    {
        context.Response.ContentType = "application/xml";
        context.Response.Headers.ContentDisposition = "attachment; filename=\"../invoice.xml\"";
        await context.Response.WriteAsync("<invoice>downloaded</invoice>");
    }
    else
    {
        context.Response.StatusCode = context.Request.Method == "POST" ? 201 : 200;
        await context.Response.WriteAsJsonAsync(new { fixtureEnvironment = environment, value = "fixture-ok",
            items = Array.Empty<object>() });
    }
});
await fixture.StartAsync();
var address = fixture.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()!.Addresses.Single();
var serveBrowser = args.Contains("--serve-browser");
var contextPath = args.SingleOrDefault(value => value.StartsWith("--context-path=", StringComparison.Ordinal))
    ?["--context-path=".Length..].TrimEnd('/') ?? "";
var appPort = serveBrowser ? 8083 : FreePort();
var appUrl = $"http://127.0.0.1:{appPort}";
#if DEBUG
const string configuration = "Debug";
#else
const string configuration = "Release";
#endif
var appDll = Path.GetFullPath(args.FirstOrDefault(value => !value.StartsWith("--")) ?? Path.Combine(root,
    $"Docnaut.FranceConnector.TestTool/bin/{configuration}/net10.0/Docnaut.FranceConnector.TestTool.dll"));
Check(File.Exists(appDll), "Build the TestTool before running the feature journey");
var start = new ProcessStartInfo(Environment.ProcessPath is { } processPath &&
    Path.GetFileNameWithoutExtension(processPath) == "dotnet" ? processPath :
    Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"))
{
    WorkingDirectory = Path.GetDirectoryName(appDll)!, UseShellExecute = false,
    RedirectStandardOutput = true, RedirectStandardError = true
};
start.ArgumentList.Add(appDll);
start.ArgumentList.Add("--urls");
start.ArgumentList.Add(serveBrowser ? "http://0.0.0.0:8083" : appUrl);
start.Environment["DOTNET_ENVIRONMENT"] = "Development";
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
start.Environment["DOCNAUT_CONTEXT_PATH"] = contextPath;
foreach (var environment in new[] { "Sandbox", "Production" })
{
    start.Environment[$"DOCNAUT_{environment.ToUpperInvariant()}_API_BASE_URL"] = $"{address}/{environment}/client/api/v1/";
    start.Environment[$"DOCNAUT_{environment.ToUpperInvariant()}_TOKEN_URL"] = $"{address}/{environment}/token";
    start.Environment[$"DOCNAUT_{environment.ToUpperInvariant()}_ALLOW_INSECURE_HTTP"] = "true";
}
using var app = Process.Start(start) ?? throw new InvalidOperationException("TestTool did not start");
var output = new StringBuilder();
app.OutputDataReceived += (_, e) => { if (e.Data is not null) lock(output) output.AppendLine(e.Data); };
app.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock(output) output.AppendLine(e.Data); };
app.BeginOutputReadLine(); app.BeginErrorReadLine();
using var browser = new HttpClient(new HttpClientHandler
{
    CookieContainer = new CookieContainer(), AllowAutoRedirect = false
}) { BaseAddress = new Uri(appUrl), Timeout = TimeSpan.FromSeconds(20) };
try
{
    await WaitForApp(browser, app, contextPath);
    if (serveBrowser)
    {
        Console.WriteLine("Browser fixture: http://localhost:8083 — Sandbox or Production; demo client test-client / test-secret (local fake credentials only).");
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
        try { await Task.Delay(Timeout.Infinite, stop.Token); } catch (OperationCanceledException) { }
        return;
    }
    using (var anonymous = await browser.GetAsync(AppPath("/")))
    {
        CheckAppRedirect(anonymous, "Anonymous access redirects within the application context");
        Check(new Uri(browser.BaseAddress!, anonymous.Headers.Location!).AbsolutePath == AppPath("/Login"),
            "Anonymous redirect retains the context path");
    }
    var login = await Page("/Login");
    foreach (var asset in new[] { "/bootstrap.min.css", "/site.css", "/docnaut-logo.webp" })
    {
        Check(login.Contains($"\"{AppPath(asset)}\"", StringComparison.Ordinal), $"Login references context asset {asset}");
        using var response = await browser.GetAsync(AppPath(asset));
        Check(response.IsSuccessStatusCode, $"Context asset {asset} is served");
        Check(response.Content.Headers.ContentType?.MediaType == (asset.EndsWith(".css") ? "text/css" : "image/webp"),
            $"Context asset {asset} has the expected content type");
    }
    Check(!login.Contains("test-secret"), "Login page does not contain a secret");
    using (var denied = await browser.PostAsync(AppPath("/Login"), new FormUrlEncodedContent(new Dictionary<string,string>
        { ["Environment"]="Sandbox", ["ClientId"]="test-client", ["ClientSecret"]="test-secret" })))
        Check(denied.StatusCode == HttpStatusCode.BadRequest, "Login requires antiforgery");
    using (var invalid = await Post("/Login", login, new()
        { ["Environment"]="Sandbox", ["ClientId"]="test-client", ["ClientSecret"]="wrong-secret" }))
    {
        var invalidPage = await invalid.Content.ReadAsStringAsync();
        Check(invalid.StatusCode == HttpStatusCode.OK, "Invalid credentials remain on login");
        Check(!invalidPage.Contains("wrong-secret"), "Rejected secret is not redisplayed");
    }
    login = await Page("/Login");
    using (var accepted = await Post("/Login", login, new()
        { ["Environment"]="Sandbox", ["ClientId"]="test-client", ["ClientSecret"]="test-secret" }))
    {
        CheckAppRedirect(accepted, "Valid credentials start a server session");
        var sessionCookie = accepted.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("Docnaut.TestTool.Session=", StringComparison.Ordinal));
        var cookiePath = Regex.Match(sessionCookie, @"(?:^|;\s*)path=([^;]+)", RegexOptions.IgnoreCase);
        Check(cookiePath.Success && cookiePath.Groups[1].Value == (contextPath.Length > 0 ? contextPath : "/"),
            "Session cookie is scoped to the configured context");
    }

    var index = await Page("/");
    Check(!index.Contains("test-secret") && !index.Contains("Sandbox-access-token"), "Session credentials stay off HTML");
    var expectedOperations = Regex.Matches(await File.ReadAllTextAsync(Path.Combine(root,
        "contracts/client-api-v1.yaml")), @"operationId:\s*(\w+)")
        .Select(m => m.Groups[1].Value).Where(id => id is not "generateProvisionedSecret" and not "getLiveness" and not "getReadiness").ToArray();
    Check(expectedOperations.ToHashSet().SetEquals(ApiOperation.All.Select(operation => operation.Id)), "SDK catalogue exactly covers authenticated contract operations");
    foreach (var operation in expectedOperations)
        Check(index.Contains(operation, StringComparison.Ordinal), $"UI includes {operation}");
    var before = calls.Count;
    using (var denied = await browser.PostAsync(AppPath("/?handler=Execute"), new FormUrlEncodedContent(
        new Dictionary<string,string> { ["Input.OperationId"]="replaceClientConfiguration", ["Input.Body"]="{\"preference\":true}" })))
        Check(denied.StatusCode == HttpStatusCode.BadRequest, "Mutations require antiforgery");
    Check(calls.Count == before, "CSRF rejection cannot reach Connector");
    using (var result = await Post("/?handler=Execute", index, new()
        { ["Input.OperationId"]="replaceClientConfiguration", ["Input.Body"]="{\"preference\":true}", ["Input.IfMatch"]="\"4\"" }))
        Check(result.StatusCode == HttpStatusCode.OK, "JSON operation renders response");
    Check(calls.Last() is { Environment: "Sandbox", Method: "PUT", Path: "configuration", Body: "{\"preference\":true}" },
        "Selected sandbox and exact JSON reach API");
    index = await Page("/");
    Check(!index.Contains("registerInvoicePayment"), "Removed payment operation is absent from the console");
    before = calls.Count;
    using (var removed = await Post("/?handler=Execute", index, new()
        { ["Input.OperationId"]="registerInvoicePayment", ["Input.Path[documentId]"]="00000000-0000-4000-8000-000000000001",
          ["Input.Body"]="{}", ["Input.IdempotencyKey"]="obsolete-payment" }))
        Check((await removed.Content.ReadAsStringAsync()).Contains("Choose an available operation."),
            "Stale payment forms show an unsupported-operation error");
    Check(calls.Count == before, "Stale payment forms never reach the removed API");
    var cdar = await File.ReadAllTextAsync(Path.Combine(root, "Docnaut.FranceConnector.FeatureTests/Fixtures/cdar-212.xml"));
    using (var multipart = new MultipartFormDataContent())
    {
        multipart.Add(new StringContent(Token(index)), "__RequestVerificationToken");
        multipart.Add(new StringContent("createDocumentIntake"), "Input.OperationId");
        multipart.Add(new StringContent("feature-payment-1"), "Input.IdempotencyKey");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(cdar));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        multipart.Add(file, "Input.Upload", "cdar-212.xml");
        using var result = await browser.PostAsync(AppPath("/?handler=Execute"), multipart);
        Check(result.StatusCode == HttpStatusCode.OK, "CDAR collection upload response is shown");
    }
    Check(calls.Last() is { Method: "POST", Path: "documents", FileName: "cdar-212.xml",
        FileContentType: "application/xml", IdempotencyKey: "feature-payment-1" } collection
        && collection.Body == cdar, "Original CDAR 212 bytes and idempotency key reach document intake");
    index = await Page("/");
    using (var result = await Post("/?handler=Execute", index, new()
        { ["Input.OperationId"]="listDocumentLifecycleEvents", ["Input.Path[documentTraceId]"]="00000000-0000-4000-8000-000000000001" }))
        Check(result.StatusCode == HttpStatusCode.OK, "Lifecycle history response is shown");
    Check(calls.Last() is { Method: "GET", Path: "documents/00000000-0000-4000-8000-000000000001/lifecycle-events", Body: "", IdempotencyKey: "" },
        "Lifecycle history uses the public document trace ID without mutation headers");
    index = await Page("/");
    using (var result = await Post("/?handler=Execute", index, new()
        { ["Input.OperationId"]="getDocument", ["Input.Path[documentTraceId]"]="00000000-0000-4000-8000-000000000099" }))
    {
        var page = await result.Content.ReadAsStringAsync();
        Check(page.Contains("FC-REQUEST-1001") && page.Contains("Fixture validation error"), "API error remains inspectable");
    }
    index = await Page("/");
    using (var multipart = new MultipartFormDataContent())
    {
        multipart.Add(new StringContent(Token(index)), "__RequestVerificationToken");
        multipart.Add(new StringContent("createDocumentIntake"), "Input.OperationId");
        multipart.Add(new StringContent("feature-upload-1"), "Input.IdempotencyKey");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("<invoice>uploaded</invoice>"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        multipart.Add(file, "Input.Upload", "invoice.xml");
        using var result = await browser.PostAsync(AppPath("/?handler=Execute"), multipart);
        Check(result.StatusCode == HttpStatusCode.OK, "Upload renders response");
    }
    Check(calls.Last() is { Path: "documents", FileName: "invoice.xml", IdempotencyKey: "feature-upload-1" } upload
        && upload.Body.Contains("<invoice>uploaded</invoice>"), "Multipart reaches API intact");
    index = await Page("/");
    using (var multipart = new MultipartFormDataContent())
    {
        multipart.Add(new StringContent(Token(index)), "__RequestVerificationToken");
        multipart.Add(new StringContent("submitReportingTransmission"), "Input.OperationId");
        multipart.Add(new StringContent("2026-10"), "Input.Path[period]");
        multipart.Add(new StringContent("{\"category\":\"SALES\",\"submissionType\":\"INITIAL\"}"), "Input.Body");
        multipart.Add(new StringContent("feature-f10-1"), "Input.IdempotencyKey");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("<report>sales</report>"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        multipart.Add(file, "Input.Upload", "report.xml");
        using var result = await browser.PostAsync(AppPath("/?handler=Execute"), multipart);
        Check(result.StatusCode == HttpStatusCode.OK, "Browser XML report upload renders response");
    }
    Check(calls.Last() is { Path: "reporting-periods/2026-10/transmissions", FileName: "report.xml",
        IdempotencyKey: "feature-f10-1", FileContentType: "application/xml" } reporting
        && reporting.Body.Contains("<report>sales</report>")
        && reporting.Body.Contains("\"category\":\"SALES\""),
        "F10 preserves XML and JSON metadata and normalizes browser text/xml");
    index = await Page("/");
    using (var result = await Post("/?handler=Execute", index, new()
        { ["Input.OperationId"]="createArtifactAccess", ["Input.Path[documentTraceId]"]="00000000-0000-4000-8000-000000000001", ["Input.Path[artifactId]"]="00000000-0000-4000-8000-000000000002" }))
    {
        Check(result.IsSuccessStatusCode, "Artifact access succeeds");
        Check(result.Content.Headers.ContentType?.MediaType == "application/xml", "Artifact MIME type is preserved");
        var disposition = result.Content.Headers.ContentDisposition;
        Check(disposition?.DispositionType == "attachment"
            && (disposition.FileNameStar ?? disposition.FileName)?.Trim('"') == "invoice.xml",
            "Artifact download preserves the safe filename and strips directory components");
        Check(await result.Content.ReadAsStringAsync() == "<invoice>downloaded</invoice>", "Artifact bytes reach the user");
    }
    index = await Page("/");
    using (var logout = await Post("/?handler=Logout", index, new()))
        CheckAppRedirect(logout, "Logout redirects");
    using (var afterLogout = await browser.GetAsync(AppPath("/")))
        CheckAppRedirect(afterLogout, "Logout closes API session");
    login = await Page("/Login");
    using (var accepted = await Post("/Login", login, new()
        { ["Environment"]="Production", ["ClientId"]="test-client", ["ClientSecret"]="test-secret" }))
        CheckAppRedirect(accepted, "Production login starts separate configured session");
    index = await Page("/");
    using (var result = await Post("/?handler=Execute", index, new() { ["Input.OperationId"]="listDocuments" }))
        Check(result.IsSuccessStatusCode, "Production operation succeeds");
    Check(calls.Last().Environment == "Production", "Environment selection routes to configured production fixture");
    lock(output) Check(!output.ToString().Contains("test-secret") && !output.ToString().Contains("wrong-secret")
        && !output.ToString().Contains("-access-token"), "Application logs do not contain credentials");
    Console.WriteLine($"PASS: client feature HTTP journey at context '{contextPath}' (assets, navigation, cookie scope, login, environments, CSRF, catalogue, JSON, idempotency, errors, upload, download, logout).");
}
finally
{
    if (!app.HasExited) app.Kill(entireProcessTree: true);
    await app.WaitForExitAsync();
    await fixture.StopAsync();
}
async Task<string> Page(string path)
{
    using var response = await browser.GetAsync(AppPath(path));
    Check(response.IsSuccessStatusCode, $"GET {path} succeeds");
    var html = await response.Content.ReadAsStringAsync();
    foreach (Match match in Regex.Matches(html, "(?:href|src|action)=\"([^\"]*)\""))
    {
        var value = WebUtility.HtmlDecode(match.Groups[1].Value);
        if (value.Length > 0 && !value.StartsWith('#')) CheckContextUrl(new Uri(browser.BaseAddress!, value));
    }
    return html;
}
Task<HttpResponseMessage> Post(string path, string html, Dictionary<string,string> form)
{
    form["__RequestVerificationToken"] = Token(html);
    return browser.PostAsync(AppPath(path), new FormUrlEncodedContent(form));
}
string AppPath(string path) => contextPath + path;
void CheckContextUrl(Uri url)
{
    Check(url.GetLeftPart(UriPartial.Authority) == browser.BaseAddress!.GetLeftPart(UriPartial.Authority),
        "Generated navigation stays on the application origin");
    Check(url.AbsolutePath == contextPath || url.AbsolutePath.StartsWith(contextPath + "/", StringComparison.Ordinal),
        "Generated navigation retains the configured context path");
}
void CheckAppRedirect(HttpResponseMessage response, string message)
{
    Check(IsRedirect(response) && response.Headers.Location is not null, message);
    CheckContextUrl(new Uri(browser.BaseAddress!, response.Headers.Location!));
}
static string Token(string html)
{
    var match = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    Check(match.Success, "Rendered form contains antiforgery token");
    return WebUtility.HtmlDecode(match.Groups[1].Value);
}
static bool IsRedirect(HttpResponseMessage response) => (int)response.StatusCode is 302 or 303;
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static int FreePort()
{
    var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
    int port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop(); return port;
}
static async Task WaitForApp(HttpClient http, Process app, string contextPath)
{
    var limit = DateTime.UtcNow.AddSeconds(30);
    while (DateTime.UtcNow < limit)
    {
        Check(!app.HasExited, "TestTool exited during startup");
        try { using var response = await http.GetAsync(contextPath + "/Login"); if (response.IsSuccessStatusCode) return; }
        catch (HttpRequestException) { }
        await Task.Delay(100);
    }
    throw new TimeoutException("TestTool startup timed out");
}
static string FindRoot()
{
    for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
        if (File.Exists(Path.Combine(path.FullName, "contracts/client-api-v1.yaml"))) return path.FullName;
    throw new InvalidOperationException("Contract snapshot must be included beside the client projects");
}
record Call(string Environment, string Method, string Path, string Body, string IdempotencyKey, string FileName, string FileContentType);
