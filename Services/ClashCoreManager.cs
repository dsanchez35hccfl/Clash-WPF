using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace Clash_WPF.Services;

public class ClashCoreManager
{
    private readonly object _lock = new();
    private Process? _coreProcess;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
                return _coreProcess is { HasExited: false };
        }
    }

    public event Action<string>? OutputReceived;
    public event Action? CoreExited;

    /// <summary>
    /// Returns true if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Checks whether wintun.dll exists in the specified directory.
    /// </summary>
    public static bool IsWintunPresent(string coreDir)
        => File.Exists(Path.Combine(coreDir, "wintun.dll"));

    /// <summary>
    /// Downloads wintun.dll from the official source and places it in the target directory.
    /// </summary>
    public static async Task<bool> InstallWintunAsync(string targetDir)
    {
        const string url = "https://www.wintun.net/builds/wintun-0.14.1.zip";
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var zipBytes = await http.GetByteArrayAsync(url);
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new System.IO.Compression.ZipArchive(zipStream);
            var entry = archive.GetEntry("wintun/bin/amd64/wintun.dll");
            if (entry is null) return false;

            var destPath = Path.Combine(targetDir, "wintun.dll");
            using (var entryStream = entry.Open())
            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                await entryStream.CopyToAsync(fileStream);
            }
            return File.Exists(destPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes wintun.dll from the specified directory.
    /// </summary>
    public static bool UninstallWintun(string coreDir)
    {
        try
        {
            var path = Path.Combine(coreDir, "wintun.dll");
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Rename wintun.dll -> wintun.dll.disabled to disable without deleting.
    /// </summary>
    public static bool DisableWintunOnDisk(string coreDir)
    {
        try
        {
            var path = Path.Combine(coreDir, "wintun.dll");
            var disabled = Path.Combine(coreDir, "wintun.dll.disabled");
            if (File.Exists(path))
            {
                if (File.Exists(disabled)) File.Delete(disabled);
                File.Move(path, disabled);
                return !File.Exists(path) && File.Exists(disabled);
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// Rename wintun.dll.disabled -> wintun.dll to re-enable.
    /// </summary>
    public static bool EnableWintunOnDisk(string coreDir)
    {
        try
        {
            var path = Path.Combine(coreDir, "wintun.dll");
            var disabled = Path.Combine(coreDir, "wintun.dll.disabled");
            if (File.Exists(disabled))
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(disabled, path);
                return File.Exists(path) && !File.Exists(disabled);
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// Restarts the current application with administrator privileges via UAC.
    /// </summary>
    public static void RestartAsAdmin()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            });
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                System.Windows.Application.Current.Shutdown());
        }
        catch { /* user cancelled UAC */ }
    }

    /// <summary>
    /// Launches an elevated copy of this application with <c>--wintun install|uninstall dir</c>
    /// arguments.  The elevated process performs only the file operation and exits.
    /// Returns <c>true</c> when the operation succeeds (exit code 0).
    /// </summary>
    public static async Task<bool> RunElevatedWintunOpAsync(string operation, string targetDir)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--wintun {operation} \"{targetDir}\"",
                UseShellExecute = true,
                Verb = "runas",
            });
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; } // user cancelled UAC
    }

    public bool Start(string corePath, string configDir)
    {
        lock (_lock)
        {
            if (_coreProcess is { HasExited: false }) return true;

            // Dispose any stale process handle before creating a new one
            if (_coreProcess is not null)
            {
                _coreProcess.Dispose();
                _coreProcess = null;
            }

            if (!File.Exists(corePath)) return false;

            try
            {
                _coreProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = corePath,
                        Arguments = $"-d \"{configDir}\"",
                        WorkingDirectory = configDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                    EnableRaisingEvents = true,
                };

                _coreProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is not null) OutputReceived?.Invoke(e.Data);
                };
                _coreProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null) OutputReceived?.Invoke(e.Data);
                };
                _coreProcess.Exited += (_, _) => CoreExited?.Invoke();

                _coreProcess.Start();
                _coreProcess.BeginOutputReadLine();
                _coreProcess.BeginErrorReadLine();
                return true;
            }
            catch
            {
                _coreProcess?.Dispose();
                _coreProcess = null;
                return false;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_coreProcess is null) return;
            try
            {
                if (!_coreProcess.HasExited)
                {
                    _coreProcess.Kill(true);
                    _coreProcess.WaitForExit(5000);
                }
            }
            catch { /* ignore */ }
            finally
            {
                _coreProcess.Dispose();
                _coreProcess = null;
            }
        }
    }

    public void Restart(string corePath, string configDir)
    {
        Stop();
        Start(corePath, configDir);
    }
}
