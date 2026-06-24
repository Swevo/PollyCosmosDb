/// <summary>Extension methods for adding Polly resilience to Azure Cosmos DB clients.</summary>
public static class PollyCosmosDbExtensions
{
    /// <summary>Wraps a <see cref="Container"/> with the given <see cref="ResiliencePipeline"/>.</summary>
    public static ResilientCosmosContainer WithPolly(
        this Container container,
        ResiliencePipeline pipeline)
        => new(container, pipeline);

    /// <summary>Wraps a <see cref="Container"/> with a pipeline built by <paramref name="configure"/>.</summary>
    public static ResilientCosmosContainer WithPolly(
        this Container container,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        return new(container, builder.Build());
    }

    /// <summary>
    /// Gets the specified container from <paramref name="client"/> and wraps it with the given pipeline.
    /// </summary>
    public static ResilientCosmosContainer WithPolly(
        this CosmosClient client,
        string databaseId,
        string containerId,
        ResiliencePipeline pipeline)
        => new(client.GetContainer(databaseId, containerId), pipeline);

    /// <summary>
    /// Gets the specified container from <paramref name="client"/> and wraps it with a pipeline
    /// built by <paramref name="configure"/>.
    /// </summary>
    public static ResilientCosmosContainer WithPolly(
        this CosmosClient client,
        string databaseId,
        string containerId,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        return new(client.GetContainer(databaseId, containerId), builder.Build());
    }
}
