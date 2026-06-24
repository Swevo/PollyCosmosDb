public class CosmosTransientErrorsTests
{
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData((HttpStatusCode)449)]
    public void StatusCodes_ContainsTransientStatusCode(HttpStatusCode statusCode)
    {
        Assert.Contains(statusCode, CosmosTransientErrors.StatusCodes);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void StatusCodes_DoesNotContainNonTransientStatusCode(HttpStatusCode statusCode)
    {
        Assert.DoesNotContain(statusCode, CosmosTransientErrors.StatusCodes);
    }

    [Fact]
    public void StatusCodes_HasFiveEntries()
    {
        Assert.Equal(5, CosmosTransientErrors.StatusCodes.Count);
    }

    [Fact]
    public void IsTransient_IsNotNull()
    {
        Assert.NotNull(CosmosTransientErrors.IsTransient);
    }
}
