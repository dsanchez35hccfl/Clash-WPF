using System.Collections.ObjectModel;
using System.Windows.Input;
using Clash_WPF.Helpers;
using Clash_WPF.Models;

namespace Clash_WPF.ViewModels;

public class RuleDisplayItem
{
    public int Index { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Proxy { get; set; } = string.Empty;
}

public class RuleViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public ObservableCollection<RuleDisplayItem> Rules { get; } = [];

    private int _ruleCount;
    public int RuleCount { get => _ruleCount; set => SetProperty(ref _ruleCount, value); }

    public ICommand RefreshCommand { get; }

    public RuleViewModel(MainViewModel main)
    {
        _main = main;
        RefreshCommand = RelayCommand.Create(async () => await LoadRulesAsync());
    }

    public override async Task ActivateAsync()
    {
        await LoadRulesAsync();
    }

    private async Task LoadRulesAsync()
    {
        var resp = await _main.Api.GetRulesAsync();
        if (resp is null) return;

        Rules.Clear();
        int idx = 1;
        foreach (var rule in resp.Rules)
        {
            Rules.Add(new RuleDisplayItem
            {
                Index = idx++,
                Type = rule.Type,
                Payload = rule.Payload,
                Proxy = rule.Proxy,
            });
        }
        RuleCount = Rules.Count;
    }
}
