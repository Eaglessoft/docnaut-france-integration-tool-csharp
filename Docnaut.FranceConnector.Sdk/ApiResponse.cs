using System.Collections.ObjectModel;

namespace Docnaut.FranceConnector.Sdk;

/// <summary>One API response. Dispose after use, including after copying a streamed download.</summary>
public sealed class ApiResponse : IDisposable, IAsyncDisposable
{
    private HttpResponseMessage? response;
    /// <summary>HTTP status from the Connector.</summary>
    public int StatusCode { get; }
    /// <summary>True for HTTP 2xx.</summary>
    public bool IsSuccess => StatusCode is >= 200 and < 300;
    /// <summary>Bounded textual body, including problem details; may contain one-time secrets. Never log it.</summary>
    public string? Body { get; }
    /// <summary>Selected response metadata; includes ETag, Location and correlation ID when present.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Successful artifact download, streamed without buffering. Null for other responses.</summary>
    public Stream? Content { get; }

    internal ApiResponse(HttpResponseMessage response, string? body, Stream? content)
    {
        this.response = response; StatusCode = (int)response.StatusCode; Body = body; Content = content;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in new[] { "X-Correlation-ID", "ETag", "Location", "Retry-After", "Cache-Control",
            "Content-Type", "Content-Length", "Content-Disposition" })
        {
            if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
                headers[name] = string.Join(", ", values);
        }
        Headers = new ReadOnlyDictionary<string, string>(headers);
    }

    /// <summary>Closes the download stream and underlying HTTP response.</summary>
    public void Dispose()
    {
        response?.Dispose(); response = null;
    }
    /// <summary>Closes the download stream and underlying HTTP response.</summary>
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    /// <summary>Returns status only, never response contents.</summary>
    public override string ToString() => $"ApiResponse {{ StatusCode = {StatusCode} }}";
}
