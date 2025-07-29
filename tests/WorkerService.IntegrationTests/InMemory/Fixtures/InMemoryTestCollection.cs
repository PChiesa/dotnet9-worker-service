using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Fixtures;

[CollectionDefinition("InMemory Integration Tests")]
public class InMemoryTestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] for the in-memory integration tests.
    // No ICollectionFixture is needed since each InMemoryWebApplicationFactory creates
    // its own isolated in-memory environment.
}