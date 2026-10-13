using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Docnaut.FranceConnector.Sdk;

/// <summary>
/// Dependency-free Client API transport with cached OAuth credentials, streamed files and no automatic retries.
/// Reuse one instance per service client. Dispose it when that client session ends.
/// </summary>
public sealed class ConnectorClient : IDisposable
{
    private const int MaxTextBytes = 2 * 1024 * 1024;
    private const int MaxJsonBytes = 1024 * 1024;
    private readonly HttpClient http;
    private readonly Uri apiBase;
    private readonly Uri tokenUrl;
    private readonly string clientId;
    private string clientSecret;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? token;
    private DateTimeOffset renewAfter;
    private bool disposed;

    /// <summary>Creates a client for fixed API and OAuth endpoints; no credentials are logged.</summary>
    public ConnectorClient(ConnectorEndpoint endpoint, string clientId, string clientSecret)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        apiBase = ValidateEndpoint(endpoint.ApiBaseUrl, endpoint.AllowInsecureHttp);
        tokenUrl = ValidateEndpoint(endpoint.TokenUrl, endpoint.AllowInsecureHttp);
        if (!apiBase.AbsolutePath.EndsWith("/client/api/v1/", StringComparison.Ordinal))
            throw new ConnectorException("API base URL must end with /client/api/v1/.");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            || clientId.Length > 4096 || clientSecret.Length > 4096)
            throw new ConnectorException("A service client ID and secret are required.");
        this.clientId = clientId; this.clientSecret = clientSecret;
        http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxResponseHeadersLength = 32
        }) { Timeout = TimeSpan.FromSeconds(120) };
    }

    /// <summary>Obtains or renews an OAuth client-credentials token without returning it to the caller.</summary>
    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (token is not null && DateTimeOffset.UtcNow < renewAfter) return;
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = clientSecret
            });
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new ConnectorException("OAuth authentication failed. Check the environment and service client credentials.",
                    (int)response.StatusCode, "authentication_failed");
            string text = await ReadTextAsync(response.Content, 64 * 1024, deadline.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("access_token", out var value) || value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString())
                || !root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out int seconds) || seconds <= 0
                || (root.TryGetProperty("token_type", out var type)
                    && (type.ValueKind != JsonValueKind.String || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase))))
                throw new ConnectorException("OAuth returned an invalid token response.", code: "authentication_failed");
            string accessToken = value.GetString()!;
            if (accessToken.Length > 32768 || !Regex.IsMatch(accessToken, "^[A-Za-z0-9._~+/-]+=*$"))
                throw new ConnectorException("OAuth returned an invalid token response.", code: "authentication_failed");
            token = accessToken;
            renewAfter = DateTimeOffset.UtcNow.AddSeconds(seconds - Math.Min(30, seconds / 2));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (OperationCanceledException)
        { throw new ConnectorException("OAuth authentication timed out.", code: "authentication_timeout"); }
        catch (Exception failure) when (failure is HttpRequestException or IOException or JsonException or InvalidOperationException)
        { throw new ConnectorException("OAuth authentication could not be completed.", code: "authentication_failed"); }
        finally { tokenLock.Release(); }
    }

    /// <summary>
    /// Executes one catalogued operation. HTTP API errors remain readable in the response.
    /// Uploads are read from their current position and remain caller-owned; dispose the returned response.
    /// </summary>
    public async Task<ApiResponse> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        ApiOperation operation = ApiOperation.Get(request.OperationId);
        Uri target = BuildTarget(operation, request);
        ValidateHeaders(operation, request);
        using var outgoing = new HttpRequestMessage(new HttpMethod(operation.Method), target);
        outgoing.Content = BuildContent(operation, request);
        await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        outgoing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        outgoing.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("D"));
        if (request.IdempotencyKey is not null) outgoing.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        if (request.IfMatch is not null) outgoing.Headers.Add("If-Match", request.IfMatch);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        HttpResponseMessage? response = null;
        try
        {
            response = await http.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (operation.BodyKind == BodyKind.Download && response.IsSuccessStatusCode)
            {
                Stream stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                var result = new ApiResponse(response, null, stream); response = null; return result;
            }
            string body = await ReadTextAsync(response.Content, MaxTextBytes, deadline.Token).ConfigureAwait(false);
            var textResult = new ApiResponse(response, string.IsNullOrWhiteSpace(body) ? null : body, null);
            response = null; return textResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (OperationCanceledException)
        { throw new ConnectorException("The API request timed out. Its remote outcome may be unknown; it was not retried.", code: "transport_timeout"); }
        catch (Exception failure) when (failure is HttpRequestException or IOException)
        { throw new ConnectorException("The API request could not be completed. Its remote outcome may be unknown; it was not retried.", code: "transport_failed"); }
        finally { response?.Dispose(); }
    }

    private static Uri ValidateEndpoint(string address, bool allowHttp)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || !(uri.Scheme == Uri.UriSchemeHttps || (allowHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            throw new ConnectorException("Use an absolute HTTPS endpoint without credentials, query or fragment. HTTP requires explicit loopback development mode.");
        return uri;
    }

    private Uri BuildTarget(ApiOperation operation, ApiRequest request)
    {
        var pathValues = request.Path ?? new Dictionary<string, string>();
        var queryValues = request.Query ?? new Dictionary<string, string>();
        if (pathValues.Count != operation.PathParameters.Count || pathValues.Keys.Any(k => !operation.PathParameters.Contains(k))
            || queryValues.Keys.Any(k => !operation.QueryParameters.Contains(k)))
            throw new ConnectorException("Only the operation's declared path and query parameters are allowed.");
        string path = operation.PathTemplate;
        foreach (string name in operation.PathParameters)
        {
            if (!pathValues.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
                throw new ConnectorException("Every operation path parameter is required.");
            if (name == "period")
            {
                if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    throw new ConnectorException("Reporting period must be a valid YYYY-MM month.");
            }
            else if (!Guid.TryParseExact(value, "D", out _))
                throw new ConnectorException("Document, report, artifact and subscription identifiers must be UUIDs.");
            path = path.Replace("{" + name + "}", Uri.EscapeDataString(value), StringComparison.Ordinal);
        }
        var query = new List<string>();
        foreach (var (name, value) in queryValues)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl))
                throw new ConnectorException("Query values must be nonempty and at most 4096 characters.");
            if (name is "limit" or "logPage")
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    || number < (name == "limit" ? 1 : 0) || number > (name == "limit" ? 100 : 10000))
                    throw new ConnectorException("The requested page size or log page is outside the API range.");
            }
            if (name == "subscriptionId" && !Guid.TryParseExact(value, "D", out _))
                throw new ConnectorException("The subscription identifier must be a UUID.");
            query.Add(Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value));
        }
        return new Uri(apiBase, path + (query.Count == 0 ? "" : "?" + string.Join("&", query)));
    }

    private static void ValidateHeaders(ApiOperation operation, ApiRequest request)
    {
        if (operation.RequiresIdempotencyKey && string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ConnectorException("This operation requires an Idempotency-Key.");
        if (request.IdempotencyKey is not null
            && (!operation.RequiresIdempotencyKey || request.IdempotencyKey.Length > 255
                || !Regex.IsMatch(request.IdempotencyKey, "^[A-Za-z0-9][A-Za-z0-9._:-]*$")))
            throw new ConnectorException("Invalid or unexpected Idempotency-Key.");
        if (operation.Id == "decideInboundInvoice" && !Guid.TryParseExact(request.IdempotencyKey, "D", out _))
            throw new ConnectorException("Invoice decisions require a UUID Idempotency-Key.");
        if (operation.RequiresIfMatch && string.IsNullOrWhiteSpace(request.IfMatch))
            throw new ConnectorException("This operation requires the If-Match ETag from a previous read.");
        if (request.IfMatch is not null
            && (!operation.RequiresIfMatch || request.IfMatch.Length > 30 || !Regex.IsMatch(request.IfMatch, "^\"[0-9]+\"$")))
            throw new ConnectorException("If-Match must be the quoted version ETag from a previous read.");
    }

    private static HttpContent? BuildContent(ApiOperation operation, ApiRequest request)
    {
        if (operation.BodyKind == BodyKind.Multipart)
        {
            bool reporting = operation.Id == "submitReportingTransmission";
            if (request.Upload is null || !request.Upload.CanRead)
                throw new ConnectorException("A readable upload stream is required.");
            string name = request.FileName ?? (reporting ? "report.xml" : "document.xml");
            string media = request.ContentType ?? "application/xml";
            if (name.Length is 0 or > 255 || name.Any(c => char.IsControl(c) || c is '/' or '\\' or '\"'))
                throw new ConnectorException("Use a filename without directory components or control characters.");
            if ((reporting && media != "application/xml") || (!reporting && media is not ("application/xml" or "text/xml" or "application/pdf")))
                throw new ConnectorException("Unsupported source content type for this operation.");
            long maximum = (reporting ? 10L : 100L) * 1024 * 1024;
            if (request.Upload.CanSeek && request.Upload.Length - request.Upload.Position > maximum)
                throw new ConnectorException(reporting ? "F10 XML must not exceed 10 MiB." : "Document uploads must not exceed 100 MiB.");
            HttpContent? metadata = reporting ? JsonContent(request.Body) : null;
            if (!reporting && request.Body is not null) throw new ConnectorException("Document uploads do not accept metadata JSON.");
            var multipart = new MultipartFormDataContent();
            var file = new LimitedUploadContent(request.Upload, maximum);
            file.Headers.ContentType = new MediaTypeHeaderValue(media);
            multipart.Add(file, "source", name);
            if (metadata is not null) multipart.Add(metadata, "metadata");
            return multipart;
        }
        if (request.Upload is not null || request.FileName is not null || request.ContentType is not null)
            throw new ConnectorException("File parameters are valid only for upload operations.");
        if (operation.BodyKind == BodyKind.Json) return JsonContent(request.Body);
        if (request.Body is not null) throw new ConnectorException("This operation does not accept a request body.");
        return null;
    }

    private static HttpContent JsonContent(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > MaxJsonBytes)
            throw new ConnectorException("A JSON object of at most 1 MiB is required.");
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object) throw new ConnectorException("The JSON body must be an object.");
        }
        catch (JsonException) { throw new ConnectorException("The request body is not valid JSON."); }
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    private static async Task<string> ReadTextAsync(HttpContent content, int maximum, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximum) throw new ConnectorException("The remote textual response exceeded the allowed size.", code: "response_too_large");
        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > maximum) throw new ConnectorException("The remote textual response exceeded the allowed size.", code: "response_too_large");
            buffer.Write(chunk, 0, count);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    /// <summary>Disposes connections and clears references to cached authentication data.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; token = null; clientSecret = ""; http.Dispose();
    }
}

internal sealed class LimitedUploadContent(Stream source, long maximum) : HttpContent
{
    protected override bool TryComputeLength(out long length)
    {
        length = source.CanSeek ? source.Length - source.Position : 0;
        return source.CanSeek;
    }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);
    protected override async Task SerializeToStreamAsync(Stream target, TransportContext? context, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920]; long total = 0; int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            total += count;
            if (total > maximum) throw new ConnectorException("The upload exceeded the operation's size limit.", code: "upload_too_large");
            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
    }
}
