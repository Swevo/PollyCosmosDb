# PollyCosmosDb

[![NuGet](https://img.shields.io/nuget/v/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb)
[![CI](https://github.com/Swevo/PollyCosmosDb/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyCosmosDb/actions)

**Polly v8 resilience for Azure Cosmos DB** — retry, timeout, and circuit-breaker for `Container` operations, plus a built-in `CosmosTransientErrors` predicate covering rate limiting (429), timeouts (408), partition failovers (410), and service unavailability (503). Zero changes to your existing Cosmos code.

```csharp
// Before
await container.CreateItemAsync(order, new PartitionKey(order.CustomerId));

// After — automatic retry + timeout on every operation
var resilient = container.WithPolly(pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = CosmosTransientErrors.IsTransient, // built-in ✔
        })
        .AddTimeout(TimeSpan.FromSeconds(30)));

await resilient.CreateItemAsync(order, new PartitionKey(order.CustomerId));
```

---

## Installation

```bash
dotnet add package PollyCosmosDb
```

Targets **net6.0**, **net8.0**, and **net9.0**.
Dependencies: `Polly.Core 8.*`, `Microsoft.Azure.Cosmos 3.*`, `Microsoft.Extensions.DependencyInjection.Abstractions 8.*`

---

## CosmosTransientErrors — the key feature

Cosmos DB has its own built-in retry for throttling (429s), but it does **not** cover timeouts, partition failovers, or service unavailability. `PollyCosmosDb` ships `CosmosTransientErrors.IsTransient` so you don't have to look up which `HttpStatusCode` values are safe to retry.

```csharp
new RetryStrategyOptions
{
    MaxRetryAttempts = 3,
    ShouldHandle = CosmosTransientErrors.IsTransient,
}
```

### Covered status codes

| Code | Name | Description |
|------|------|-------------|
| `408` | RequestTimeout | Request timed out at Cosmos |
| `410` | Gone | Partition split or replica failover |
| `429` | TooManyRequests | RU/s exhausted (rate limited) |
| `449` | RetryWith | Cosmos-specific sub-status — retry immediately |
| `503` | ServiceUnavailable | Cosmos temporarily unavailable |

> **Note:** Cosmos DB already retries 429s internally. Adding Polly on top gives you control over *how many times* and *how long* to retry across all transient failure modes.

The raw set is also available for extension:

```csharp
var myErrors = CosmosTransientErrors.StatusCodes.ToHashSet();
myErrors.Add(HttpStatusCode.InternalServerError); // retry 500s too

new RetryStrategyOptions
{
    ShouldHandle = new PredicateBuilder()
        .Handle<CosmosException>(ex => myErrors.Contains(ex.StatusCode))
}
```

---

## Quick start

### Inline pipeline

```csharp
using PollyCosmosDb;

var resilient = container.WithPolly(pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = CosmosTransientErrors.IsTransient,
        })
        .AddTimeout(TimeSpan.FromSeconds(30)));

// CRUD
var created  = await resilient.CreateItemAsync(order, new PartitionKey(order.Id));
var read     = await resilient.ReadItemAsync<Order>(id, new PartitionKey(id));
var upserted = await resilient.UpsertItemAsync(order, new PartitionKey(order.Id));
var replaced = await resilient.ReplaceItemAsync(order, order.Id, new PartitionKey(order.Id));
var deleted  = await resilient.DeleteItemAsync<Order>(id, new PartitionKey(id));

// Query
var query  = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @id")
    .WithParameter("@id", customerId);
var orders = await resilient.QueryAsync<Order>(query);
```

### From CosmosClient directly

```csharp
var resilient = cosmosClient.WithPolly("ecommerce", "orders", pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = CosmosTransientErrors.IsTransient,
        })
        .AddTimeout(TimeSpan.FromSeconds(30)));
```

### Dependency injection

```csharp
// Program.cs
builder.Services.AddSingleton(new CosmosClient(connectionString));

builder.Services.AddPollyCosmosDb("ecommerce", "orders", pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = CosmosTransientErrors.IsTransient,
        })
        .AddTimeout(TimeSpan.FromSeconds(30))
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
        }));

// Repository
public class OrderRepository(ResilientCosmosContainer container)
{
    public Task<ItemResponse<Order>> SaveAsync(Order order) =>
        container.UpsertItemAsync(order, new PartitionKey(order.Id));

    public async Task<List<Order>> GetByCustomerAsync(string customerId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.customerId = @id")
            .WithParameter("@id", customerId);
        return await container.QueryAsync<Order>(query);
    }
}
```

---

## Supported operations

| Method | Description |
|--------|-------------|
| `CreateItemAsync<T>` | Create an item |
| `ReadItemAsync<T>` | Read item by id + partition key |
| `UpsertItemAsync<T>` | Insert or replace an item |
| `ReplaceItemAsync<T>` | Replace an existing item |
| `DeleteItemAsync<T>` | Delete item by id + partition key |
| `QueryAsync<T>` | Query with `QueryDefinition`, returns `List<T>` |

---

## Pipeline order

```
[Timeout] → [Retry] → [Circuit Breaker] → [CosmosClient]
```

```csharp
pipeline
    .AddTimeout(TimeSpan.FromSeconds(30))   // 1. Overall deadline
    .AddRetry(retryOptions)                 // 2. Retry transient failures
    .AddCircuitBreaker(cbOptions)           // 3. Open circuit under load
```

---

## Related packages

| Package | Downloads | Description |
|---|---|---|
| [PollyAzureBlob](https://www.nuget.org/packages/PollyAzureBlob) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureBlob.svg)](https://www.nuget.org/packages/PollyAzureBlob) | Polly v8 resilience for Azure Blob Storage |
| [PollySqlClient](https://www.nuget.org/packages/PollySqlClient) | [![Downloads](https://img.shields.io/nuget/dt/PollySqlClient.svg)](https://www.nuget.org/packages/PollySqlClient) | Polly v8 resilience for SQL Server and Azure SQL with SqlServerTransientErrors predicate |
| [PollyNpgsql](https://www.nuget.org/packages/PollyNpgsql) | [![Downloads](https://img.shields.io/nuget/dt/PollyNpgsql.svg)](https://www.nuget.org/packages/PollyNpgsql) | Polly v8 resilience for Npgsql (PostgreSQL) with PostgresTransientErrors predicate |
| [PollyDapper](https://www.nuget.org/packages/PollyDapper) | [![Downloads](https://img.shields.io/nuget/dt/PollyDapper.svg)](https://www.nuget.org/packages/PollyDapper) | Polly v8 resilience for Dapper (any IDbConnection) |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience for Entity Framework Core |
| [PollyMongo](https://www.nuget.org/packages/PollyMongo) | [![Downloads](https://img.shields.io/nuget/dt/PollyMongo.svg)](https://www.nuget.org/packages/PollyMongo) | Polly v8 resilience for MongoDB.Driver |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyAzureEventHub](https://github.com/Swevo/PollyAzureEventHub) | Polly v8 for Azure Event Hubs |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience for MediatR |
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers |
| [PollyElasticsearch](https://github.com/Swevo/PollyElasticsearch) | Polly v8 for Elastic.Clients.Elasticsearch |
| [PollyAzureKeyVault](https://github.com/Swevo/PollyAzureKeyVault) | Polly v8 for Azure Key Vault |
| [PollySendGrid](https://github.com/Swevo/PollySendGrid) | Polly v8 for SendGrid |
| [PollyMassTransit](https://github.com/Swevo/PollyMassTransit) | Polly v8 for MassTransit |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Jitter, linear & custom backoff for Polly v8 retry |

---

## License

MIT