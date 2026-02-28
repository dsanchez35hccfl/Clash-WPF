using System.Windows.Input;
using System.Windows.Threading;
using Clash_WPF.Helpers;
using Clash_WPF.Services;

namespace Clash_WPF.ViewModels;

public class OverviewViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _trafficCts;
    private readonly DispatcherTimer _refreshTimer;

    public bool IsCoreRunning => _main.IsCoreRunning;
    public string CoreVersion => _main.CoreVersion;

    private string _proxyMode = "rule";
    public string ProxyMode
    {
        get => _proxyMode;
        set
        {
            var old = _proxyMode;
            if (SetProperty(ref _proxyMode, value))
                _ = SetModeAsync(value, old);
        }
    }

    private bool _systemProxy;
    public bool SystemProxy
    {
        get => _systemProxy;
        set
        {
            if (SetProperty(ref _systemProxy, value))
            {
                if (value)
                    SystemProxyService.SetProxy("127.0.0.1", _main.ProfileMgr.Config.MixedPort);
                else
                    SystemProxyService.ClearProxy();
                _main.ProfileMgr.Config.SetSystemProxy = value;
                _main.ProfileMgr.Save();

                // Reset traffic counters and close all connections on toggle
                _ = ResetTrafficAsync();
            }
        }
    }

    private bool _tunEnabled;
    public bool TunEnabled
    {
        get => _tunEnabled;
        set
        {
            if (value && !CanEnableTun(out var reason))
            {
                TunStatus = reason;
                // Force UI back to unchecked (field didn't change)
                OnPropertyChanged(nameof(TunEnabled));
                return;
            }
            var old = _tunEnabled;
            if (SetProperty(ref _tunEnabled, value))
            {
                TunStatus = string.Empty;
                _ = SetTunAsync(value, old);
            }
        }
    }

    private string _tunStatus = string.Empty;
    public string TunStatus { get => _tunStatus; set => SetProperty(ref _tunStatus, value); }

    public bool IsAdmin => ClashCoreManager.IsRunningAsAdmin();

    private long _uploadSpeed;
    public long UploadSpeed { get => _uploadSpeed; set => SetProperty(ref _uploadSpeed, value); }

    private long _downloadSpeed;
    public long DownloadSpeed { get => _downloadSpeed; set => SetProperty(ref _downloadSpeed, value); }

    private long _uploadTotal;
    public long UploadTotal { get => _uploadTotal; set => SetProperty(ref _uploadTotal, value); }

    private long _downloadTotal;
    public long DownloadTotal { get => _downloadTotal; set => SetProperty(ref _downloadTotal, value); }

    private int _activeConnections;
    public int ActiveConnections { get => _activeConnections; set => SetProperty(ref _activeConnections, value); }

    private string _activeProfileName = "无";
    public string ActiveProfileName { get => _activeProfileName; set => SetProperty(ref _activeProfileName, value); }

    public ICommand RefreshCommand { get; }
    public ICommand SetModeCommand { get; }

    public OverviewViewModel(MainViewModel main)
    {
        _main = main;
        _systemProxy = false;
        RefreshCommand = RelayCommand.Create(async () => await RefreshAsync());
        SetModeCommand = new RelayCommand(p => ProxyMode = p as string ?? "rule");

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += async (_, _) => await PollConnectionsAsync();

        _main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsCoreRunning))
                OnPropertyChanged(nameof(IsCoreRunning));
            if (e.PropertyName is nameof(MainViewModel.CoreVersion))
                OnPropertyChanged(nameof(CoreVersion));
        };
    }

    public override async Task ActivateAsync()
    {
        var profile = _main.ProfileMgr.GetActiveProfile();
        ActiveProfileName = profile?.Name ?? "无";

        await RefreshAsync();
        StartTrafficStream();
        _refreshTimer.Start();
    }

    public override void Deactivate()
    {
        _trafficCts?.Cancel();
        _trafficCts = null;
        _refreshTimer.Stop();
    }

    private async Task RefreshAsync()
    {
        var config = await _main.Api.GetConfigAsync();
        if (config is not null)
        {
            _proxyMode = config.Mode;
            _tunEnabled = config.Tun?.Enable ?? false;
        }
        OnPropertyChanged(nameof(ProxyMode));
        OnPropertyChanged(nameof(TunEnabled));
    }

    private async Task SetModeAsync(string mode, string oldMode)
    {
        var success = await _main.Api.PatchConfigAsync(new Dictionary<string, object> { ["mode"] = mode });
        if (!success)
        {
            _proxyMode = oldMode;
            OnPropertyChanged(nameof(ProxyMode));
        }
    }

    private bool CanEnableTun(out string reason)
    {
        var coreDir = _main.ProfileMgr.ResolvedConfigDir;
        if (!ClashCoreManager.IsWintunPresent(coreDir))
        {
            reason = "未检测到 wintun.dll，请在「设置」页中安装 TUN 驱动";
            return false;
        }
        if (!ClashCoreManager.IsRunningAsAdmin())
        {
            reason = "TUN 模式需要管理员权限，请在「设置」页中以管理员身份重启";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private async Task SetTunAsync(bool enable, bool oldValue)
    {
        var tunPatch = new Dictionary<string, object>
        {
            ["tun"] = new Dictionary<string, object>
            {
                ["enable"] = enable,
                ["stack"] = "gvisor",
                ["auto-route"] = true
            }
        };
        var success = await _main.Api.PatchConfigAsync(tunPatch);
        if (!success)
        {
            _tunEnabled = oldValue;
            OnPropertyChanged(nameof(TunEnabled));
            TunStatus = enable ? "启用 TUN 失败，请检查内核日志" : "关闭 TUN 失败";
        }
        else
        {
            TunStatus = enable ? "TUN 已启用" : "TUN 已关闭";
        }
    }

    private async Task ResetTrafficAsync()
    {
        await _main.Api.CloseAllConnectionsAsync();
        UploadTotal = 0;
        DownloadTotal = 0;
        UploadSpeed = 0;
        DownloadSpeed = 0;
    }

    private void StartTrafficStream()
    {
        _trafficCts?.Cancel();
        _trafficCts = new CancellationTokenSource();
        var ct = _trafficCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var data in _main.Api.StreamTrafficAsync(ct))
                {
                    if (data is null) continue;
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        UploadSpeed = data.Up;
                        DownloadSpeed = data.Down;
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch { /* stream ended */ }
        }, ct);
    }

    private async Task PollConnectionsAsync()
    {
        var resp = await _main.Api.GetConnectionsAsync();
        if (resp is null) return;
        UploadTotal = resp.UploadTotal;
        DownloadTotal = resp.DownloadTotal;
        ActiveConnections = resp.Connections?.Count ?? 0;
    }
}
