namespace Docnaut.FranceConnector.Sdk;

/// <summary>Wire body expected by an operation.</summary>
public enum BodyKind
{
    /// <summary>No request body.</summary>
    None,
    /// <summary>JSON request body.</summary>
    Json,
    /// <summary>XML request body.</summary>
    Xml,
    /// <summary>A source file and, for F10, JSON metadata.</summary>
    Multipart,
    /// <summary>No request body; the successful response is streamed.</summary>
    Download
}

/// <summary>The fixed, supported Client API catalogue. Instances cannot specify arbitrary routes.</summary>
public sealed class ApiOperation
{
    /// <summary>OpenAPI operation identifier.</summary>
    public string Id { get; }
    /// <summary>Human-readable operation label.</summary>
    public string Label { get; }
    /// <summary>Functional group.</summary>
    public string Group { get; }
    /// <summary>HTTP method.</summary>
    public string Method { get; }
    /// <summary>Path relative to the Client API base, without a leading slash.</summary>
    public string PathTemplate { get; }
    /// <summary>Request and response transfer shape.</summary>
    public BodyKind BodyKind { get; }
    /// <summary>Required path variable names.</summary>
    public IReadOnlyList<string> PathParameters { get; }
    /// <summary>Allowed optional query variable names.</summary>
    public IReadOnlyList<string> QueryParameters { get; }
    /// <summary>Whether the caller must provide an idempotency key.</summary>
    public bool RequiresIdempotencyKey { get; }
    /// <summary>Whether the caller must provide the previous response ETag.</summary>
    public bool RequiresIfMatch { get; }

    private ApiOperation(string id, string label, string group, string method, string path,
        BodyKind body = BodyKind.None, string[]? query = null, bool key = false, bool match = false)
    {
        Id = id; Label = label; Group = group; Method = method; PathTemplate = path; BodyKind = body;
        PathParameters = Array.AsReadOnly(path.Split('/').Where(p => p.StartsWith('{'))
            .Select(p => p[1..^1]).ToArray());
        QueryParameters = Array.AsReadOnly(query ?? []);
        RequiresIdempotencyKey = key; RequiresIfMatch = match;
    }

    /// <summary>All supported authenticated business operations, in display order.</summary>
    public static IReadOnlyList<ApiOperation> All { get; } = Array.AsReadOnly(new ApiOperation[]
    {
        new("getClientProfile", "Read profile", "Account", "GET", "profile"),
        new("getClientConfiguration", "Read preferences", "Account", "GET", "configuration"),
        new("replaceClientConfiguration", "Replace preferences", "Account", "PUT", "configuration", BodyKind.Json, match: true),
        new("rotateOwnServiceClientSecret", "Rotate service client secret", "Account", "POST", "service-client/rotate-secret", BodyKind.Json),
        new("createDocumentIntake", "Submit document", "Documents", "POST", "documents", BodyKind.Multipart, key: true),
        new("listDocuments", "List documents", "Documents", "GET", "documents", query: ["cursor", "limit"]),
        new("getDocument", "Read document", "Documents", "GET", "documents/{documentTraceId}"),
        new("getDocumentProcessing", "Read processing and delivery", "Documents", "GET", "documents/{documentTraceId}/processing", query: ["logPage"]),
        new("listDocumentEvents", "List document events", "Documents", "GET", "documents/{documentTraceId}/events", query: ["cursor", "limit"]),
        new("listDocumentArtifacts", "List document files", "Documents", "GET", "documents/{documentTraceId}/artifacts", query: ["cursor", "limit"]),
        new("createArtifactAccess", "Download document file", "Documents", "POST", "documents/{documentTraceId}/artifacts/{artifactId}/access", BodyKind.Download),
        new("decideInboundInvoice", "Record invoice decision", "Decisions", "POST", "documents/{documentId}/decision", BodyKind.Json, key: true),
        new("getInboundInvoiceDecision", "Read invoice decision", "Decisions", "GET", "documents/{documentId}/decision"),
        new("registerInvoicePayment", "Record invoice payment", "Reports", "POST", "documents/{documentId}/payments", BodyKind.Json, key: true),
        new("submitReportingTransmission", "Submit F10 transmission", "Reports", "POST", "reporting-periods/{period}/transmissions", BodyKind.Multipart, key: true),
        new("getReport", "Read report", "Reports", "GET", "reports/{reportId}"),
        new("getReportReceipts", "Read report receipts", "Reports", "GET", "reports/{reportId}/receipts"),
        new("listPeppolFacts", "List Peppol facts", "Documents", "GET", "peppol/facts", query: ["cursor", "limit"]),
        new("listClientWebhookSubscriptions", "List subscriptions", "Webhooks", "GET", "webhook-subscriptions", query: ["cursor", "limit"]),
        new("createClientWebhookSubscription", "Create subscription", "Webhooks", "POST", "webhook-subscriptions", BodyKind.Json, key: true),
        new("getClientWebhookSubscription", "Read subscription", "Webhooks", "GET", "webhook-subscriptions/{subscriptionId}"),
        new("updateClientWebhookSubscription", "Update subscription", "Webhooks", "PATCH", "webhook-subscriptions/{subscriptionId}", BodyKind.Json, match: true),
        new("verifyClientWebhookSubscription", "Verify endpoint", "Webhooks", "POST", "webhook-subscriptions/{subscriptionId}/verify", key: true),
        new("rotateClientWebhookSecret", "Rotate webhook secret", "Webhooks", "POST", "webhook-subscriptions/{subscriptionId}/rotate-secret", BodyKind.Json),
        new("listClientWebhookDeliveries", "List webhook deliveries", "Webhooks", "GET", "webhook-deliveries", query: ["subscriptionId", "cursor", "limit"]),
        new("getClientWebhookDelivery", "Read webhook delivery", "Webhooks", "GET", "webhook-deliveries/{deliveryId}")
    });

    /// <summary>Looks up an operation, rejecting unsupported identifiers.</summary>
    public static ApiOperation Get(string id) => All.FirstOrDefault(o => o.Id == id)
        ?? throw new ConnectorException("Unsupported Client API operation.");
}
