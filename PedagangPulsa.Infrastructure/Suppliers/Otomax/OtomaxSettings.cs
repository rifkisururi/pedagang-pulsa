using System.Net;

namespace PedagangPulsa.Infrastructure.Suppliers.Otomax;

public class OtomaxSettings
{
    public const string SectionName = "Otomax";

    /// <summary>
    /// IP address of the proxy server (e.g., "103.152.73.10")
    /// </summary>
    public string? ProxyHost { get; set; }

    /// <summary>
    /// Proxy port (e.g., 8080)
    /// </summary>
    public int ProxyPort { get; set; } = 8080;

    /// <summary>
    /// Optional proxy username for authentication
    /// </summary>
    public string? ProxyUsername { get; set; }

    /// <summary>
    /// Optional proxy password for authentication
    /// </summary>
    public string? ProxyPassword { get; set; }

    /// <summary>
    /// Whether proxy is enabled
    /// </summary>
    public bool UseProxy { get; set; } = false;

    /// <summary>
    /// Creates an HttpClientHandler configured with proxy settings
    /// </summary>
    public HttpMessageHandler? CreateHttpMessageHandler()
    {
        if (!UseProxy || string.IsNullOrWhiteSpace(ProxyHost))
        {
            return null;
        }

        var proxy = new WebProxy(new Uri($"http://{ProxyHost}:{ProxyPort}"));

        if (!string.IsNullOrWhiteSpace(ProxyUsername))
        {
            proxy.Credentials = new NetworkCredential(ProxyUsername, ProxyPassword);
        }

        return new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true
        };
    }
}
