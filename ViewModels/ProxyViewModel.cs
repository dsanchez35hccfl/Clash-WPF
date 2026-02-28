using System.Collections.ObjectModel;
using System.Windows.Input;
using Clash_WPF.Helpers;
using Clash_WPF.Models;

namespace Clash_WPF.ViewModels;

public class ProxyGroupItem : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    private string _now = string.Empty;
    public string Now { get => _now; set => SetProperty(ref _now, value); }

    public bool IsSelectable => Type == "Selector";

    private bool _isGroupSelected;
    public bool IsGroupSelected { get => _isGroupSelected; set => SetProperty(ref _isGroupSelected, value); }

    public ObservableCollection<ProxyNodeItem> Nodes { get; } = [];
}

public class ProxyNodeItem : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    private int _delay;
    public int Delay { get => _delay; set => SetProperty(ref _delay, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private bool _isTesting;
    public bool IsTesting { get => _isTesting; set => SetProperty(ref _isTesting, value); }
}

public class ProxyViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private bool _isSwitching;

    public ObservableCollection<ProxyGroupItem> Groups { get; } = [];

    private ProxyGroupItem? _selectedGroup;
    public ProxyGroupItem? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup is not null) _selectedGroup.IsGroupSelected = false;
            if (SetProperty(ref _selectedGroup, value) && value is not null)
                value.IsGroupSelected = true;
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SelectNodeCommand { get; }
    public ICommand TestNodeDelayCommand { get; }
    public ICommand TestAllDelayCommand { get; }

    public ProxyViewModel(MainViewModel main)
    {
        _main = main;
        RefreshCommand = RelayCommand.Create(async () => await LoadProxiesAsync());
        SelectNodeCommand = new RelayCommand(p => _ = SelectNodeAsync(p));
        TestNodeDelayCommand = new RelayCommand(p => _ = TestNodeDelayAsync(p));
        TestAllDelayCommand = RelayCommand.Create(async () => await TestAllDelayAsync());
    }

    public override async Task ActivateAsync()
    {
        await LoadProxiesAsync();
    }

    private async Task LoadProxiesAsync()
    {
        StatusMessage = "正在加载代理列表...";
        var resp = await _main.Api.GetProxiesAsync();
        if (resp is null)
        {
            var err = _main.Api.LastError ?? "未知错误";
            StatusMessage = _main.IsCoreRunning
                ? $"无法获取代理信息: {err}"
                : "内核未运行，请先在「设置」中配置内核路径，然后在「订阅」中添加并选择一个订阅";
            return;
        }

        Groups.Clear();

        foreach (var (name, proxy) in resp.Proxies)
        {
            if (proxy.Type is not ("Selector" or "URLTest" or "Fallback" or "LoadBalance"))
                continue;
            if (proxy.All is null) continue;

            var group = new ProxyGroupItem
            {
                Name = proxy.Name,
                Type = proxy.Type,
                Now = proxy.Now ?? string.Empty,
            };

            foreach (var nodeName in proxy.All)
            {
                if (!resp.Proxies.TryGetValue(nodeName, out var nodeData))
                    continue;

                var lastDelay = nodeData.History.Count > 0
                    ? nodeData.History[^1].Delay
                    : 0;

                group.Nodes.Add(new ProxyNodeItem
                {
                    Name = nodeData.Name,
                    Type = nodeData.Type,
                    Delay = lastDelay,
                    IsSelected = nodeData.Name == proxy.Now,
                });
            }

            Groups.Add(group);
        }

        SelectedGroup = Groups.FirstOrDefault();
        StatusMessage = Groups.Count > 0
            ? $"共 {Groups.Count} 个代理组"
            : "配置中没有代理组，请检查订阅内容";
    }

    private async Task SelectNodeAsync(object? param)
    {
        if (_isSwitching) return;
        if (param is not ProxyNodeItem node || SelectedGroup is null) return;
        if (!SelectedGroup.IsSelectable) return;
        if (node.IsSelected) return;

        _isSwitching = true;
        try
        {
            var success = await _main.Api.SelectProxyAsync(SelectedGroup.Name, node.Name);
            if (!success)
            {
                StatusMessage = "切换节点失败";
                return;
            }

            foreach (var n in SelectedGroup.Nodes)
                n.IsSelected = false;
            node.IsSelected = true;
            SelectedGroup.Now = node.Name;
            StatusMessage = $"已切换到 {node.Name}";
        }
        catch
        {
            StatusMessage = "切换节点时发生错误";
        }
        finally
        {
            _isSwitching = false;
        }
    }

    private async Task TestNodeDelayAsync(object? param)
    {
        if (param is not ProxyNodeItem node) return;
        node.IsTesting = true;
        try
        {
            var delay = await _main.Api.TestProxyDelayAsync(node.Name);
            node.Delay = delay ?? 0;
        }
        catch { /* ignore */ }
        finally
        {
            node.IsTesting = false;
        }
    }

    private async Task TestAllDelayAsync()
    {
        if (SelectedGroup is null) return;

        var tasks = SelectedGroup.Nodes.Select(async node =>
        {
            node.IsTesting = true;
            try
            {
                var delay = await _main.Api.TestProxyDelayAsync(node.Name);
                node.Delay = delay ?? 0;
            }
            catch { /* ignore */ }
            finally
            {
                node.IsTesting = false;
            }
        });

        await Task.WhenAll(tasks);
    }
}
