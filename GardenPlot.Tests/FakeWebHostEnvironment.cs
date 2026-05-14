using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace GardenPlot.Tests;

/// <summary>Minimal IWebHostEnvironment used by service tests.</summary>
internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "GardenPlot.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Test";
}
