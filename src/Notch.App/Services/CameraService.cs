using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Notch.App.Services;

/// <summary>
/// Descobre se a webcam está em uso, sem abrir a câmera.
///
/// Fonte principal: o monitor de atividade de sensores do Media Foundation (o mesmo que alimenta
/// o aviso de privacidade do Windows). Ele avisa sempre que alguma câmera começa ou para de
/// transmitir e informa quais processos estão usando.
///
/// Reserva: o registro de privacidade (ConsentStore\webcam). Nas versões novas do Windows 11 ele
/// deixou de ser atualizado, por isso só é usado se o monitor não estiver disponível.
/// </summary>
internal sealed class CameraService : IDisposable
{
    private const uint MfVersion = 0x00020070;
    private const uint MfStartupLite = 1;
    private const string Store = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    private IMFSensorActivityMonitor? _monitor;
    private ReportCallback? _callback;
    private bool _mfStarted;
    private bool _unavailable;
    private volatile int[] _streamingPids = [];

    /// <summary>Nomes dos programas usando a webcam agora (vazio = desligada).</summary>
    public List<string> AppsInUse()
    {
        if (!_unavailable && _monitor == null)
        {
            // o monitor e seus avisos vivem numa thread MTA
            var error = Task.Run(StartMonitor).GetAwaiter().GetResult();
            if (error != null)
            {
                _unavailable = true;
                Core.Paths.WriteLog("Monitor da câmera indisponível, usando o registro: " + error);
                StopMonitor();
            }
        }
        return _unavailable ? FromRegistry() : FromPids(_streamingPids);
    }

    private static List<string> FromPids(int[] pids)
    {
        var names = new List<string>();
        foreach (var pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                names.Add(p.ProcessName);
            }
            catch (ArgumentException) { } // processo já fechou
            catch (InvalidOperationException) { }
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string? StartMonitor()
    {
        try
        {
            var hr = MFStartup(MfVersion, MfStartupLite);
            if (hr < 0) return $"MFStartup 0x{hr:X8}";
            _mfStarted = true;
            _callback = new ReportCallback(this);
            hr = MFCreateSensorActivityMonitor(_callback, out var monitor);
            if (hr < 0) return $"MFCreateSensorActivityMonitor 0x{hr:X8}";
            _monitor = monitor;
            hr = monitor.Start();
            return hr < 0 ? $"Start 0x{hr:X8}" : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException or InvalidCastException)
        {
            return ex.Message;
        }
    }

    private void StopMonitor()
    {
        if (_monitor != null)
        {
            try { _monitor.Stop(); } catch (COMException) { }
            Marshal.ReleaseComObject(_monitor);
            _monitor = null;
        }
        _callback = null;
        if (_mfStarted)
        {
            _ = MFShutdown();
            _mfStarted = false;
        }
    }

    /// <summary>Chamado pelo Windows (em outra thread) sempre que alguma câmera começa ou para.</summary>
    private void OnReport(IMFSensorActivitiesReport report)
    {
        var pids = new HashSet<int>();
        try
        {
            report.GetCount(out var devices);
            for (uint d = 0; d < devices; d++)
            {
                report.GetActivityReport(d, out var activity);
                try
                {
                    activity.GetProcessCount(out var count);
                    for (uint i = 0; i < count; i++)
                    {
                        activity.GetProcessActivity(i, out var process);
                        try
                        {
                            process.GetProcessId(out var pid);
                            process.GetStreamingState(out var streaming);
                            // só conta quem está transmitindo (abrir sem transmitir não acende a câmera)
                            if (streaming != 0 && pid > 0) pids.Add((int)pid);
                        }
                        finally { Marshal.ReleaseComObject(process); }
                    }
                }
                finally { Marshal.ReleaseComObject(activity); }
            }
        }
        catch (COMException ex)
        {
            Core.Paths.WriteLog("Relatório da câmera: " + ex.Message);
            return;
        }
        _streamingPids = pids.ToArray();
    }

    // ---------------- reserva: registro de privacidade ----------------

    private static List<string> FromRegistry()
    {
        var apps = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var root = hive.OpenSubKey(Store);
                if (root == null) continue;
                Scan(root, apps);
                using var nonPackaged = root.OpenSubKey("NonPackaged");
                if (nonPackaged != null) Scan(nonPackaged, apps);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return apps.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Scan(RegistryKey parent, List<string> apps)
    {
        // Registros "presos": um app que travou com a câmera ligada nunca grava o fim do uso.
        // Por isso só vale o que começou depois que o PC ligou e cujo programa ainda está aberto.
        var bootUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        foreach (var name in parent.GetSubKeyNames())
        {
            if (name == "NonPackaged") continue;
            using var key = parent.OpenSubKey(name);
            if (key == null) continue;
            var start = key.GetValue("LastUsedTimeStart") is long s ? s : 0;
            var stop = key.GetValue("LastUsedTimeStop") is long e ? e : -1;
            if (start <= 0 || stop != 0) continue;
            if (DateTime.FromFileTimeUtc(start) < bootUtc) continue;
            if (name.Contains('#') && !IsRunning(name)) continue;
            apps.Add(ShortName(name));
        }
    }

    /// <summary>Apps comuns (não da Store) usam o caminho do .exe como nome da chave, com # no lugar de \.</summary>
    private static bool IsRunning(string keyName)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(keyName.Split('#').Last()));
        var running = processes.Length > 0;
        foreach (var p in processes) p.Dispose();
        return running;
    }

