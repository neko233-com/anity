using Xunit;

namespace Anity.Core.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AssetPipelineStateCollection
{
    public const string Name = "Asset pipeline global state";
}
