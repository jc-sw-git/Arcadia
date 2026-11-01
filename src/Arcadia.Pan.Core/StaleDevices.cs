using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Arcadia.Pan;

/// <summary>
/// A device Windows knows (#347): plugged in or not. <paramref name="Parent"/> is the live parent of a present device and
/// the one Windows remembers (<c>DEVPKEY_Device_LastKnownParent</c>) for one that isn't; null when it can't be read.
/// </summary>
public sealed record KnownDevice(string InstanceId, bool Present, string? Parent);

/// <summary>Windows' device entries, through the plug and play manager; every call may wait on its PnP lock.</summary>
public interface IStaleDeviceStore
{
    /// <summary>Every device Windows knows, plugged in or not.</summary>
    /// <exception cref="InvalidOperationException">The list can't be read.</exception>
    IReadOnlyList<KnownDevice> KnownDevices();

    /// <summary>The hardware ids of a device, empty when they can't be read.</summary>
    IReadOnlyList<string> HardwareIdsOf(string instanceId);

    bool IsPresent(string instanceId);

    /// <summary>Removes the entry (<c>CM_Uninstall_DevNode</c>); returns the CONFIGRET, 0 on success.</summary>
    uint Remove(string instanceId);
}

/// <summary>
/// Which device entries are stale (#347): not plugged in, and their parent chain ends at usbip-win2's host, which
/// carries every device Arcadia plugs in through VIIPER. Real devices and other software hosts are never touched; a
/// chain that can't be read, or that ends at a host that is gone (usbip-win2 uninstalled), keeps the entry.
/// </summary>
public static class StaleDeviceList
{
    /// <summary>
    /// usbip-win2's host (its <c>usbip2_ude.inf</c>): matched exactly, so the hosts of other usbip drivers (the older
    /// usbip-win names its own) are not.
    /// </summary>
    public const string UsbipWin2HostHardwareId = @"ROOT\USBIP_WIN2\UDE";

    /// <summary>
    /// The stale entries, deepest first (a HID collection before its USB interface, before its device), so a parent goes
    /// only after its children (<see cref="StaleDeviceCleanup"/>).
    /// </summary>
    public static IReadOnlyList<string> Select(IReadOnlyList<KnownDevice> devices, Func<string, IReadOnlyList<string>> hardwareIdsOf)
    {
        var byId = new Dictionary<string, KnownDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            byId.TryAdd(device.InstanceId, device);
        }

        string? ParentOf(string id) => byId.GetValueOrDefault(id)?.Parent;
        var hostIsUsbip = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool IsUsbipHost(string host)
        {
            if (!hostIsUsbip.TryGetValue(host, out var usbip))
            {
                usbip = byId.GetValueOrDefault(host) is { Present: true }
                    && hardwareIdsOf(host).Contains(UsbipWin2HostHardwareId, StringComparer.OrdinalIgnoreCase);
                hostIsUsbip[host] = usbip;
            }

            return usbip;
        }

        return devices
            .Where(device => !device.Present && HidingList.HostOf(device.InstanceId, ParentOf) is { } host && IsUsbipHost(host))
            .Select(device => (device.InstanceId, Depth: Depth(device.InstanceId, ParentOf)))
            .OrderByDescending(entry => entry.Depth)
            .Select(entry => entry.InstanceId)
            .ToList();
    }

    /// <summary>The model for the log (<c>28DE:1304</c>), or the enumerator: an instance id can carry the real puck's serial.</summary>
    public static string ModelOf(string instanceId)
    {
        var match = Regex.Match(instanceId, "VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        return match.Success ? $"{match.Groups[1].Value.ToUpperInvariant()}:{match.Groups[2].Value.ToUpperInvariant()}" : instanceId.Split('\\')[0].ToUpperInvariant();
    }

    private static int Depth(string instanceId, Func<string, string?> parentOf)
    {
        var depth = 0;
        for (var current = instanceId; parentOf(current) is { Length: > 0 } parent && depth < 16; current = parent)
        {
            depth++;
        }

        return depth;
    }
}

public sealed record StaleCleanupResult(int Found, int Removed, int Failed, int Kept, bool Stopped);

/// <summary>
/// Removes the stale entries one by one (#347). <c>CM_Uninstall_DevNode</c> removes a present devnode too and can't be
/// cancelled, so each entry is checked for presence right before it goes, and the run stops only between two entries:
/// when <paramref name="mayContinue"/> says no (an emulation wants to start, Pan stops) or the budget is spent. An
/// entry is removed only when none of its children is still known; a child that failed would otherwise point to a
/// missing parent and never be recognised again. What is left goes at the next run. Never throws.
/// </summary>
public static class StaleDeviceCleanup
{
    public static StaleCleanupResult Run(IStaleDeviceStore store, Func<bool> mayContinue, TimeSpan budget, Action<string> log)
    {
        var watch = Stopwatch.StartNew();
        IReadOnlyList<KnownDevice> known;
        IReadOnlyList<string> stale;
        try
        {
            known = store.KnownDevices();
            stale = StaleDeviceList.Select(known, store.HardwareIdsOf);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log($"Stale device entries not listed: {ex.Message}; tried again next time");
            return new StaleCleanupResult(0, 0, 0, 0, Stopped: false);
        }

        // Children per parent, by what Windows still knows; a removed entry leaves it.
        var children = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in known)
        {
            if (device.Parent is { Length: > 0 } parent)
            {
                if (!children.TryGetValue(parent, out var set))
                {
                    children[parent] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                set.Add(device.InstanceId);
            }
        }

        var parentOf = known.ToDictionary(device => device.InstanceId, device => device.Parent, StringComparer.OrdinalIgnoreCase);
        int removed = 0, failed = 0, kept = 0;
        // One line per model and error, not per entry: the same failure may repeat on many entries at every trigger.
        var failures = new Dictionary<string, int>(StringComparer.Ordinal);
        void Failed(string id, string error)
        {
            failed++;
            var key = $"{StaleDeviceList.ModelOf(id)}: {error}";
            failures[key] = failures.GetValueOrDefault(key) + 1;
        }

        var stopped = false;
        foreach (var id in stale)
        {
            if (!mayContinue() || watch.Elapsed >= budget)
            {
                stopped = true;
                break;
            }

            if (children.TryGetValue(id, out var left) && left.Count > 0)
            {
                kept++;
                continue;
            }

            uint result;
            try
            {
                if (store.IsPresent(id))
                {
                    // Plugged in again since it was listed: Windows uses this entry.
                    kept++;
                    continue;
                }

                result = store.Remove(id);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Failed(id, ex.GetType().Name);
                continue;
            }

            if (result != 0)
            {
                Failed(id, $"CONFIGRET 0x{result:X}");
                continue;
            }

            removed++;
            if (parentOf.GetValueOrDefault(id) is { Length: > 0 } parent && children.TryGetValue(parent, out var siblings))
            {
                siblings.Remove(id);
            }
        }

        foreach (var (failure, count) in failures)
        {
            log($"Stale entries not removed: {count} × {failure}");
        }

        return new StaleCleanupResult(stale.Count, removed, failed, kept, stopped);
    }
}
