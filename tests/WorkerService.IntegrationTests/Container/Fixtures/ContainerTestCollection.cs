using Xunit;
using WorkerService.IntegrationTests.Shared.Fixtures;

namespace WorkerService.IntegrationTests.Container.Fixtures;

[CollectionDefinition("Container Integration Tests")]
public class ContainerTestCollection : ICollectionFixture<WorkerServiceTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces for the container-based integration tests.
}