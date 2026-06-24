/// <summary>
/// Wraps an Azure Cosmos DB <see cref="Container"/> with a Polly v8 <see cref="ResiliencePipeline"/>,
/// applying retry, timeout, and circuit-breaker policies to every operation.
/// </summary>
public sealed class ResilientCosmosContainer(Container container, ResiliencePipeline pipeline)
{
    /// <summary>The underlying <see cref="Container"/>.</summary>
    public Container Inner => container;

    /// <summary>Creates an item, protected by the resilience pipeline.</summary>
    public Task<ItemResponse<T>> CreateItemAsync<T>(
        T item,
        PartitionKey? partitionKey = null,
        ItemRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask<ItemResponse<T>>(container.CreateItemAsync(item, partitionKey, requestOptions, ct)),
            cancellationToken).AsTask();

    /// <summary>Reads an item by id and partition key, protected by the resilience pipeline.</summary>
    public Task<ItemResponse<T>> ReadItemAsync<T>(
        string id,
        PartitionKey partitionKey,
        ItemRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask<ItemResponse<T>>(container.ReadItemAsync<T>(id, partitionKey, requestOptions, ct)),
            cancellationToken).AsTask();

    /// <summary>Upserts an item, protected by the resilience pipeline.</summary>
    public Task<ItemResponse<T>> UpsertItemAsync<T>(
        T item,
        PartitionKey? partitionKey = null,
        ItemRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask<ItemResponse<T>>(container.UpsertItemAsync(item, partitionKey, requestOptions, ct)),
            cancellationToken).AsTask();

    /// <summary>Replaces an item by id, protected by the resilience pipeline.</summary>
    public Task<ItemResponse<T>> ReplaceItemAsync<T>(
        T item,
        string id,
        PartitionKey? partitionKey = null,
        ItemRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask<ItemResponse<T>>(container.ReplaceItemAsync(item, id, partitionKey, requestOptions, ct)),
            cancellationToken).AsTask();

    /// <summary>Deletes an item by id and partition key, protected by the resilience pipeline.</summary>
    public Task<ItemResponse<T>> DeleteItemAsync<T>(
        string id,
        PartitionKey partitionKey,
        ItemRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask<ItemResponse<T>>(container.DeleteItemAsync<T>(id, partitionKey, requestOptions, ct)),
            cancellationToken).AsTask();

    /// <summary>
    /// Executes a query and collects all pages into a <see cref="List{T}"/>,
    /// protected by the resilience pipeline.
    /// </summary>
    public Task<List<T>> QueryAsync<T>(
        QueryDefinition query,
        QueryRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(async ct =>
        {
            var results = new List<T>();
            using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: requestOptions);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(ct);
                results.AddRange(page);
            }
            return results;
        }, cancellationToken).AsTask();
}