    // "C:#Program Files#Google#Chrome#Application#chrome.exe" -> "chrome"
    // "Microsoft.WindowsCamera_8wekyb3d8bbwe" -> "Microsoft.WindowsCamera"
    private static string ShortName(string keyName)
    {
        var last = Path.GetFileNameWithoutExtension(keyName.Split('#').Last());
        var underscore = last.IndexOf('_');
        return underscore > 0 ? last[..underscore] : last;
    }

    public void Dispose() => StopMonitor();

    // ---------------- Media Foundation (mfidl.h) ----------------

    private sealed class ReportCallback(CameraService owner) : IMFSensorActivitiesReportCallback
    {
        public int OnActivitiesReport(IMFSensorActivitiesReport report)
        {
            owner.OnReport(report);
            return 0;
        }
    }

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(uint version, uint flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFShutdown();

    [DllImport("mfsensorgroup.dll", ExactSpelling = true)]
    private static extern int MFCreateSensorActivityMonitor(IMFSensorActivitiesReportCallback callback, out IMFSensorActivityMonitor monitor);

    // Só os métodos usados têm assinatura real; os demais mantêm a ordem da vtable.
    [ComImport, Guid("DE5072EE-DBE3-46DC-8A87-B6F631194751"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivitiesReportCallback
    {
        [PreserveSig] int OnActivitiesReport(IMFSensorActivitiesReport report);
    }

    [ComImport, Guid("D0CEF145-B3F4-4340-A2E5-7A5080CA05CB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivityMonitor
    {
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
    }

    [ComImport, Guid("683F7A5E-4A19-43CD-B1A9-DBF4AB3F7777"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivitiesReport
    {
        void GetCount(out uint count);
        void GetActivityReport(uint index, out IMFSensorActivityReport report);
    }

    [ComImport, Guid("3E8C4BE1-A8C2-4528-90DE-2851BDE5FEAD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivityReport
    {
        void GetFriendlyName();
        void GetSymbolicLink();
        void GetProcessCount(out uint count);
        void GetProcessActivity(uint index, out IMFSensorProcessActivity activity);
    }

    [ComImport, Guid("39DC7F4A-B141-4719-813C-A7F46162A2B8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorProcessActivity
    {
        void GetProcessId(out uint processId);
        void GetStreamingState(out int streaming);
    }
}
