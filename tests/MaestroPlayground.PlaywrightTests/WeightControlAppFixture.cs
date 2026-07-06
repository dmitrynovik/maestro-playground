using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace MaestroPlayground.PlaywrightTests;

/// <summary>
/// Launches the <c>MaestroPlayground.TestHost</c> Blazor app as a real child process on a
/// free TCP port and starts a shared Playwright/Chromium instance for the test run.
/// </summary>
/// <remarks>
/// The shipping MAUI app renders <c>Home.razor</c> inside a WebView2 control with no HTTP
/// server behind it, so Playwright (which drives browsers over CDP via a URL) can't attach
/// to it directly without enabling WebView2 remote debugging and locating the live MAUI
/// process/port — fragile, Windows-only, and awkward to automate in CI. Instead, this
/// fixture runs the exact same <c>Home.razor</c> file (linked, not copied) inside a minimal
/// ASP.NET Core Blazor Server app, giving Playwright a normal URL to navigate to while still
/// exercising the real component markup and @code logic.
///
/// The host runs out-of-process (rather than in-process via <c>WebApplication</c> directly)
/// so that ASP.NET Core's static web assets discovery — which serves
/// <c>_framework/blazor.web.js</c> — resolves against the TestHost's own entry assembly
/// instead of the `dotnet test` runner's.
/// </remarks>
public sealed class WeightControlAppFixture : IAsyncLifetime
{
    private const string TestHostAssemblyRelativePath =
        @"..\..\..\..\MaestroPlayground.TestHost\bin\{0}\net10.0\MaestroPlayground.TestHost.dll";

    private Process? _process;

    public string ServerAddress { get; private set; } = string.Empty;

    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var port = GetFreeTcpPort();
        ServerAddress = $"http://127.0.0.1:{port}";

        var testHostDll = ResolveTestHostAssemblyPath();

        _process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{testHostDll}\" --urls {ServerAddress}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Failed to start MaestroPlayground.TestHost process.");

        await WaitUntilRespondingAsync(ServerAddress, TimeSpan.FromSeconds(30));

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        // Uses the system-installed, Microsoft-signed Edge instead of downloading
        // Playwright's own Chromium build, which corporate antivirus/EDR software
        // commonly quarantines as an unsigned executable.
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = false,
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        Playwright?.Dispose();

        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();
    }

    private static string ResolveTestHostAssemblyPath()
    {
        var baseDir = AppContext.BaseDirectory;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var path = Path.GetFullPath(Path.Combine(baseDir, string.Format(TestHostAssemblyRelativePath, configuration)));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Could not find the built MaestroPlayground.TestHost assembly at '{path}'. " +
                "Build the MaestroPlayground.TestHost project (matching configuration) before running these tests.",
                path);
        }

        return path;
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilRespondingAsync(string address, TimeSpan timeout)
    {
        using var client = new HttpClient();
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(address, cts.Token);
                return;
            }
            catch
            {
                await Task.Delay(200, CancellationToken.None);
            }
        }

        throw new TimeoutException($"MaestroPlayground.TestHost did not respond at '{address}' within {timeout}.");
    }
}
