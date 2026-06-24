public class PollyCosmosDbExtensionsTests
{
    // Well-known local emulator key — safe to use in tests (no network calls made)
    private static readonly CosmosClient _client = new(
        "https://localhost:8081/",
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

    private static readonly Container _container = _client.GetContainer("testdb", "testcontainer");
    private static readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder().Build();

    [Fact]
    public void WithPolly_Container_Pipeline_ReturnsResilientCosmosContainer()
    {
        var resilient = _container.WithPolly(_pipeline);

        Assert.NotNull(resilient);
        Assert.Same(_container, resilient.Inner);
    }

    [Fact]
    public void WithPolly_Container_Configure_ReturnsResilientCosmosContainer()
    {
        var resilient = _container.WithPolly(p => p.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            ShouldHandle = CosmosTransientErrors.IsTransient,
        }));

        Assert.NotNull(resilient);
        Assert.Same(_container, resilient.Inner);
    }

    [Fact]
    public void WithPolly_CosmosClient_Pipeline_ReturnsResilientCosmosContainer()
    {
        var resilient = _client.WithPolly("testdb", "testcontainer", _pipeline);

        Assert.NotNull(resilient);
        Assert.NotNull(resilient.Inner);
    }

    [Fact]
    public void WithPolly_CosmosClient_Configure_ReturnsResilientCosmosContainer()
    {
        var resilient = _client.WithPolly("testdb", "testcontainer", p =>
            p.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                ShouldHandle = CosmosTransientErrors.IsTransient,
            }));

        Assert.NotNull(resilient);
        Assert.NotNull(resilient.Inner);
    }

    [Fact]
    public void WithPolly_Container_InnerIsOriginalContainer()
    {
        var resilient = _container.WithPolly(_pipeline);

        Assert.Same(_container, resilient.Inner);
    }
}
