using Docnaut.FranceConnector.TestTool.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var contextPathValue = (builder.Configuration["DOCNAUT_CONTEXT_PATH"] ?? "").TrimEnd('/');
if (contextPathValue.Length > 0 && (!contextPathValue.StartsWith('/')
    || contextPathValue[1..].Split('/').Any(segment => segment.Length == 0
        || segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))))
    throw new InvalidOperationException("DOCNAUT_CONTEXT_PATH must be empty, '/', or slash-separated letters, digits, '-' and '_'.");
var contextPath = new PathString(contextPathValue);
builder.Services.AddRazorPages();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = "Docnaut.TestTool.Session";
    options.Cookie.Path = contextPath.HasValue ? contextPath.Value! : "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 101L * 1024 * 1024);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 101L * 1024 * 1024);
builder.Services.AddSingleton<ConnectorConsole>();
var app = builder.Build();
if (contextPath.HasValue) app.UsePathBase(contextPath);
app.UseExceptionHandler("/Error");
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();
app.Run();
