using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace GardenPlot.Tests.E2E;

/// <summary>
/// Phase 6 smoke test — proves the testability contract (Phases 1–3) holds
/// by walking the complete takeoff workflow via Playwright.
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E")]
public class TakeoffWorkflowTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;

    public TakeoffWorkflowTests(PlaywrightFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _page = await _fixture.Browser.NewPageAsync();
        await _page.GotoAsync(_fixture.BaseUrl);
    }

    public async Task DisposeAsync() => await _page.CloseAsync();

    [Fact]
    public async Task TakeoffWorkflow_EndToEnd_UsesAllThreeContractLayers()
    {
        // ═══════════════════════════════════════════════════════════════
        // STEP 1: Phase 3 — Wait for app readiness signal
        // ═══════════════════════════════════════════════════════════════
        await _page.WaitForFunctionAsync(
            "() => document.body.dataset.appState === 'idle'",
            null,
            new() { Timeout = 30_000 });

        // ═══════════════════════════════════════════════════════════════
        // STEP 2: Phase 1 — Open takeoff panel via data-testid
        // ═══════════════════════════════════════════════════════════════
        var toggleBtn = _page.GetByTestId("takeoff-toggle-btn");
        await Expect(toggleBtn).ToBeVisibleAsync();
        await toggleBtn.ClickAsync();

        // ═══════════════════════════════════════════════════════════════
        // STEP 3: Phase 1 — Verify takeoff panel is visible
        // ═══════════════════════════════════════════════════════════════
        var addBtn = _page.GetByTestId("takeoff-add-btn");
        await Expect(addBtn).ToBeVisibleAsync();

        // ═══════════════════════════════════════════════════════════════
        // STEP 4: Phase 1 — Click "Add item" to open edit modal
        // ═══════════════════════════════════════════════════════════════
        await addBtn.ClickAsync();
        var nameInput = _page.GetByTestId("takeoff-edit-name-input");
        await Expect(nameInput).ToBeVisibleAsync();

        // ═══════════════════════════════════════════════════════════════
        // STEP 5: Phase 2 — Assert modal close button has accessible name
        // ═══════════════════════════════════════════════════════════════
        var modalCloseBtn = _page.GetByRole(AriaRole.Button, new() { Name = "Close" })
            .First;
        await Expect(modalCloseBtn).ToBeVisibleAsync();

        // ═══════════════════════════════════════════════════════════════
        // STEP 6: Phase 1 — Fill form via data-testid inputs
        // ═══════════════════════════════════════════════════════════════
        await nameInput.FillAsync("Test Plant — Smoke");
        var qtyInput = _page.GetByTestId("takeoff-edit-qty-input");
        await qtyInput.FillAsync("5");

        // ═══════════════════════════════════════════════════════════════
        // STEP 7: Phase 1+3 — Click Done, verify save cycle
        // ═══════════════════════════════════════════════════════════════
        var doneBtn = _page.GetByTestId("takeoff-edit-done-btn");
        await doneBtn.ClickAsync();

        // Phase 3: App transitions through saving → idle
        await _page.WaitForFunctionAsync(
            "() => document.body.dataset.appState === 'idle'",
            null,
            new() { Timeout = 10_000 });

        // ═══════════════════════════════════════════════════════════════
        // STEP 8: Phase 1 — Switch view mode
        // ═══════════════════════════════════════════════════════════════
        var summaryBtn = _page.GetByTestId("takeoff-view-summary-btn");
        await Expect(summaryBtn).ToBeVisibleAsync();
        await summaryBtn.ClickAsync();

        // Verify view changed (summary view should be active)
#pragma warning disable SYSLIB1045 // Simple regex in test; generated regex overhead not needed
        await Expect(summaryBtn).ToHaveAttributeAsync("class", new Regex("active"));
#pragma warning restore SYSLIB1045
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);
}
