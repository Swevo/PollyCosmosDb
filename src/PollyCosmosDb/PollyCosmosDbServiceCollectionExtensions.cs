/// <summary>Dependency-injection extensions for <c>PollyCosmosDb</c>.</summary>
public static class PollyCosmosDbServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="ResiliencePipeline"/> built by <paramref name="configure"/>
    /// and a transient <see cref="ResilientCosmosContainer"/> factory that resolves
    /// <see cref="CosmosClient"/> from the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseId">Cosmos DB database identifier.</param>
    /// <param name="containerId">Cosmos DB container identifier.</param>
    /// <param name="configure">Delegate to configure the resilience pipeline.</param>
    public static IServiceCollection AddPollyCosmosDb(
        this IServiceCollection services,
        string databaseId,
        string containerId,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        var pipeline = builder.Build();

        services.AddSingleton(pipeline);
        services.AddTransient<ResilientCosmosContainer>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            return client.WithPolly(databaseId, containerId, pipeline);
        });

        return services;
    }
}
