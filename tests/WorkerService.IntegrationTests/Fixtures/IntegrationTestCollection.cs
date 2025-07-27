using Xunit;

namespace WorkerService.IntegrationTests.Fixtures;

[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<WorkerServiceTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the ICollectionFixture<> interfaces.
    // This ensures that all tests in the "Integration Tests" collection share the same test fixture
    // and run sequentially to avoid port conflicts with test containers.
}