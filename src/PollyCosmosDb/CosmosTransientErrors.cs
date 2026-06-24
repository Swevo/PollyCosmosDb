/// <summary>
/// Pre-built Polly <see cref="PredicateBuilder"/> for common Azure Cosmos DB transient errors.
/// Covers rate limiting (429), timeouts (408), partition splits/failovers (410),
/// service unavailability (503), and the Cosmos-specific RetryWith (449) sub-status.
/// </summary>
public static class CosmosTransientErrors
{
    /// <summary>
    /// Cosmos DB HTTP status codes that indicate a transient failure safe to retry.
    /// </summary>
    public static readonly IReadOnlySet<HttpStatusCode> StatusCodes = new HashSet<HttpStatusCode>
    {
        HttpStatusCode.RequestTimeout,     // 408 — request timed out at Cosmos
        HttpStatusCode.Gone,               // 410 — partition split or replica failover
        HttpStatusCode.TooManyRequests,    // 429 — RU/s exhausted (rate limited)
        HttpStatusCode.ServiceUnavailable, // 503 — Cosmos temporarily unavailable
        (HttpStatusCode)449,               // 449 — RetryWith (Cosmos-specific sub-status)
    };

    /// <summary>
    /// A <see cref="PredicateBuilder"/> that handles <see cref="CosmosException"/> for all
    /// status codes in <see cref="StatusCodes"/>. Assign to <c>ShouldHandle</c> on any Polly strategy.
    /// </summary>
    public static readonly PredicateBuilder IsTransient =
        (PredicateBuilder)new PredicateBuilder()
            .Handle<CosmosException>(ex => StatusCodes.Contains(ex.StatusCode));
}
