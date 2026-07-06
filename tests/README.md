# Playwright UI tests

Browser-driven equivalent of `.maestro/weight-control.yaml`, using
[Playwright for .NET](https://playwright.dev/dotnet/) instead of Maestro.

## Why not drive the real MAUI app with Playwright?

Playwright drives browsers over CDP via a URL. The shipping app renders
`Home.razor` inside a WebView2 control with no HTTP server behind it — there's
no URL for Playwright to navigate to. Attaching Playwright to the live MAUI
process via WebView2's remote-debugging port (`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
=--remote-debugging-port=N`) is possible in principle, but fragile in practice:
it's Windows-only, requires locating/launching the real exe and its debug
port, and has no Android equivalent.

Instead, `MaestroPlayground.TestHost` hosts the exact same `Home.razor` file
(linked, not copied — see its `.csproj`) inside a minimal ASP.NET Core Blazor
Server app. This exercises the real component markup and `@code` logic, gives
Playwright a normal URL, and runs everywhere `dotnet test` runs (including
CI), at the cost of not testing the actual WebView2/MAUI shell itself. The
Maestro test remains the source of truth for that end-to-end path; this test
is a fast, low-friction check of the component's behavior.

## Projects

- `MaestroPlayground.TestHost` — minimal ASP.NET Core Blazor Server app.
  Links `..\..\Components\Pages\Home.razor` from the main project so the two
  never drift apart.
- `MaestroPlayground.PlaywrightTests` — xUnit + Microsoft.Playwright test
  project. `WeightControlAppFixture` launches the built TestHost as a child
  process on a free port and starts a shared headless Chromium instance;
  `WeightControlTests` drives it.

## Running the tests

One-time setup — install the Playwright browser binaries (Chromium) after
the first build:

```powershell
cd tests\MaestroPlayground.PlaywrightTests
dotnet build
pwsh bin\Debug\net10.0\playwright.ps1 install chromium
# No pwsh (PowerShell 7) available? Windows PowerShell works too:
powershell -File bin\Debug\net10.0\playwright.ps1 install chromium
```

Then, any time:

```powershell
dotnet test tests\MaestroPlayground.PlaywrightTests
```

This builds `MaestroPlayground.TestHost` (via project reference), launches it
on a free localhost port, and runs the Playwright assertions against it.

## Notes

- These two projects target plain `net10.0` and are excluded from the main
  `MaestroPlayground.csproj`'s default file globs (see its
  `DefaultItemExcludes`), so they don't affect the Windows/Android MAUI build.
- The fixture kills the TestHost child process (and its process tree) in
  `DisposeAsync`, so no server is left running after the test run.
