using System;
using System.IO;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;

namespace MaestroPlayground.AppiumTests.Windows;

/// <summary>
/// Launches the real Windows build of the MAUI app via WinAppDriver (through Appium's
/// Windows driver) so <see cref="WeightControlTests"/> can drive it with UI Automation,
/// the same way <c>.maestro/weight-control.yaml</c> drives the Android build with Maestro.
/// </summary>
/// <remarks>
/// Requires WinAppDriver.exe to already be running at <see cref="WinAppDriverUrl"/> — see
/// the project README for one-time setup (Developer Mode, WinAppDriver install/start,
/// building this app for <c>net10.0-windows10.0.19041.0</c>).
///
/// The app's UI lives inside a BlazorWebView/WebView2 control. WebView2 content is not
/// exposed to Windows UI Automation (and therefore not visible to WinAppDriver) unless the
/// WebView2 process is started with <c>--force-renderer-accessibility</c>. This app does not
/// set that itself, so it must be set in the environment this test (and the app process it
/// launches) run in — see the README's "Accessibility prerequisite" section. This fixture
/// fails fast with a clear message if the flag looks unset, rather than leaving the caller
/// to puzzle over an inexplicable "element not found".
/// </remarks>
public sealed class WeightControlAppFixture : IDisposable
{
    private const string WinAppDriverUrl = "http://127.0.0.1:4723";

    private const string AppExeRelativePath =
        @"..\..\..\..\..\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MaestroPlayground.exe";

    private const string AccessibilityEnvVar = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
    private const string AccessibilityArg = "--force-renderer-accessibility";

    public WindowsDriver Driver { get; }

    public WeightControlAppFixture()
    {
        var envValue = Environment.GetEnvironmentVariable(AccessibilityEnvVar);
        if (envValue is null || !envValue.Contains(AccessibilityArg, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Environment variable '{AccessibilityEnvVar}' must contain '{AccessibilityArg}' " +
                "before this process starts, so the app's WebView2 control exposes its DOM to " +
                "Windows UI Automation. Set it in the shell that launches `dotnet test` and retry " +
                "(see tests/MaestroPlayground.AppiumTests.Windows/README.md).");
        }

        var appPath = ResolveAppExePath();

        var options = new AppiumOptions
        {
            PlatformName = "Windows",
            AutomationName = "Windows",
            App = appPath,
            DeviceName = "WindowsPC",
        };

        try
        {
            Driver = new WindowsDriver(new Uri(WinAppDriverUrl), options, TimeSpan.FromSeconds(60));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not connect to WinAppDriver at {WinAppDriverUrl}. Make sure WinAppDriver.exe " +
                "is installed and running (see tests/MaestroPlayground.AppiumTests.Windows/README.md).",
                ex);
        }
    }

    public void Dispose()
    {
        try
        {
            Driver.Quit();
        }
        catch
        {
            // Best-effort cleanup; the app may have already exited (e.g. asserted and closed itself).
        }

        Driver.Dispose();
    }

    private static string ResolveAppExePath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, AppExeRelativePath));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Could not find the built Windows app at '{path}'. Build it first with " +
                "`dotnet build -f net10.0-windows10.0.19041.0` from the repo root.",
                path);
        }

        return path;
    }
}

[CollectionDefinition(Name)]
public sealed class WeightControlAppCollection : ICollectionFixture<WeightControlAppFixture>
{
    public const string Name = "WeightControlApp";
}
