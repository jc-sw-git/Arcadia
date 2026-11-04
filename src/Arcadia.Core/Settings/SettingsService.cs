namespace Arcadia.Core.Settings;

/// <summary>
/// The one place that holds the user's settings while Arcadia runs. Every part that changes them
/// (the library view on the UI thread, Steam Controller Support on the thread pool) goes through
/// <see cref="Update"/>, so no part saves a stale copy over another part's change.
/// </summary>
public sealed class SettingsService
{
    private readonly Lock _lock = new();

    private readonly SettingsStore _store;

    // Volatile: the thread pool reads what the UI thread wrote, without taking the lock.
    private volatile AppSettings _current;

    public SettingsService(SettingsStore store)
    {
        _store = store;
        var loaded = store.Load();
        _current = loaded.WithValidLibraryView().WithValidMouseMode();
        if (loaded.LibrarySort != _current.LibrarySort || loaded.PreferredPageSize != _current.PreferredPageSize)
        {
            // A view value this version replaced, e.g. a page size of an older Arcadia (#24): saved at once, so
            // settings.json says what the library shows.
            SaveReplacedValues();
        }
    }

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
            _store.Save(_current);
        }
    }

    private void SaveReplacedValues()
    {
        try
        {
            _store.Save(_current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked or read-only now: the replaced values still apply, and the next change saves them.
        }
    }
}
