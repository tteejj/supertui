using Xunit;

namespace SuperTUI.Tests.TestHelpers
{
    /// <summary>
    /// Collection definition for tests that use singleton services.
    /// All tests in this collection will run sequentially to avoid singleton state pollution.
    /// </summary>
    [CollectionDefinition("SingletonTests", DisableParallelization = true)]
    public class SingletonTestCollection
    {
        // This class is never instantiated. It exists only to define the collection.
    }
}
