using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Clash_WPF.Models;

namespace Clash_WPF.Services;

public class ProfileManager
{
    private readonly HttpClient _http = new();
    private AppConfig _config = new();

    /// <summary>
    /// Application exe directory — all relative paths are resolved against this.
    /// </summary>
    public static string AppBaseDir { get; } =
        AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>
    /// App settings JSON file (stored alongside the core).
    /// </summary>
    public string ConfigFilePath => Path.Combine(ResolvedConfigDir, "clash-wpf.json");

    /// <summary>
    /// Directory where downloaded subscription YAMLs are stored.
    /// </summary>
    public string ProfileDir => Path.Combine(ResolvedConfigDir, "profiles");

    /// <summary>
    /// The resolved (absolute) core config directory.
    /// </summary>
    public string ResolvedConfigDir => ResolvePath(_config.ConfigDir);

    /// <summary>
    /// The resolved (absolute) path to the Mihomo executable.
    /// </summary>
    public string ResolvedCorePath => ResolvePath(_config.CorePath);

    /// <summary>
    /// Exposed for SettingsViewModel "open data folder".
    /// </summary>
    public string AppDataDir => ResolvedConfigDir;

    public AppConfig Config => _config;

    public ProfileManager()
    {
    }

    /// <summary>
    /// Resolves a potentially relative path against <see cref="AppBaseDir"/>.
    /// </summary>
    public static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return AppBaseDir;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(AppBaseDir, path));
    }

    public void Load()
    {
        // Ensure directories exist
        Directory.CreateDirectory(ResolvedConfigDir);
        Directory.CreateDirectory(ProfileDir);

        if (!File.Exists(ConfigFilePath)) return;
        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            _config = new AppConfig();
        }

        // Re-create directories in case config changed them
        Directory.CreateDirectory(ResolvedConfigDir);
        Directory.CreateDirectory(ProfileDir);
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigFilePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public async Task<ProfileItem> AddProfileAsync(string name, string url)
    {
        var id = Guid.NewGuid().ToString("N");
        var item = new ProfileItem
        {
            Id = id,
            Name = name,
            Url = url,
            FileName = $"{id}.yaml",
        };

        await DownloadProfileAsync(item);
        _config.Profiles.Add(item);
        Save();
        return item;
    }

    public async Task UpdateProfileAsync(ProfileItem item)
    {
        await DownloadProfileAsync(item);
        Save();
    }

    public void RemoveProfile(ProfileItem item)
    {
        var path = GetProfilePath(item);
        if (File.Exists(path)) File.Delete(path);
        _config.Profiles.Remove(item);
        if (_config.SelectedProfileId == item.Id)
            _config.SelectedProfileId = null;
        Save();
    }

    public void SelectProfile(ProfileItem item)
    {
        _config.SelectedProfileId = item.Id;
        CopyActiveProfileToConfig();
        Save();
    }

    public ProfileItem? GetActiveProfile()
        => _config.Profiles.FirstOrDefault(p => p.Id == _config.SelectedProfileId);

    public string GetProfilePath(ProfileItem item)
        => Path.Combine(ProfileDir, item.FileName);

    /// <summary>
    /// Gets the path to the runtime config.yaml used by the Clash core.
    /// </summary>
    public string GetRuntimeConfigPath()
        => Path.Combine(ResolvedConfigDir, "config.yaml");

    /// <summary>
    /// Copies the active profile to config.yaml in the config directory,
    /// injecting required settings (external-controller, mixed-port, secret)
    /// so the Clash REST API is always accessible.
    /// </summary>
    public bool CopyActiveProfileToConfig()
    {
        var active = GetActiveProfile();
        if (active is null) return false;

        var sourcePath = GetProfilePath(active);
        if (!File.Exists(sourcePath)) return false;

        Directory.CreateDirectory(ResolvedConfigDir);
        var targetPath = GetRuntimeConfigPath();

        var lines = File.ReadAllLines(sourcePath).ToList();

        // Remove existing lines for settings we manage, to avoid duplicates
        lines.RemoveAll(line =>
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("external-controller:")
                || trimmed.StartsWith("secret:")
                || trimmed.StartsWith("mixed-port:")
                || trimmed.StartsWith("# --- Clash-WPF managed");
        });

        // Append managed settings — external-controller is required for API access
        lines.Add("");
        lines.Add("# --- Clash-WPF managed settings ---");

        if (Uri.TryCreate(_config.ApiUrl, UriKind.Absolute, out var apiUri))
            lines.Add($"external-controller: {apiUri.Host}:{apiUri.Port}");
        else
            lines.Add("external-controller: 127.0.0.1:9090");

        if (!string.IsNullOrEmpty(_config.ApiSecret))
            lines.Add($"secret: \"{_config.ApiSecret}\"");

        if (_config.MixedPort > 0)
            lines.Add($"mixed-port: {_config.MixedPort}");

        File.WriteAllLines(targetPath, lines);
        return true;
    }

    /// <summary>
    /// Ensures a minimal config.yaml exists even if no profile is active,
    /// so the core can at least start and expose the API.
    /// </summary>
    public void EnsureMinimalConfig()
    {
        var targetPath = GetRuntimeConfigPath();
        if (File.Exists(targetPath)) return;

        // If there is an active profile, copy it
        if (CopyActiveProfileToConfig()) return;

        // Otherwise create a bare-minimum config
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var sb = new StringBuilder();
        sb.AppendLine("# Clash-WPF minimal config");

        if (Uri.TryCreate(_config.ApiUrl, UriKind.Absolute, out var apiUri))
            sb.AppendLine($"external-controller: {apiUri.Host}:{apiUri.Port}");
        else
            sb.AppendLine("external-controller: 127.0.0.1:9090");

        if (!string.IsNullOrEmpty(_config.ApiSecret))
            sb.AppendLine($"secret: \"{_config.ApiSecret}\"");

        if (_config.MixedPort > 0)
            sb.AppendLine($"mixed-port: {_config.MixedPort}");

        sb.AppendLine("mode: rule");
        sb.AppendLine("log-level: info");

        File.WriteAllText(targetPath, sb.ToString());
    }

    private async Task DownloadProfileAsync(ProfileItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Url)) return;

        var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
        request.Headers.Add("User-Agent", "Clash-WPF/1.0");

        var resp = await _http.SendAsync(request);
        resp.EnsureSuccessStatusCode();

        var content = await resp.Content.ReadAsStringAsync();
        Directory.CreateDirectory(ProfileDir);
        var path = GetProfilePath(item);
        await File.WriteAllTextAsync(path, content);
        item.UpdatedAt = DateTime.Now;

        // Try to get name from Content-Disposition or subscription-userinfo
        if (string.IsNullOrEmpty(item.Name) || item.Name == "新订阅")
        {
            var disposition = resp.Content.Headers.ContentDisposition;
            if (disposition?.FileName is { Length: > 0 } fn)
                item.Name = fn.Trim('"');
        }
    }
}
