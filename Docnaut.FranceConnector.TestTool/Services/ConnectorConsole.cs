using Docnaut.FranceConnector.Sdk;
using Microsoft.Extensions.Caching.Memory;

namespace Docnaut.FranceConnector.TestTool.Services;

public sealed class ConnectorConsole(IMemoryCache cache, IConfiguration configuration)
{
    private const string SessionKey = "connection";
    public IReadOnlyList<ApiOperation> Operations => ApiOperation.All;
    public string? Environment(ISession session) => State(session)?.Environment;
    public ApiOperation? Operation(string? id) => Operations.FirstOrDefault(operation => operation.Id == id);

    public async Task<string?> LoginAsync(ISession session, string environment, string clientId,
        string clientSecret, CancellationToken cancellationToken)
    {
        Logout(session);
        if (environment is not ("Sandbox" or "Production") || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)) return "Select an environment and enter both credentials.";
        string prefix = "DOCNAUT_" + environment.ToUpperInvariant() + "_";
        ConnectorClient? client = null;
        try
        {
            client = new ConnectorClient(new ConnectorEndpoint(
                configuration[prefix + "API_BASE_URL"] ?? "", configuration[prefix + "TOKEN_URL"] ?? "",
                configuration.GetValue<bool>(prefix + "ALLOW_INSECURE_HTTP")), clientId, clientSecret);
            await client.AuthenticateAsync(cancellationToken);
            string id = Guid.NewGuid().ToString("N");
            var state = new Connection(environment, client);
            cache.Set(id, state, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .RegisterPostEvictionCallback((_, value, _, _) =>
                {
                    if (value is Connection expired) _ = expired.CloseAsync();
                }));
            session.SetString(SessionKey, id);
            return null;
        }
        catch (ConnectorException error) { client?.Dispose(); return error.Message; }
        catch (Exception error) when (error is ArgumentException or HttpRequestException or TaskCanceledException)
        { client?.Dispose(); return "Authentication could not be completed. Check the selected environment and credentials."; }
    }

    public void Logout(ISession session)
    {
        string? id = session.GetString(SessionKey);
        session.Clear();
        if (id != null) cache.Remove(id);
    }

    public async Task<ConsoleResult> ExecuteAsync(ISession session, ConsoleInput input, CancellationToken cancellationToken)
    {
        var state = State(session);
        if (state == null) return new(null, "Your session has expired. Sign in again.");
        var operation = Operation(input.OperationId);
        if (operation == null) return new(null, "Choose an available operation.");
        if (input.Upload is { Length: > 104857600 }) return new(null, "The file exceeds the 100 MiB upload limit.");
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (state.Closed) return new(null, "Your session has expired. Sign in again.");
            await using Stream? upload = input.Upload?.OpenReadStream();
            var request = new ApiRequest(operation.Id)
            {
                Path = input.Path,
                Query = input.Query.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                Body = string.IsNullOrWhiteSpace(input.Body) ? null : input.Body,
                IdempotencyKey = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey,
                IfMatch = string.IsNullOrWhiteSpace(input.IfMatch) ? null : input.IfMatch,
                Upload = upload, FileName = input.Upload?.FileName,
                ContentType = operation.Id == "submitReportingTransmission" ? "application/xml" : input.Upload?.ContentType
            };
            return new(await state.Client.ExecuteAsync(request, cancellationToken), null);
        }
        catch (ConnectorException error)
        {
            return new(null, string.Join(" | ", new[] { error.Message, error.StatusCode?.ToString(),
                error.Code, error.CorrelationId }.Where(value => !string.IsNullOrEmpty(value))));
        }
        catch (Exception error) when (error is ArgumentException or HttpRequestException or TaskCanceledException)
        { return new(null, "The request could not be completed. Check the input and connection before retrying."); }
        finally { state.Gate.Release(); }
    }

    private Connection? State(ISession session) =>
        session.GetString(SessionKey) is string id ? cache.Get<Connection>(id) : null;

    private sealed class Connection(string environment, ConnectorClient client)
    {
        public string Environment { get; } = environment;
        public ConnectorClient Client { get; } = client;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public bool Closed { get; private set; }
        public async Task CloseAsync()
        {
            await Gate.WaitAsync();
            try { Closed = true; Client.Dispose(); }
            finally { Gate.Release(); }
        }
    }
}

public sealed class ConsoleInput
{
    public string OperationId { get; set; } = "";
    public Dictionary<string, string> Path { get; set; } = [];
    public Dictionary<string, string> Query { get; set; } = [];
    public string? Body { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }
    public IFormFile? Upload { get; set; }
}

public sealed record ConsoleResult(ApiResponse? Response, string? Error);
