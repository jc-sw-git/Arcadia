namespace Arcadia.Core.Settings;

/// <summary>
/// The one place that holds the user's settings while Arcadia runs. Every part that changes them
/// (the library view on the UI thread, Steam Controller Support on the thread pool) goes through
/// <see cref="Update"/>, so no part saves a stale copy over another part's change.
/// </summary>
public sealed class SettingsService(SettingsStore store)
{
    private readonly Lock _lock = new();

    // Volatile: the thread pool reads what the UI thread wrote, without taking the lock.
    private volatile AppSettings _current = store.Load().WithValidLibraryView().WithValidMouseMode();

    public AppSettings Current => _current;

    /// <summary>
    /// Changes and saves the settings. Reading, changing and saving happen under one lock, so no
    /// change is lost and two saves never write <c>settings.json.tmp</c> at once.
    /// </summary>
    public void Update(Func<AppSettings, AppSettings> change)
    {
        lock (_lock)
        {
            _current = change(_current);
            store.Save(_current);
        }
    }
}
