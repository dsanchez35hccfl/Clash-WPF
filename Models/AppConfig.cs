namespace Clash_WPF.Models;

public class AppConfig
{
    public string CorePath { get; set; } = @"core\mihomo-windows-amd64-v3.exe";
    public string ConfigDir { get; set; } = "core";
    public string ApiUrl { get; set; } = "http://127.0.0.1:9090";
    public string ApiSecret { get; set; } = string.Empty;
    public string? SelectedProfileId { get; set; }
    public bool AutoStartCore { get; set; } = true;
    public bool SetSystemProxy { get; set; }
    public int MixedPort { get; set; } = 7890;
    // Whether TUN mode is enabled (UI-controlled). If false, core will run without using TUN even
    // if wintun.dll exists on disk. This is persisted in the app config so user choice survives restarts.
    public bool TunEnabled { get; set; } = true;
    public List<ProfileItem> Profiles { get; set; } = [];
}

public class ProfileItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}
