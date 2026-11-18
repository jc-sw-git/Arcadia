using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>The merge as on a German PC (#557): German UI, region Germany, so USK first. Tests of other languages say so.</summary>
internal static class GermanPc
{
    public static MergedMetadata Merged(IReadOnlyList<ProviderMetadata> providers) => MergedMetadata.Of(providers, "de", AgeRatingBoard.Usk);
}
