# Windows Appium UI tests

Appium/WinAppDriver equivalent of `.maestro/weight-control.yaml`, driving the real Windows
build of the app (`MaestroPlayground.exe`) instead of Maestro or the Playwright test host.

## Why a separate project from the Playwright tests

`MaestroPlayground.PlaywrightTests` exercises `Home.razor`'s markup and `@code` logic via a
plain ASP.NET Core host — fast and CI-friendly, but it never touches the actual MAUI shell,
WebView2 hosting, or Windows packaging. This project instead launches the shipping
`MaestroPlayground.exe` and drives it through Windows UI Automation, the same way the Maestro
test drives the Android APK through `adb`/UiAutomator2. It's slower and requires more local
setup (see below), but it's the only one of the three that verifies the real Windows binary.

## One-time machine setup

1. **Enable Developer Mode** — Settings > Privacy & security > For developers > Developer
   Mode: On. Required for WinAppDriver to attach to and automate other processes.

2. **Install and run WinAppDriver** — download and install
   [WinAppDriver](https://github.com/microsoft/WinAppDriver/releases) (the classic
   `WinAppDriver.exe`, not to be confused with the `appium-windows-driver` npm package — this
   project's `Appium.WebDriver` NuGet package is just the .NET client and talks to whichever
   server is listening). Start it before running tests:

   ```powershell
   & "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"
   ```

   Leave it running in its own window; it listens on `http://127.0.0.1:4723` by default. The
   fixture in this project connects to that address and does not start the service itself.

3. **Build the Windows app** from the repo root:

   ```powershell
   dotnet build -f net10.0-windows10.0.19041.0
   ```

   The fixture expects the built exe at:
   `bin\Debug\net10.0-windows10.0.19041.0\win-x64\MaestroPlayground.exe`

## Accessibility prerequisite (WebView2)

This app's UI renders inside a `BlazorWebView`/WebView2 control. WebView2 content is **not**
exposed to Windows UI Automation by default, and WinAppDriver can only see and click what UI
Automation exposes. Before the app process starts, set:

```powershell
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--force-renderer-accessibility"
```

in the **same shell** you'll run `dotnet test` from — WinAppDriver launches
`MaestroPlayground.exe` as a child of the WinAppDriver service, which inherits environment
variables from whatever process started it, so the variable needs to be set before
WinAppDriver itself is started (not just before `dotnet test`). The simplest way to guarantee
this is to set it in the same PowerShell window before starting WinAppDriver:

```powershell
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--force-renderer-accessibility"
& "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"
```

`WeightControlAppFixture` checks this environment variable at the start of the test run and
fails immediately with a clear message if it's missing, rather than surfacing a confusing
"element not found" once the test starts clicking.

Appium's Windows driver does not currently support setting arbitrary process environment
variables via capabilities (there is no `appium:environment` capability for
`automationName: Windows`), which is why this must be set out-of-band rather than in
`WeightControlAppFixture`'s `AppiumOptions`.

## Running the tests

With WinAppDriver running (with the environment variable set as above) and the app built:

```powershell
dotnet test tests\MaestroPlayground.AppiumTests.Windows
```

## Notes

- This project targets plain `net10.0` and is excluded from the main
  `MaestroPlayground.csproj`'s default item globs (see its `DefaultItemExcludes`), so it
  doesn't affect the Windows/Android MAUI build — same pattern as the Playwright/TestHost
  projects.
- Elements are located by their UI Automation accessible name/AutomationId (`By.Name`,
  `By.Id`) rather than coordinates, matching how the Maestro test locates elements by
  visible text.
- The Android counterpart to this project lives at
  `tests/MaestroPlayground.AppiumTests` and uses `appium-uiautomator2-driver` — this project
  is Windows/WinAppDriver only.
