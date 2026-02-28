using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Clash_WPF.Models;

namespace Clash_WPF.Services;

public class ClashApiService : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(5) };
    private string _baseUrl = "http://127.0.0.1:9090";

    /// <summary>
    /// Lenient options: ignore unknown properties, case-insensitive matching.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Last error message for diagnostics (shown in UI).
    /// </summary>
    public string? LastError { get; private set; }

    public void Configure(string baseUrl, string secret)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _client.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrEmpty(secret))
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secret}");
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var resp = await _client.GetAsync($"{_baseUrl}/version");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<VersionResponse?> GetVersionAsync()
        => await GetAsync<VersionResponse>("/version");

    public async Task<ProxiesResponse?> GetProxiesAsync()
        => await GetAsync<ProxiesResponse>("/proxies");

    public async Task<ProxyData?> GetProxyAsync(string name)
        => await GetAsync<ProxyData>($"/proxies/{Uri.EscapeDataString(name)}");

    public async Task<bool> SelectProxyAsync(string group, string name)
    {
        try
        {
            var content = JsonContent.Create(new { name });
            var resp = await _client.PutAsync(
                $"{_baseUrl}/proxies/{Uri.EscapeDataString(group)}", content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<int?> TestProxyDelayAsync(string name, int timeout = 5000,
        string url = "http://www.gstatic.com/generate_204")
    {
        try
        {
            var resp = await GetAsync<DelayResponse>(
                $"/proxies/{Uri.EscapeDataString(name)}/delay?timeout={timeout}&url={Uri.EscapeDataString(url)}");
            return resp?.Delay;
        }
        catch { return null; }
    }

    public async Task<ConnectionsResponse?> GetConnectionsAsync()
        => await GetAsync<ConnectionsResponse>("/connections");

    public async Task CloseAllConnectionsAsync()
    {
        try { await _client.DeleteAsync($"{_baseUrl}/connections"); }
        catch { /* ignore */ }
    }

    public async Task CloseConnectionAsync(string id)
    {
        try { await _client.DeleteAsync($"{_baseUrl}/connections/{id}"); }
        catch { /* ignore */ }
    }

    public async Task<RulesResponse?> GetRulesAsync()
        => await GetAsync<RulesResponse>("/rules");

    public async Task<ClashConfigData?> GetConfigAsync()
        => await GetAsync<ClashConfigData>("/configs");

    public async Task<bool> PatchConfigAsync(Dictionary<string, object> patch)
    {
        try
        {
            var content = JsonContent.Create(patch);
            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/configs")
            {
                Content = content
            };
            var resp = await _client.SendAsync(request);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ReloadConfigAsync(string path)
    {
        try
        {
            var content = JsonContent.Create(new { path });
            var resp = await _client.PutAsync($"{_baseUrl}/configs?force=true", content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async IAsyncEnumerable<TrafficData?> StreamTrafficAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        HttpResponseMessage? resp = null;
        try
        {
            resp = await _client.GetAsync($"{_baseUrl}/traffic",
                HttpCompletionOption.ResponseHeadersRead, ct);
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                TrafficData? data = null;
                try { data = JsonSerializer.Deserialize<TrafficData>(line); }
                catch { /* skip malformed */ }
                if (data is not null) yield return data;
            }
        }
        finally
        {
            resp?.Dispose();
        }
    }

    public async IAsyncEnumerable<LogData?> StreamLogsAsync(string level = "info",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        HttpResponseMessage? resp = null;
        try
        {
            resp = await _client.GetAsync($"{_baseUrl}/logs?level={level}",
                HttpCompletionOption.ResponseHeadersRead, ct);
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                LogData? data = null;
                try { data = JsonSerializer.Deserialize<LogData>(line); }
                catch { /* skip malformed */ }
                if (data is not null) yield return data;
            }
        }
        finally
        {
            resp?.Dispose();
        }
    }

    private async Task<T?> GetAsync<T>(string path)
    {
        try
        {
            var json = await _client.GetStringAsync($"{_baseUrl}{path}");
            LastError = null;
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            LastError = $"{path}: {ex.GetType().Name} - {ex.Message}";
            return default;
        }
    }

    public void Dispose() => _client.Dispose();
}
