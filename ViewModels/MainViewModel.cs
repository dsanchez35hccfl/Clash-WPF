using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Clash_WPF.Helpers;
using Clash_WPF.Models;
using Clash_WPF.Services;

namespace Clash_WPF.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ClashApiService _api;
    private readonly ClashCoreManager _coreManager;
    private readonly ProfileManager _profileManager;
    private readonly DispatcherTimer _statusTimer;

    public ClashApiService Api => _api;
    public ClashCoreManager CoreManager => _coreManager;
    public ProfileManager ProfileMgr => _profileManager;

    private ViewModelBase _currentPage = null!;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private bool _isCoreRunning;
    public bool IsCoreRunning
    {
        get => _isCoreRunning;
        set => SetProperty(ref _isCoreRunning, value);
    }

    private string _coreVersion = "未知";
    public string CoreVersion
    {
        get => _coreVersion;
        set => SetProperty(ref _coreVersion, value);
    }

    public OverviewViewModel OverviewVM { get; }
    public ProxyViewModel ProxyVM { get; }
    public ProfileViewModel ProfileVM { get; }
    public ConnectionViewModel ConnectionVM { get; }
    public RuleViewModel RuleVM { get; }
    public LogViewModel LogVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public ICommand NavigateCommand { get; }
    public ICommand StartCoreCommand { get; }
    public ICommand StopCoreCommand { get; }

    public MainViewModel()
    {
        _api = new ClashApiService();
        _coreManager = new ClashCoreManager();
        _profileManager = new ProfileManager();
        _profileManager.Load();

        _api.Configure(_profileManager.Config.ApiUrl, _profileManager.Config.ApiSecret);

        OverviewVM = new OverviewViewModel(this);
        ProxyVM = new ProxyViewModel(this);
        ProfileVM = new ProfileViewModel(this);
        ConnectionVM = new ConnectionViewModel(this);
        RuleVM = new RuleViewModel(this);
        LogVM = new LogViewModel(this);
        SettingsVM = new SettingsViewModel(this);

        _currentPage = OverviewVM;

        NavigateCommand = new RelayCommand(Navigate);
        StartCoreCommand = RelayCommand.Create(StartCore);
        StopCoreCommand = RelayCommand.Create(StopCore);

        _coreManager.CoreExited += () =>
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsCoreRunning = false;
            });
        };

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += async (_, _) => await CheckCoreStatusAsync();
        _statusTimer.Start();
    }

    public async Task InitializeAsync()
    {
        // Always start with system proxy disabled
        SystemProxyService.ClearProxy();
        _profileManager.Config.SetSystemProxy = false;
        _profileManager.Save();

        // Ensure config.yaml exists before attempting to start the core
        _profileManager.EnsureMinimalConfig();

        if (_profileManager.Config.AutoStartCore &&
            !string.IsNullOrEmpty(_profileManager.Config.CorePath))
        {
            StartCore();
            await WaitForCoreApiAsync();
        }

        await CheckCoreStatusAsync();
        await OverviewVM.ActivateAsync();
    }

    private async void Navigate(object? param)
    {
        if (param is not string page) return;

        CurrentPage.Deactivate();

        CurrentPage = page switch
        {
            "Overview" => OverviewVM,
            "Proxies" => ProxyVM,
            "Profiles" => ProfileVM,
            "Connections" => ConnectionVM,
            "Rules" => RuleVM,
            "Logs" => LogVM,
            "Settings" => SettingsVM,
            _ => OverviewVM,
        };

        await CurrentPage.ActivateAsync();
    }

    private bool _isReloading;

    public void StartCore()
    {
        if (_isReloading) return; // Reload in progress, let it handle start

        var corePath = _profileManager.ResolvedCorePath;
        var configDir = _profileManager.ResolvedConfigDir;

        if (string.IsNullOrEmpty(corePath)) return;

        _coreManager.Start(corePath, configDir);
        IsCoreRunning = _coreManager.IsRunning;
    }

    public void StopCore()
    {
        if (_isReloading) return; // Reload in progress, don't interfere

        _coreManager.Stop();
        IsCoreRunning = false;
    }

    private async Task CheckCoreStatusAsync()
    {
        var available = await _api.IsAvailableAsync();
        IsCoreRunning = available || _coreManager.IsRunning;

        if (available)
        {
            var ver = await _api.GetVersionAsync();
            if (ver is not null)
                CoreVersion = ver.Version;
        }
    }

    /// <summary>
    /// Copies the active profile to config.yaml (with managed settings injected),
    /// then restarts the core process to apply the new config.
    /// Only one reload runs at a time; concurrent calls are silently skipped.
    /// </summary>
    public async Task ReloadCoreConfigAsync()
    {
        if (_isReloading) return;
        _isReloading = true;
        try
        {
            var profile = _profileManager.GetActiveProfile();
            if (profile is null) return;

            // Copy profile → config.yaml with external-controller / mixed-port injected
            _profileManager.CopyActiveProfileToConfig();

            // Always restart the core — API reload (PUT /configs) is unreliable
            // across Mihomo versions and can silently fail.
            _coreManager.Stop();
            IsCoreRunning = false;
            await Task.Delay(500); // brief pause to release ports

            var corePath = _profileManager.ResolvedCorePath;
            var configDir = _profileManager.ResolvedConfigDir;
            if (!string.IsNullOrEmpty(corePath))
            {
                _coreManager.Start(corePath, configDir);
                IsCoreRunning = _coreManager.IsRunning;
            }

            await WaitForCoreApiAsync();
            await CheckCoreStatusAsync();
        }
        finally
        {
            _isReloading = false;
        }
    }

    /// <summary>
    /// Polls the Clash API until it responds or the timeout elapses.
    /// </summary>
    private async Task WaitForCoreApiAsync(int maxWaitMs = 15000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < maxWaitMs)
        {
            if (await _api.IsAvailableAsync()) return;
            await Task.Delay(300);
        }
    }

    private bool _cleaned;
    public void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;

        _statusTimer.Stop();
        CurrentPage?.Deactivate();

        if (_profileManager.Config.SetSystemProxy)
            SystemProxyService.ClearProxy();

        _coreManager.Stop();
        _api.Dispose();
    }
}
