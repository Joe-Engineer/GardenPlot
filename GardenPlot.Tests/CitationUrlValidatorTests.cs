// <copyright file="CitationUrlValidatorTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Services;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #93 — defense-in-depth URL validator for citation previews. Verifies that
/// the validator rejects every threat-model class identified during the WASM migration:
/// http://, IP literals (v4 + v6), localhost variants, *.local / *.internal hostnames.
/// </summary>
public sealed class CitationUrlValidatorTests
{
    [Theory]
    [InlineData("https://en.wikipedia.org/wiki/Tomato")]
    [InlineData("https://example.com/path")]
    [InlineData("https://docs.example.org/some/page?query=1")]
    [InlineData("https://gardenplot.app/foo")]
    public void IsSafeForFetch_RegularHttpsHosts_Allowed(string url)
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch(url);
        Assert.True(allow, $"Expected allow but got reject: {reason}");
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("http://example.com/path")] // http
    [InlineData("ftp://example.com/file")]   // ftp
    [InlineData("file:///etc/passwd")]       // file
    [InlineData("javascript:alert(1)")]      // js
    [InlineData("data:text/html,<script>alert(1)</script>")] // data
    public void IsSafeForFetch_NonHttpsSchemes_Rejected(string url)
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch(url);
        Assert.False(allow);
        Assert.NotNull(reason);
    }

    [Theory]
    [InlineData("https://192.168.1.1/")]
    [InlineData("https://10.0.0.5/admin")]
    [InlineData("https://172.16.0.1/")]
    [InlineData("https://169.254.169.254/latest/meta-data")] // AWS metadata
    [InlineData("https://100.64.0.1/")]                       // CGNAT
    [InlineData("https://127.0.0.1:8080/admin")]
    [InlineData("https://8.8.8.8/")] // even public IPs rejected (IP-literal == suspicious)
    public void IsSafeForFetch_IPv4Literals_Rejected(string url)
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch(url);
        Assert.False(allow, $"Expected reject for {url}");
        Assert.Contains("IP-literal", reason!);
    }

    [Theory]
    [InlineData("https://[::1]/")]
    [InlineData("https://[fe80::1]/")]
    [InlineData("https://[fd00::1]/")]
    public void IsSafeForFetch_IPv6Literals_Rejected(string url)
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch(url);
        Assert.False(allow, $"Expected reject for {url}");
        Assert.Contains("IP-literal", reason!);
    }

    [Theory]
    [InlineData("https://localhost/")]
    [InlineData("https://localhost:5000/api")]
    [InlineData("https://LOCALHOST/")] // case-insensitive
    [InlineData("https://broadcasthost/")]
    [InlineData("https://ip6-localhost/")]
    [InlineData("https://ip6-loopback/")]
    public void IsSafeForFetch_LocalhostNames_Rejected(string url)
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch(url);
        Assert.False(allow, $"Expected reject for {url}");
        Assert.Contains("not allowed", reason!);
    }

    [Theory]
    [InlineData("https://router.local/")]
    [InlineData("https://printer.local/")]
    [InlineData("https://my-mac.local:631/")]
    [InlineData("https://corp.internal/")]
    [InlineData("https://app.localhost/")]
    [InlineData("https://server.lan/")]
    [InlineData("https://wiki.intranet/page")]
    public void IsSafeForFetch_PrivateDomainSuffixes_Rejected(string url)
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch(url);
        Assert.False(allow, $"Expected reject for {url}");
        Assert.Contains("host suffix", reason!);
    }

    [Fact]
    public void IsSafeForFetch_NullOrEmpty_Rejected()
    {
        Assert.False(CitationUrlValidator.IsSafeForFetch(null).Allow);
        Assert.False(CitationUrlValidator.IsSafeForFetch(string.Empty).Allow);
        Assert.False(CitationUrlValidator.IsSafeForFetch("   ").Allow);
    }

    [Fact]
    public void IsSafeForFetch_RelativeUrl_Rejected()
    {
        var (allow, reason) = CitationUrlValidator.IsSafeForFetch("/relative/path");
        Assert.False(allow);
        Assert.Contains("absolute", reason!);
    }

    [Fact]
    public void IsSafeForFetch_MalformedUrl_Rejected()
    {
        var (allow, _) = CitationUrlValidator.IsSafeForFetch("not a url at all");
        Assert.False(allow);
    }

    [Fact]
    public void MaxResponseBytes_IsReasonable()
    {
        // 256 KB matches the issue's proposed cap. Larger than a sensible OG-meta document,
        // small enough that an attacker URL streaming gigabytes can't OOM the browser.
        Assert.Equal(256 * 1024L, CitationUrlValidator.MaxResponseBytes);
    }
}
