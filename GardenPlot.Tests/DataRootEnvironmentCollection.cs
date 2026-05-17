namespace GardenPlot.Tests;

public static class TestCollections
{
    public const string DataRootEnvironment = "DataRoot environment";
}

[CollectionDefinition(TestCollections.DataRootEnvironment, DisableParallelization = true)]
public sealed class DataRootEnvironmentCollectionDefinition
{
}
