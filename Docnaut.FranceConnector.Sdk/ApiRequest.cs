namespace Docnaut.FranceConnector.Sdk;

/// <summary>Connection addresses. HTTP is allowed only for explicit loopback development.</summary>
/// <param name="ApiBaseUrl">Absolute URL ending in /client/api/v1/.</param>
/// <param name="TokenUrl">Absolute OAuth client-credentials token URL.</param>
/// <param name="AllowInsecureHttp">Permit HTTP only when the URL host is loopback.</param>
public sealed record ConnectorEndpoint(string ApiBaseUrl, string TokenUrl, bool AllowInsecureHttp = false)
{
    /// <summary>Returns a description without connection addresses.</summary>
    public override string ToString() => nameof(ConnectorEndpoint);
}

/// <summary>Values for one whitelisted operation. Upload streams remain caller-owned.</summary>
/// <param name="OperationId">An identifier from <see cref="ApiOperation.All"/>.</param>
public sealed record ApiRequest(string OperationId)
{
    /// <summary>Required route variables; documentId denotes the public document trace ID.</summary>
    public IReadOnlyDictionary<string, string>? Path { get; init; }
    /// <summary>Allowed query values from the operation catalogue.</summary>
    public IReadOnlyDictionary<string, string>? Query { get; init; }
    /// <summary>JSON body, or JSON metadata for an F10 multipart submission.</summary>
    public string? Body { get; init; }
    /// <summary>Upload filename without directory components.</summary>
    public string? FileName { get; init; }
    /// <summary>Upload MIME type: application/xml, text/xml or application/pdf.</summary>
    public string? ContentType { get; init; }
    /// <summary>Caller-generated key, retained unchanged for an intentional retry.</summary>
    public string? IdempotencyKey { get; init; }
    /// <summary>Quoted version ETag returned by the previous read.</summary>
    public string? IfMatch { get; init; }
    /// <summary>Readable source stream; the SDK does not rewind or dispose it.</summary>
    public Stream? Upload { get; init; }
    /// <summary>Returns only the operation name, without request data.</summary>
    public override string ToString() => $"ApiRequest {{ OperationId = {ApiOperation.Get(OperationId).Id} }}";
}

/// <summary>Safe SDK validation, authentication or transport failure. HTTP API errors are returned as responses.</summary>
public sealed class ConnectorException : Exception
{
    /// <summary>HTTP status when available.</summary>
    public int? StatusCode { get; }
    /// <summary>Stable SDK error category.</summary>
    public string? Code { get; }
    /// <summary>Safe correlation identifier, if one is available.</summary>
    public string? CorrelationId { get; }
    internal ConnectorException(string message, int? statusCode = null, string? code = null, string? correlationId = null)
        : base(message)
    {
        StatusCode = statusCode; Code = code; CorrelationId = correlationId;
    }
}
