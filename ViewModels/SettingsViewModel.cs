using System.Windows.Input;
using Clash_WPF.Helpers;
using Clash_WPF.Services;

namespace Clash_WPF.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    private string _corePath = string.Empty;
    public string CorePath
    {
        get => _corePath;
        set => SetProperty(ref _corePath, value);
    }

    private string _configDir = string.Empty;
    public string ConfigDir
    {
        get => _configDir;
        set => SetProperty(ref _configDir, value);
    }

    private string _apiUrl = "http://127.0.0.1:9090";
    public string ApiUrl
    {
        get => _apiUrl;
        set => SetProperty(ref _apiUrl, value);
    }

    private string _apiSecret = string.Empty;
    public string ApiSecret
    {
        get => _apiSecret;
        set => SetProperty(ref _apiSecret, value);
    }

    private int _mixedPort = 7890;
    public int MixedPort
    {
        get => _mixedPort;
        set => SetProperty(ref _mixedPort, value);
    }

    private bool _autoStartCore = true;
    public bool AutoStartCore
    {
        get => _autoStartCore;
        set => SetProperty(ref _autoStartCore, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isWintunInstalled;
    public bool IsWintunInstalled
    {
        get => _isWintunInstalled;
        set => SetProperty(ref _isWintunInstalled, value);
    }

    private bool _tunEnabled;
    public bool TunEnabled
    {
        get => _tunEnabled;
        set
        {
            if (SetProperty(ref _tunEnabled, value))
            {
                // Persist change and apply immediately
                _main.ProfileMgr.Config.TunEnabled = _tunEnabled;
                _main.ProfileMgr.Save();
                // If enabling, attempt to enable on disk and start core if needed
                var coreDir = _main.ProfileMgr.ResolvedConfigDir;
                if (_tunEnabled)
                {
                    // If DLL was disabled on disk, re-enable it
                    if (!ClashCoreManager.IsWintunPresent(coreDir) &&
                        System.IO.File.Exists(System.IO.Path.Combine(coreDir, "wintun.dll.disabled")))
                    {
                        ClashCoreManager.EnableWintunOnDisk(coreDir);
                    }
                    // Start or restart core to pick up change
                    if (_main.CoreManager.IsRunning)
                    {
                        _main.StopCore();
                        Task.Delay(300).Wait();
                        _main.StartCore();
                    }
                }
                else
                {
                    // Disabling — stop core to release adapters, rename dll if present
                    if (_main.CoreManager.IsRunning)
                    {
                        _main.StopCore();
                        Task.Delay(300).Wait();
                    }
                    ClashCoreManager.DisableWintunOnDisk(coreDir);
                }
                RefreshStatus();
            }
        }
    }

    public bool IsAdmin => ClashCoreManager.IsRunningAsAdmin();

    public ICommand SaveCommand { get; }
    public ICommand BrowseCorePathCommand { get; }
    public ICommand BrowseConfigDirCommand { get; }
    public ICommand RestartCoreCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand InstallWintunCommand { get; }
    public ICommand UninstallWintunCommand { get; }
    public ICommand RestartAsAdminCommand { get; }

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        SaveCommand = RelayCommand.Create(Save);
        BrowseCorePathCommand = RelayCommand.Create(BrowseCorePath);
        BrowseConfigDirCommand = RelayCommand.Create(BrowseConfigDir);
        RestartCoreCommand = RelayCommand.Create(RestartCore);
        OpenDataFolderCommand = RelayCommand.Create(OpenDataFolder);
        InstallWintunCommand = new RelayCommand(_ => _ = InstallWintunAsync());
        UninstallWintunCommand = new RelayCommand(_ => _ = UninstallWintunAsync());
        RestartAsAdminCommand = RelayCommand.Create(RestartAsAdmin);
        // Bind TunEnabled to UI via property on this VM
        _tunEnabled = _main.ProfileMgr.Config.TunEnabled;
    }

    public override Task ActivateAsync()
    {
        var config = _main.ProfileMgr.Config;
        CorePath = config.CorePath;
        ConfigDir = config.ConfigDir;
        ApiUrl = config.ApiUrl;
        ApiSecret = config.ApiSecret;
        MixedPort = config.MixedPort;
        AutoStartCore = config.AutoStartCore;

        // Ensure UI reflects persisted TUN enabled/disabled state
        RefreshStatus();
        return Task.CompletedTask;
    }

    private void RefreshStatus()
    {
        var resolvedCore = _main.ProfileMgr.ResolvedCorePath;
        var coreExists = System.IO.File.Exists(resolvedCore);
        var coreDir = _main.ProfileMgr.ResolvedConfigDir;

        var tunExists = ClashCoreManager.IsWintunPresent(coreDir);
        var tunEnabled = _main.ProfileMgr.Config.TunEnabled;

        IsWintunInstalled = tunExists;

        var parts = new List<string>();
        parts.Add(coreExists ? $"内核文件: {resolvedCore}" : $"⚠ 内核文件不存在: {resolvedCore}");

        if (!tunExists)
            parts.Add("✗ TUN 驱动 (wintun.dll) 未安装");
        else if (tunExists && tunEnabled)
            parts.Add("✓ TUN 驱动 (wintun.dll) 已安装并启用");
        else
            parts.Add("⚠ TUN 驱动已存在但已被禁用（DLL 保留）");

        parts.Add(IsAdmin ? "✓ 管理员权限" : "✗ 非管理员运行 (TUN 模式不可用)");
        StatusMessage = string.Join("\n", parts);
    }

    private void Save()
    {
        var config = _main.ProfileMgr.Config;
        config.CorePath = CorePath;
        config.ConfigDir = ConfigDir;
        config.ApiUrl = ApiUrl;
        config.ApiSecret = ApiSecret;
        config.MixedPort = MixedPort;
        config.AutoStartCore = AutoStartCore;
        _main.ProfileMgr.Save();

        _main.Api.Configure(ApiUrl, ApiSecret);
        RefreshStatus();
    }

    private void BrowseCorePath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 Clash/Mihomo 内核文件",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
            CorePath = dialog.FileName;
    }

    private void BrowseConfigDir()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择配置文件目录",
        };

        if (dialog.ShowDialog() == true)
            ConfigDir = dialog.FolderName;
    }

    private void RestartCore()
    {
        Save();
        _main.StopCore();
        _main.StartCore();
        StatusMessage = _main.IsCoreRunning ? "内核已重启" : "内核启动失败，请检查路径";
    }

    private void OpenDataFolder()
    {
        var path = _main.ProfileMgr.AppDataDir;
        if (System.IO.Directory.Exists(path))
            System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private async Task InstallWintunAsync()
    {
        var coreDir = _main.ProfileMgr.ResolvedConfigDir;

        var tunExists = ClashCoreManager.IsWintunPresent(coreDir) || System.IO.File.Exists(System.IO.Path.Combine(coreDir, "wintun.dll.disabled"));
        if (tunExists)
        {
            // If DLL exists (normal or disabled), enable it on disk if necessary, update config and restart core
            if (!ClashCoreManager.IsWintunPresent(coreDir))
            {
                ClashCoreManager.EnableWintunOnDisk(coreDir);
            }
            _main.ProfileMgr.Config.TunEnabled = true;
            _main.ProfileMgr.Save();
            IsWintunInstalled = ClashCoreManager.IsWintunPresent(coreDir);
            StatusMessage = "✓ 已检测到 wintun.dll，已启用 TUN（未重新下载）";
            if (_main.CoreManager.IsRunning)
            {
                _main.StopCore();
                await Task.Delay(300);
                _main.StartCore();
            }
            RefreshStatus();
            return;
        }

        // Remember whether core was running so we can restart it afterwards
        var wasRunning = _main.CoreManager.IsRunning;
        if (wasRunning)
        {
            StatusMessage = "正在停止内核...";
            _main.StopCore();
            await Task.Delay(500);
        }

        // Try direct download first
        StatusMessage = "正在下载 wintun.dll ...";
        var success = await ClashCoreManager.InstallWintunAsync(coreDir);

        // If direct write failed and we're not admin, try via elevated helper
        if (!success && !IsAdmin)
        {
            StatusMessage = "需要管理员权限，正在请求提升...";
            success = await ClashCoreManager.RunElevatedWintunOpAsync("install", coreDir);
        }

        IsWintunInstalled = ClashCoreManager.IsWintunPresent(coreDir);

        if (!success || !IsWintunInstalled)
        {
            StatusMessage = "✗ 安装失败，请手动将 wintun.dll 放入内核目录";
            if (wasRunning) _main.StartCore();
            return;
        }

        // Mark TUN enabled in persisted config
        _main.ProfileMgr.Config.TunEnabled = true;
        _main.ProfileMgr.Save();

        // Restart core to pick up the new driver (no app restart needed)
        if (wasRunning)
            _main.StartCore();

        RefreshStatus();

        if (!IsAdmin)
            StatusMessage += "\n⚠ 当前非管理员运行，TUN 模式不可用。请点击「以管理员身份重启」启用 TUN。";
    }

    private async Task UninstallWintunAsync()
    {
        var coreDir = _main.ProfileMgr.ResolvedConfigDir;

        if (!ClashCoreManager.IsWintunPresent(coreDir))
        {
            IsWintunInstalled = false;
            StatusMessage = "✓ TUN 驱动未安装，无需卸载";
            return;
        }

        // Stop core to release TUN adapter before disabling TUN mode.
        var wasRunning = _main.CoreManager.IsRunning;
        if (wasRunning)
        {
            StatusMessage = "正在停止内核以释放 TUN 适配器...";
            _main.StopCore();
            await Task.Delay(500);
        }

        // Do not remove wintun.dll from disk — rename to .disabled so we can
        // re-enable later without re-downloading. Update config and restart core.
        var disabled = ClashCoreManager.DisableWintunOnDisk(coreDir);
        _main.ProfileMgr.Config.TunEnabled = false;
        _main.ProfileMgr.Save();
        IsWintunInstalled = ClashCoreManager.IsWintunPresent(coreDir);
        StatusMessage = disabled
            ? "✓ 已禁用 TUN（wintun.dll 已重命名为 wintun.dll.disabled）"
            : "✓ 已禁用 TUN（wintun.dll 保留，若文件无法重命名请手动删除或重命名）";

        // Restart core (no app restart needed)
        if (wasRunning)
            _main.StartCore();

        RefreshStatus();
    }

    /// <summary>
    /// Restarts the application. Elevates to admin if not already running as admin.
    /// </summary>
    private void RestartApp()
    {
        _main.Cleanup();
        if (IsAdmin)
        {
            // Already admin — restart without UAC prompt
            var exe = Environment.ProcessPath;
            if (exe is null) return;
            try
            {
                System.Diagnostics.Process.Start(exe);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    System.Windows.Application.Current.Shutdown());
            }
            catch { }
        }
        else
        {
            ClashCoreManager.RestartAsAdmin();
        }
    }

    private void RestartAsAdmin()
    {
        RestartApp();
    }
}
