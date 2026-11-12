namespace Arcadia.Core.Metadata;

/// <summary>
/// What the row "Fehlende Metadaten" shows (#493): of the <paramref name="Total"/> games whose launcher has a store page,
/// <paramref name="Missing"/> lack its answer (during "Alle Metadaten neu laden" also those not reloaded yet).
/// </summary>
/// <param name="Running">A run asks the store now.</param>
/// <param name="Reload">That run is "Alle Metadaten neu laden".</param>
/// <param name="Remaining">About how long the run still takes, from the store's limit and the rate seen; null while none runs.</param>
public sealed record MetadataProgress(int Missing, int Total, bool Running, bool Reload, TimeSpan? Remaining)
{
    /// <summary>Before the first scan.</summary>
    public static MetadataProgress NotYet { get; } = new(0, 0, false, false, null);
}

/// <summary>A launcher's library and its metadata provider for a run; null when the launcher has none.</summary>
public sealed record LibraryMetadata(Library.LauncherLibrary Library, global::Arcadia.Launchers.Abstractions.ILauncherMetadata? Source);
