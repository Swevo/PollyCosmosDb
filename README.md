# PollyCosmosDb

[![NuGet](https://img.shields.io/nuget/v/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb)
[![CI](https://github.com/Swevo/PollyCosmosDb/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyCosmosDb/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers — expose circuit-breaker state (Closed, HalfOpen, Open, Isolated) as /health endpoint responses |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies for Polly v8 resilience pipelines |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience pipelines for Entity Framework Core — wrap every EF Core query and SaveChanges with retry, timeout and circuit-breaker via a single AddPollyResilience() call |
| [PollyMailKit](https://www.nuget.org/packages/PollyMailKit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMailKit.svg)](https://www.nuget.org/packages/PollyMailKit) | Polly v8 resilience pipelines for MailKit — retry, timeout, and circuit-breaker for SmtpClient.SendAsync and any MailKit SMTP operation |
| [PollyMassTransit](https://www.nuget.org/packages/PollyMassTransit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMassTransit.svg)](https://www.nuget.org/packages/PollyMassTransit) | Polly v8 resilience pipelines for MassTransit — retry, timeout, and circuit-breaker for IBus.Publish and ISendEndpointProvider.Send |
| [PollyNpgsql](https://www.nuget.org/packages/PollyNpgsql) | [![Downloads](https://img.shields.io/nuget/dt/PollyNpgsql.svg)](https://www.nuget.org/packages/PollyNpgsql) | Polly v8 resilience pipelines for Npgsql (PostgreSQL) — retry, timeout, and circuit-breaker for NpgsqlConnection queries and commands, plus a built-in PostgresTransientErrors predicate covering all common PostgreSQL transient SQLSTATE codes |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollyAzureEventHub](https://www.nuget.org/packages/PollyAzureEventHub) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureEventHub.svg)](https://www.nuget.org/packages/PollyAzureEventHub) | Polly v8 resilience pipelines for Azure Event Hubs — retry, timeout, and circuit-breaker for EventHubProducerClient and EventHubConsumerClient |
| [PollyElasticsearch](https://www.nuget.org/packages/PollyElasticsearch) | [![Downloads](https://img.shields.io/nuget/dt/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch) | Polly v8 resilience pipelines for Elastic.Clients.Elasticsearch 8+ — retry, timeout, and circuit-breaker for any Elasticsearch operation, plus a built-in ElasticTransientErrors predicate covering rate limiting (429), service unavailability (503), gateway timeouts (504), and connection failures |
| [PollyHangfire](https://www.nuget.org/packages/PollyHangfire) | [![Downloads](https://img.shields.io/nuget/dt/PollyHangfire.svg)](https://www.nuget.org/packages/PollyHangfire) | Polly v8 resilience pipelines for Hangfire — retry, timeout, and circuit-breaker for IBackgroundJobClient.Enqueue and Schedule |
| [PollySendGrid](https://www.nuget.org/packages/PollySendGrid) | [![Downloads](https://img.shields.io/nuget/dt/PollySendGrid.svg)](https://www.nuget.org/packages/PollySendGrid) | Polly v8 resilience pipelines for SendGrid — retry, timeout, and circuit-breaker for ISendGridClient.SendEmailAsync |
| [PollyMongo](https://www.nuget.org/packages/PollyMongo) | [![Downloads](https://img.shields.io/nuget/dt/PollyMongo.svg)](https://www.nuget.org/packages/PollyMongo) | Polly v8 resilience pipelines for MongoDB.Driver — wrap Find, InsertOne, UpdateOne, DeleteOne and other IMongoCollection calls with retry, timeout, circuit-breaker, and more using a single ResilientMongoCollection decorator |
| [PollyDapper](https://www.nuget.org/packages/PollyDapper) | [![Downloads](https://img.shields.io/nuget/dt/PollyDapper.svg)](https://www.nuget.org/packages/PollyDapper) | Polly v8 resilience pipelines for Dapper — wrap QueryAsync, ExecuteAsync, and other Dapper calls with retry, timeout, circuit-breaker, and more using a single ResilientDbConnection decorator |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience pipelines for MediatR — add retry, timeout, circuit-breaker, rate-limiting, hedging, and chaos engineering to any MediatR request handler with a single line of DI registration |
| [PollySqlClient](https://www.nuget.org/packages/PollySqlClient) | [![Downloads](https://img.shields.io/nuget/dt/PollySqlClient.svg)](https://www.nuget.org/packages/PollySqlClient) | Polly v8 resilience pipelines for Microsoft.Data.SqlClient (SQL Server and Azure SQL) — retry, timeout, and circuit-breaker for SqlConnection queries and commands, plus a built-in SqlServerTransientErrors predicate covering all common SQL Server and Azure SQL transient error numbers |
| [PollyAzureKeyVault](https://www.nuget.org/packages/PollyAzureKeyVault) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureKeyVault.svg)](https://www.nuget.org/packages/PollyAzureKeyVault) | Polly v8 resilience pipelines for Azure Key Vault — retry, timeout, and circuit-breaker for SecretClient, KeyClient, and CertificateClient |
| [PollyAzureQueueStorage](https://www.nuget.org/packages/PollyAzureQueueStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureQueueStorage.svg)](https://www.nuget.org/packages/PollyAzureQueueStorage) | Polly v8 resilience pipelines for Azure Queue Storage — retry, timeout, and circuit-breaker for Azure.Storage.Queues QueueClient |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus — retry, circuit breaker, and timeout for sending and receiving messages |
| [PollyAzureBlob](https://www.nuget.org/packages/PollyAzureBlob) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureBlob.svg)](https://www.nuget.org/packages/PollyAzureBlob) | Polly v8 resilience pipelines for Azure Blob Storage — wrap BlobClient and BlobContainerClient operations with retry, timeout, circuit-breaker, and more using ResilientBlobClient and ResilientBlobContainerClient decorators |
| [PollyAzureTableStorage](https://www.nuget.org/packages/PollyAzureTableStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureTableStorage.svg)](https://www.nuget.org/packages/PollyAzureTableStorage) | Polly v8 resilience pipelines for Azure Table Storage — retry, timeout, and circuit-breaker for Azure.Data.Tables TableClient |

## 💼 Need .NET consulting?

The author of this package is available for consulting on **Polly v8 resilience**, **Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**
## License

MIT
