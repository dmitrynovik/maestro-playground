using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using Xunit;

namespace MaestroPlayground.AppiumTests.Windows;

/// <summary>
/// WinAppDriver/Appium equivalent of the Maestro flow in
/// <c>.maestro/weight-control.yaml</c>, driving the real Windows build of the app (not the
/// Blazor Server test host used by the Playwright tests) through Windows UI Automation:
/// launch, wait for the "Weight Control" heading, toggle "Weight On"/"Weight Off", and
/// assert the read-only weight text box reflects 5/0.
/// </summary>
[Collection(WeightControlAppCollection.Name)]
public sealed class WeightControlTests
{
    // .NET MAUI cold starts (JIT + WebView2/Blazor bootstrap) can take well over 5s,
    // same rationale as the extendedWaitUntil timeout in the Maestro yaml.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(5);

    private readonly AppiumDriver _driver;

    public WeightControlTests(WeightControlAppFixture fixture)
    {
        _driver = fixture.Driver;
    }

    [Fact]
    public void WeightOnAndOff_TogglesWeightInputValue()
    {
        WaitUntilVisible(By.Name("Weight Control"), StartupTimeout);

        FindByName("Weight On").Click();
        WaitUntilElementSatisfies(
            By.Id("weightInput"),
            e => e.Text == "5" || e.GetAttribute("Value.Value") == "5",
            RenderTimeout);

        FindByName("Weight Off").Click();
        WaitUntilElementSatisfies(
            By.Id("weightInput"),
            e => e.Text == "0" || e.GetAttribute("Value.Value") == "0",
            RenderTimeout);
    }

    private AppiumElement FindByName(string accessibleName) =>
        (AppiumElement)WaitUntilVisible(By.Name(accessibleName), RenderTimeout);

    private IWebElement WaitUntilVisible(By locator, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var element = _driver.FindElement(locator);
                if (element.Displayed)
                {
                    return element;
                }
            }
            catch (Exception ex) when (ex is NoSuchElementException or WebDriverException)
            {
                lastError = ex;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException(
            $"Element located by {locator} was not visible within {timeout}.", lastError);
    }

    private void WaitUntilElementSatisfies(By locator, Func<IWebElement, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        IWebElement? last = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                last = _driver.FindElement(locator);
                if (predicate(last))
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is NoSuchElementException or WebDriverException)
            {
                lastError = ex;
            }

            Thread.Sleep(250);
        }

        var actual = last is null ? "<not found>" : $"Text='{last.Text}'";
        throw new TimeoutException(
            $"Element located by {locator} did not satisfy the expected condition within {timeout}. " +
            $"Last observed: {actual}.", lastError);
    }
}
