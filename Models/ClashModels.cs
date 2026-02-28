using System.Text.Json.Serialization;

namespace Clash_WPF.Models;

public class ProxiesResponse
{
    [JsonPropertyName("proxies")]
    public Dictionary<string, ProxyData> Proxies { get; set; } = [];
}

public class ProxyData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("now")]
    public string? Now { get; set; }

    [JsonPropertyName("all")]
    public List<string>? All { get; set; }

    [JsonPropertyName("udp")]
    public bool Udp { get; set; }

    [JsonPropertyName("history")]
    public List<ProxyHistory> History { get; set; } = [];
}

public class ProxyHistory
{
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("delay")]
    public int Delay { get; set; }
}

public class ConnectionsResponse
{
    [JsonPropertyName("downloadTotal")]
    public long DownloadTotal { get; set; }

    [JsonPropertyName("uploadTotal")]
    public long UploadTotal { get; set; }

    [JsonPropertyName("connections")]
    public List<ConnectionData>? Connections { get; set; }
}

public class ConnectionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ConnectionMetadata Metadata { get; set; } = new();

    [JsonPropertyName("upload")]
    public long Upload { get; set; }

    [JsonPropertyName("download")]
    public long Download { get; set; }

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("chains")]
    public List<string> Chains { get; set; } = [];

    [JsonPropertyName("rule")]
    public string Rule { get; set; } = string.Empty;

    [JsonPropertyName("rulePayload")]
    public string RulePayload { get; set; } = string.Empty;
}

public class ConnectionMetadata
{
    [JsonPropertyName("network")]
    public string Network { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sourceIP")]
    public string SourceIP { get; set; } = string.Empty;

    [JsonPropertyName("destinationIP")]
    public string DestinationIP { get; set; } = string.Empty;

    [JsonPropertyName("sourcePort")]
    public string SourcePort { get; set; } = string.Empty;

    [JsonPropertyName("destinationPort")]
    public string DestinationPort { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("processPath")]
    public string? ProcessPath { get; set; }
}

public class RulesResponse
{
    [JsonPropertyName("rules")]
    public List<RuleData> Rules { get; set; } = [];
}

public class RuleData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("proxy")]
    public string Proxy { get; set; } = string.Empty;
}

public class ClashConfigData
{
    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("socks-port")]
    public int SocksPort { get; set; }

    [JsonPropertyName("mixed-port")]
    public int MixedPort { get; set; }

    [JsonPropertyName("allow-lan")]
    public bool AllowLan { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "rule";

    [JsonPropertyName("log-level")]
    public string LogLevel { get; set; } = "info";

    [JsonPropertyName("ipv6")]
    public bool Ipv6 { get; set; }

    [JsonPropertyName("tun")]
    public TunConfig? Tun { get; set; }
}

public class TunConfig
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("stack")]
    public string Stack { get; set; } = "gvisor";

    [JsonPropertyName("auto-route")]
    public bool AutoRoute { get; set; }
}

public class TrafficData
{
    [JsonPropertyName("up")]
    public long Up { get; set; }

    [JsonPropertyName("down")]
    public long Down { get; set; }
}

public class LogData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
}

public class DelayResponse
{
    [JsonPropertyName("delay")]
    public int Delay { get; set; }
}

public class VersionResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("meta")]
    public bool? Meta { get; set; }
}
