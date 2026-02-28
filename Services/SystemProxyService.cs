using Microsoft.Win32;

namespace Clash_WPF.Services;

public static class SystemProxyService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public static void SetProxy(string host, int port)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
        if (key is null) return;
        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", $"{host}:{port}");
    }

    public static void ClearProxy()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
        if (key is null) return;
        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
    }

    public static bool IsProxyEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue("ProxyEnable") is int val && val == 1;
    }
}
