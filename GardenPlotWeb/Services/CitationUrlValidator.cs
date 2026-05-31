// <copyright file="CitationUrlValidator.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services;

/// <summary>
/// Issue #93 — defense-in-depth URL validator for citation previews. In WASM the threat
/// model is "a malicious palette / plot import auto-fetches an attacker URL from the
/// importer's browser"; CORS blocks most cross-origin reads but the request still leaves
/// the browser and can probe localhost / LAN devices.
///
/// Rules (all must pass):
/// 1. URL parses as an absolute URI.
/// 2. Scheme is exactly <c>https</c>. No <c>http</c>, no <c>file</c>, no <c>ftp</c>, no JS pseudo-schemes.
/// 3. Host is a DNS name (not an IP literal). This blocks <c>https://192.168.1.1/</c>,
///    <c>https://169.254.169.254/</c>, and IPv6-literal targets like <c>https://[::1]/</c>.
/// 4. Host is not on the local-host shortlist: <c>localhost</c>, <c>broadcasthost</c>,
///    or anything ending in <c>.local</c>, <c>.internal</c>, <c>.localhost</c>, <c>.lan</c>.
/// </summary>
public static class CitationUrlValidator
{
    private static readonly System.Collections.Generic.HashSet<string> BlockedHosts = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "broadcasthost",
        "ip6-localhost",
        "ip6-loopback",
    };

    private static readonly string[] BlockedHostSuffixes =
    [
        ".local",
        ".internal",
        ".localhost",
        ".lan",
        ".intranet",
    ];

    /// <summary>
    /// Returns true when the URL passes every validation rule and is safe to fetch
    /// from the user's WASM HttpClient. Returns false (along with a rejection reason)
    /// when any rule fails.
    /// </summary>
    public static (bool Allow, string? Reason) IsSafeForFetch(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "empty url");
        }

        if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out System.Uri? uri))
        {
            return (false, "not an absolute URI");
        }

        if (!string.Equals(uri.Scheme, "https", System.StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"scheme '{uri.Scheme}' not allowed (https only)");
        }

        if (uri.HostNameType is System.UriHostNameType.IPv4 or System.UriHostNameType.IPv6)
        {
            return (false, "IP-literal hosts not allowed");
        }

        if (uri.HostNameType != System.UriHostNameType.Dns)
        {
            return (false, $"host type '{uri.HostNameType}' not allowed");
        }

        string host = uri.Host;
        if (BlockedHosts.Contains(host))
        {
            return (false, $"host '{host}' not allowed");
        }

        foreach (string suffix in BlockedHostSuffixes)
        {
            if (host.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"host suffix '{suffix}' not allowed");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Maximum response body size (bytes) the citation fetcher will read before giving
    /// up. Keeps memory bounded when an attacker URL streams gigabytes of HTML.
    /// </summary>
    public const long MaxResponseBytes = 256 * 1024;
}
