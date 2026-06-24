public class PollyCosmosDbServiceCollectionExtensionsTests
{
    private static readonly CosmosClient _client = new(
        "https://localhost:8081/",
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

    [Fact]
    public void AddPollyCosmosDb_RegistersResiliencePipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);
        services.AddPollyCosmosDb("testdb", "testcontainer", p =>
            p.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                ShouldHandle = CosmosTransientErrors.IsTransient,
            }));

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<ResiliencePipeline>();

        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddPollyCosmosDb_RegistersResilientCosmosContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);
        services.AddPollyCosmosDb("testdb", "testcontainer", p =>
            p.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                ShouldHandle = CosmosTransientErrors.IsTransient,
            }));

        var provider = services.BuildServiceProvider();
        var container = provider.GetRequiredService<ResilientCosmosContainer>();

        Assert.NotNull(container);
    }

    [Fact]
    public void AddPollyCosmosDb_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);

        var result = services.AddPollyCosmosDb("testdb", "testcontainer", p => { });

        Assert.Same(services, result);
    }

    [Fact]
    public void AddPollyCosmosDb_ResilientContainerHasCorrectInner()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);
        services.AddPollyCosmosDb("testdb", "testcontainer", p => { });

        var provider = services.BuildServiceProvider();
        var container = provider.GetRequiredService<ResilientCosmosContainer>();

        Assert.Equal("testcontainer", container.Inner.Id);
    }
}
