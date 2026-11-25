using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// The unused methods preserve the native COM vtable order.
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDevices {
    void EnumEndpoints();
    void GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDevice {
    void Activate(); void OpenPropertyStore();
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
// Windows' internal PolicyConfig interface; verify this on each supported OS.
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPolicy {
    void GetMixFormat(); void GetDeviceFormat(); void ResetDeviceFormat(); void SetDeviceFormat();
    void GetProcessingPeriod(); void SetProcessingPeriod(); void GetShareMode(); void SetShareMode();
    void GetPropertyValue(); void SetPropertyValue();
    void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
}

interface IDefaultDevices {
    string Get(int role);
    void Set(string id, int role);
}
interface IRecoveryStore {
    string[] Read();
    void Save(string[] ids);
    void Clear();
}

sealed class WindowsDefaultDevices : IDefaultDevices {
    static object Create(string cls) { return Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(cls))); }
    public string Get(int role) {
        var e = (IDevices)Create("BCDE0395-E52F-467C-8E3D-C4579291692E");
        try {
            IDevice d; e.GetDefaultAudioEndpoint(0, role, out d);
            try { string id; d.GetId(out id); return id; }
            finally { Marshal.ReleaseComObject(d); }
        } finally { Marshal.ReleaseComObject(e); }
    }
    public void Set(string id, int role) {
        var p = (IPolicy)Create("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
        try { p.SetDefaultEndpoint(id, role); }
        finally { Marshal.ReleaseComObject(p); }
    }
}

sealed class RecoveryFile : IRecoveryStore {
    readonly string path;
    public RecoveryFile(string path) { this.path = path; }
    public string[] Read() { return File.Exists(path) ? File.ReadAllLines(path) : null; }
    public void Save(string[] ids) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        // Commit the recovery record before any device change. Preserve the old format.
        using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) {
            var bytes = new UTF8Encoding(false).GetBytes(string.Join(Environment.NewLine, ids));
            file.Write(bytes, 0, bytes.Length);
            file.Flush(true);
        }
        File.Move(path + ".tmp", path);
    }
    public void Clear() { File.Delete(path); }
}

sealed class DefaultOutputSession {
    readonly IDefaultDevices devices;
    readonly IRecoveryStore store;
    public DefaultOutputSession(IDefaultDevices devices, IRecoveryStore store) {
        this.devices = devices; this.store = store;
    }
    public static bool Equal(string a, string b) { return String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    public void Switch(string cable) {
        Restore();
        var previous = new[] { devices.Get(0), devices.Get(1), devices.Get(2) };
        if (Equal(previous[0], cable) && Equal(previous[1], cable)) return;
        store.Save(new[] { cable, previous[0], previous[1], previous[2] });
        Apply(new[] { cable, cable, previous[2] });
    }
    public void Restore() {
        var ids = store.Read();
        if (ids == null) return;
        if (ids.Length != 4 || ids.Any(string.IsNullOrWhiteSpace))
            throw new Exception("输出设备恢复记录不完整，请保留 restore.txt 并检查记录。");
        Apply(ids.Skip(1).ToArray());
        store.Clear();
    }
    void Apply(string[] desired) {
        Exception lastError = null;
        // A role change can affect other roles. Re-read and verify the entire snapshot.
        for (int attempt = 0; attempt < 3; attempt++) {
            for (int role = 0; role < 3; role++) {
                try { if (!Equal(devices.Get(role), desired[role])) devices.Set(desired[role], role); }
                catch (Exception e) { lastError = e; }
            }
            try {
                if (Enumerable.Range(0, 3).All(r => Equal(devices.Get(r), desired[r]))) return;
            } catch (Exception e) { lastError = e; }
        }
        throw new Exception("Windows 输出设备切换未完成，请重新连接原输出设备后重试。", lastError);
    }
}

static class Defaults {
    static readonly IDefaultDevices Devices = new WindowsDefaultDevices();
    static readonly DefaultOutputSession Session = new DefaultOutputSession(Devices,
        new RecoveryFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicCast", "restore.txt")));
    public static string Get(int role) { return Devices.Get(role); }
    public static bool Equal(string a, string b) { return DefaultOutputSession.Equal(a, b); }
    public static void Switch(string cable) { Session.Switch(cable); }
    public static void Restore() { Session.Restore(); }
}
