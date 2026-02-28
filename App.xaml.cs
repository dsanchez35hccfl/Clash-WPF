using System.Diagnostics;
using System.Windows;
using Clash_WPF.Services;
using Clash_WPF.ViewModels;

namespace Clash_WPF
{
    public partial class App : Application
    {
        private MainViewModel? _mainVm;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Elevated helper mode: perform wintun file operation and exit (no UI)
            if (e.Args.Length >= 2 && e.Args[0] == "--wintun")
            {
                var code = await HandleWintunOpAsync(e.Args);
                Shutdown(code);
                return;
            }

            KillExistingInstances();

            // If TUN driver is installed but we're not admin, auto-restart elevated
            if (!ClashCoreManager.IsRunningAsAdmin())
            {
                var pm = new ProfileManager();
                pm.Load();
                if (ClashCoreManager.IsWintunPresent(pm.ResolvedConfigDir))
                {
                    ClashCoreManager.RestartAsAdmin();
                    return;
                }
            }

            _mainVm = new MainViewModel();

            var window = new MainWindow();
            window.SetViewModel(_mainVm);
            window.Show();

            // Ensure core is killed even on abnormal exit (Task Manager, crash, etc.)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _mainVm?.CoreManager.Stop();

            await _mainVm.InitializeAsync();
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            _mainVm?.Cleanup();
            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mainVm?.Cleanup();
            base.OnExit(e);
        }

        private static async Task<int> HandleWintunOpAsync(string[] args)
        {
            var op = args[1];
            var dir = args.Length >= 3 ? args[2] : string.Empty;
            if (string.IsNullOrEmpty(dir)) return 1;
            try
            {
                if (op == "install")
                    return await ClashCoreManager.InstallWintunAsync(dir) ? 0 : 1;
                if (op == "uninstall")
                    return ClashCoreManager.UninstallWintun(dir) ? 0 : 1;
                if (op == "disable")
                    return ClashCoreManager.DisableWintunOnDisk(dir) ? 0 : 1;
                if (op == "enable")
                    return ClashCoreManager.EnableWintunOnDisk(dir) ? 0 : 1;
                return 1;
            }
            catch { return 1; }
        }

        private static void KillExistingInstances()
        {
            var current = Process.GetCurrentProcess();
            foreach (var proc in Process.GetProcessesByName(current.ProcessName))
            {
                if (proc.Id == current.Id)
                {
                    proc.Dispose();
                    continue;
                }
                try
                {
                    proc.Kill(true);
                    proc.WaitForExit(3000);
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }
    }
}
