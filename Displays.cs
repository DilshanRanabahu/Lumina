using System.Management;

namespace Lumina;

/// <summary>A screen whose brightness can be changed. Writes run on a background thread and only the latest value is applied.</summary>
abstract class DisplayItem : IDisposable
{
    readonly object gate = new();
    int pending = -1;
    bool busy;

    protected DisplayItem(string name) => Name = name;

    public string Name { get; }

    /// <summary>Reads the current brightness (0-100), or -1 if it could not be read.</summary>
    public abstract int ReadBrightness();

    protected abstract void ApplyBrightness(int percent);

    public void RequestBrightness(int percent)
    {
        lock (gate)
        {
            pending = percent;
            if (busy) return;
            busy = true;
        }

        Task.Run(() =>
        {
            while (true)
            {
                int value;
                lock (gate)
                {
                    if (pending < 0) { busy = false; return; }
                    value = pending;
                    pending = -1;
                }
                try { ApplyBrightness(value); } catch { }
            }
        });
    }

    public virtual void Dispose()
    {
        // Let any in-flight write finish before the handle goes away.
        for (int i = 0; i < 50; i++)
        {
            lock (gate) { pending = -1; if (!busy) return; }
            Thread.Sleep(20);
        }
    }
}

/// <summary>External monitor controlled over DDC/CI.</summary>
sealed class DdcDisplay : DisplayItem
{
    readonly IntPtr handle;
    readonly uint max;

    public DdcDisplay(string name, IntPtr handle, uint max) : base(name)
    {
        this.handle = handle;
        this.max = max == 0 ? 100 : max;
    }

    public override int ReadBrightness() =>
        Native.GetVCPFeatureAndVCPFeatureReply(handle, Native.VCP_BRIGHTNESS, IntPtr.Zero, out uint cur, out _)
            ? (int)Math.Round(cur * 100.0 / max)
            : -1;

    protected override void ApplyBrightness(int percent) =>
        Native.SetVCPFeature(handle, Native.VCP_BRIGHTNESS, (uint)Math.Round(percent * max / 100.0));

    public override void Dispose()
    {
        base.Dispose();
        Native.DestroyPhysicalMonitor(handle);
    }
}

/// <summary>Built-in laptop panel controlled through WMI.</summary>
sealed class WmiDisplay : DisplayItem
{
    readonly string instanceName;

    public WmiDisplay(string name, string instanceName) : base(name) => this.instanceName = instanceName;

    public override int ReadBrightness()
    {
        // WMI can fail transiently (e.g. right after resume from sleep); report "unknown" instead of crashing.
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    if ((string)mo["InstanceName"] == instanceName)
                        return (byte)mo["CurrentBrightness"];
                }
            }
        }
        catch (Exception e) when (e is ManagementException or System.Runtime.InteropServices.COMException) { }
        return -1;
    }

    protected override void ApplyBrightness(int percent)
    {
        using var cls = new ManagementClass(@"root\wmi", "WmiMonitorBrightnessMethods", null);
        foreach (ManagementObject mo in cls.GetInstances())
        {
            using (mo)
            {
                if ((string)mo["InstanceName"] == instanceName)
                    mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
            }
        }
    }
}

static class DisplayManager
{
    public static List<DisplayItem> Enumerate()
    {
        var friendlyNames = ReadFriendlyNames();
        var result = new List<DisplayItem>();

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName FROM WmiMonitorBrightness");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    var instance = (string)mo["InstanceName"];
                    var name = LookupName(friendlyNames, instance);
                    result.Add(new WmiDisplay(string.IsNullOrEmpty(name) ? "Laptop screen" : name, instance));
                }
            }
        }
        catch (ManagementException) { }

        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref Native.RECT rect, IntPtr data) =>
        {
            AddDdcMonitors(hMonitor, friendlyNames, result);
            return true;
        }, IntPtr.Zero);

        return result;
    }

    static void AddDdcMonitors(IntPtr hMonitor, Dictionary<string, string> friendlyNames, List<DisplayItem> result)
    {
        if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
            return;

        var monitors = new Native.PHYSICAL_MONITOR[count];
        if (!Native.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
            return;

        var info = new Native.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
        Native.GetMonitorInfo(hMonitor, ref info);

        for (uint i = 0; i < count; i++)
        {
            var handle = monitors[i].hPhysicalMonitor;

            // Panels without DDC/CI (such as the laptop screen) fail here and are skipped.
            if (!Native.GetVCPFeatureAndVCPFeatureReply(handle, Native.VCP_BRIGHTNESS, IntPtr.Zero, out _, out uint max))
            {
                Native.DestroyPhysicalMonitor(handle);
                continue;
            }

            var name = LookupName(friendlyNames, DeviceInstanceFor(info.szDevice, i));
            result.Add(new DdcDisplay(string.IsNullOrEmpty(name) ? "External monitor" : name, handle, max));
        }
    }

    /// <summary>Maps a GDI device like \\.\DISPLAY2 to a PnP instance like DISPLAY\GSM5AB8\4&amp;512c522&amp;0&amp;UID198147.</summary>
    static string DeviceInstanceFor(string gdiDevice, uint index)
    {
        var dd = new Native.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
        if (!Native.EnumDisplayDevices(gdiDevice, index, ref dd, Native.EDD_GET_DEVICE_INTERFACE_NAME))
            return "";

        // \\?\DISPLAY#GSM5AB8#4&512c522&0&UID198147#{e6f07b5f-...}
        var id = dd.DeviceID;
        if (id.StartsWith(@"\\?\")) id = id[4..];
        int guid = id.IndexOf("#{", StringComparison.Ordinal);
        if (guid >= 0) id = id[..guid];
        return id.Replace('#', '\\');
    }

    static Dictionary<string, string> ReadFriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    var chars = ((ushort[])mo["UserFriendlyName"] ?? []).TakeWhile(c => c != 0).Select(c => (char)c);
                    names[(string)mo["InstanceName"]] = new string(chars.ToArray()).Trim();
                }
            }
        }
        catch (ManagementException) { }
        return names;
    }

    static string LookupName(Dictionary<string, string> names, string instance)
    {
        if (string.IsNullOrEmpty(instance)) return "";
        // WMI instance names carry a "_0" style suffix.
        foreach (var (key, value) in names)
            if (key.StartsWith(instance, StringComparison.OrdinalIgnoreCase))
                return value;
        return "";
    }
}
