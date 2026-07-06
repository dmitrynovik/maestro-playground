using Microsoft.Playwright;
using Xunit;

namespace MaestroPlayground.PlaywrightTests;

/// <summary>
/// Browser-driven equivalent of the Maestro flow in <c>.maestro/weight-control.yaml</c>:
/// launch, assert the heading, toggle "Weight On"/"Weight Off", and assert the read-only
/// weight text box reflects 5/0. Runs against the same <c>Home.razor</c> component, hosted
/// via <see cref="WeightControlAppFixture"/> instead of the WebView2-hosted MAUI shell.
/// </summary>
public sealed class WeightControlTests : IClassFixture<WeightControlAppFixture>
{
    private readonly WeightControlAppFixture _fixture;

    public WeightControlTests(WeightControlAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task WeightOnAndOff_TogglesWeightInputValue()
    {
        var page = await _fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(_fixture.ServerAddress, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
            });

            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Weight Control" }))
                .ToBeVisibleAsync();

            var weightInput = page.Locator("#weightInput");

            await page.GetByRole(AriaRole.Button, new() { Name = "Weight On" }).ClickAsync();
            await Assertions.Expect(weightInput).ToHaveValueAsync("5");

            await page.GetByRole(AriaRole.Button, new() { Name = "Weight Off" }).ClickAsync();
            await Assertions.Expect(weightInput).ToHaveValueAsync("0");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
