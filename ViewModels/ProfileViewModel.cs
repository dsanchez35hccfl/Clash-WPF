using System.Collections.ObjectModel;
using System.Windows.Input;
using Clash_WPF.Helpers;
using Clash_WPF.Models;

namespace Clash_WPF.ViewModels;

public class ProfileDisplayItem : ViewModelBase
{
    public ProfileItem Source { get; set; } = null!;

    public string Name => Source.Name;
    public string Url => Source.Url;
    public string UpdatedText => Source.UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "从未更新";

    private bool _isActive;
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

    private bool _isUpdating;
    public bool IsUpdating { get => _isUpdating; set => SetProperty(ref _isUpdating, value); }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(UpdatedText));
    }
}

public class ProfileViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public ObservableCollection<ProfileDisplayItem> Profiles { get; } = [];

    private bool _isAddingProfile;
    public bool IsAddingProfile { get => _isAddingProfile; set => SetProperty(ref _isAddingProfile, value); }

    private string _newProfileName = string.Empty;
    public string NewProfileName { get => _newProfileName; set => SetProperty(ref _newProfileName, value); }

    private string _newProfileUrl = string.Empty;
    public string NewProfileUrl { get => _newProfileUrl; set => SetProperty(ref _newProfileUrl, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public ICommand ShowAddCommand { get; }
    public ICommand ConfirmAddCommand { get; }
    public ICommand CancelAddCommand { get; }
    public ICommand UpdateProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SelectProfileCommand { get; }

    public ProfileViewModel(MainViewModel main)
    {
        _main = main;
        ShowAddCommand = RelayCommand.Create(() =>
        {
            NewProfileName = "新订阅";
            NewProfileUrl = string.Empty;
            IsAddingProfile = true;
        });
        ConfirmAddCommand = RelayCommand.Create(async () => await AddProfileAsync());
        CancelAddCommand = RelayCommand.Create(() => IsAddingProfile = false);
        UpdateProfileCommand = new RelayCommand(async p => await UpdateProfileAsync(p));
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        SelectProfileCommand = new RelayCommand(async p => await SelectProfileAsync(p));
    }

    public override Task ActivateAsync()
    {
        RefreshList();
        return Task.CompletedTask;
    }

    private void RefreshList()
    {
        Profiles.Clear();
        var activeId = _main.ProfileMgr.Config.SelectedProfileId;
        foreach (var item in _main.ProfileMgr.Config.Profiles)
        {
            Profiles.Add(new ProfileDisplayItem
            {
                Source = item,
                IsActive = item.Id == activeId,
            });
        }
    }

    private async Task AddProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProfileUrl))
        {
            StatusMessage = "请输入订阅地址";
            return;
        }

        try
        {
            StatusMessage = "正在下载订阅...";
            var item = await _main.ProfileMgr.AddProfileAsync(NewProfileName, NewProfileUrl);
            IsAddingProfile = false;
            StatusMessage = $"订阅 \"{item.Name}\" 添加成功";
            RefreshList();

            // Auto-select the first profile, or if no profile is currently active
            if (_main.ProfileMgr.GetActiveProfile() is null)
            {
                var display = Profiles.FirstOrDefault(p => p.Source.Id == item.Id);
                if (display is not null)
                    await SelectProfileAsync(display);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"添加失败: {ex.Message}";
        }
    }

    private async Task UpdateProfileAsync(object? param)
    {
        if (param is not ProfileDisplayItem display) return;
        display.IsUpdating = true;
        try
        {
            await _main.ProfileMgr.UpdateProfileAsync(display.Source);
            display.Refresh();
            StatusMessage = $"订阅 \"{display.Name}\" 更新成功";

            // If this is the active profile, reload the core config
            if (display.IsActive)
            {
                _main.ProfileMgr.CopyActiveProfileToConfig();
                await _main.ReloadCoreConfigAsync();
                StatusMessage += "，配置已重新加载";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"更新失败: {ex.Message}";
        }
        finally
        {
            display.IsUpdating = false;
        }
    }

    private void DeleteProfile(object? param)
    {
        if (param is not ProfileDisplayItem display) return;
        _main.ProfileMgr.RemoveProfile(display.Source);
        Profiles.Remove(display);
        StatusMessage = $"已删除 \"{display.Name}\"";
    }

    private async Task SelectProfileAsync(object? param)
    {
        if (param is not ProfileDisplayItem display) return;
        _main.ProfileMgr.SelectProfile(display.Source);

        foreach (var p in Profiles) p.IsActive = false;
        display.IsActive = true;

        StatusMessage = $"正在切换到 \"{display.Name}\"，启动内核中...";
        await _main.ReloadCoreConfigAsync();
        StatusMessage = _main.IsCoreRunning
            ? $"已切换到 \"{display.Name}\""
            : $"已切换到 \"{display.Name}\"（内核未运行，请在设置中配置内核路径）";
    }
}
