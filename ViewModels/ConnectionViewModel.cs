using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Clash_WPF.Helpers;
using Clash_WPF.Models;

namespace Clash_WPF.ViewModels;

public class ConnectionDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Chains { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public long Download { get; set; }
    public long Upload { get; set; }
    public string DownloadText { get; set; } = string.Empty;
    public string UploadText { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
}

public class ConnectionViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<ConnectionDisplayItem> Connections { get; } = [];

    private int _connectionCount;
    public int ConnectionCount { get => _connectionCount; set => SetProperty(ref _connectionCount, value); }

    private string _totalDownload = "0 B";
    public string TotalDownload { get => _totalDownload; set => SetProperty(ref _totalDownload, value); }

    private string _totalUpload = "0 B";
    public string TotalUpload { get => _totalUpload; set => SetProperty(ref _totalUpload, value); }

    public ICommand RefreshCommand { get; }
    public ICommand CloseConnectionCommand { get; }
    public ICommand CloseAllCommand { get; }

    public ConnectionViewModel(MainViewModel main)
    {
        _main = main;
        RefreshCommand = RelayCommand.Create(async () => await LoadConnectionsAsync());
        CloseConnectionCommand = new RelayCommand(async p => await CloseConnectionAsync(p));
        CloseAllCommand = RelayCommand.Create(async () =>
        {
            await _main.Api.CloseAllConnectionsAsync();
            await LoadConnectionsAsync();
        });

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += async (_, _) => await LoadConnectionsAsync();
    }

    public override async Task ActivateAsync()
    {
        await LoadConnectionsAsync();
        _refreshTimer.Start();
    }

    public override void Deactivate()
    {
        _refreshTimer.Stop();
    }

    private async Task LoadConnectionsAsync()
    {
        var resp = await _main.Api.GetConnectionsAsync();
        if (resp is null) return;

        TotalDownload = FormatBytes(resp.DownloadTotal);
        TotalUpload = FormatBytes(resp.UploadTotal);

        var items = resp.Connections?
            .OrderByDescending(c => c.Start)
            .Select(c => new ConnectionDisplayItem
            {
                Id = c.Id,
                Host = string.IsNullOrEmpty(c.Metadata.Host)
                    ? $"{c.Metadata.DestinationIP}:{c.Metadata.DestinationPort}"
                    : $"{c.Metadata.Host}:{c.Metadata.DestinationPort}",
                Network = c.Metadata.Network.ToUpperInvariant(),
                Type = c.Metadata.Type,
                Chains = string.Join(" → ", c.Chains),
                Rule = string.IsNullOrEmpty(c.RulePayload)
                    ? c.Rule
                    : $"{c.Rule}({c.RulePayload})",
                Download = c.Download,
                Upload = c.Upload,
                DownloadText = FormatBytes(c.Download),
                UploadText = FormatBytes(c.Upload),
                StartTime = c.Start.ToLocalTime().ToString("HH:mm:ss"),
                Process = c.Metadata.ProcessPath ?? string.Empty,
            })
            .ToList() ?? [];

        ConnectionCount = items.Count;
        Connections.Clear();
        foreach (var item in items)
            Connections.Add(item);
    }

    private async Task CloseConnectionAsync(object? param)
    {
        if (param is not ConnectionDisplayItem item) return;
        await _main.Api.CloseConnectionAsync(item.Id);
        Connections.Remove(item);
        ConnectionCount = Connections.Count;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double val = bytes;
        int i = 0;
        while (val >= 1024 && i < units.Length - 1)
        {
            val /= 1024;
            i++;
        }
        return $"{val:F1} {units[i]}";
    }
}
