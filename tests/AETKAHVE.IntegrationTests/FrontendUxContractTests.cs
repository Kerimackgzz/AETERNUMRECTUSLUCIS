using System.Net;
using AETKAHVE.IntegrationTests.Infrastructure;

namespace AETKAHVE.IntegrationTests;

public sealed class FrontendUxContractTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Public_navigation_is_a_mobile_disclosure_and_keeps_the_scroll_motion_contract()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var homeResponse = await client.GetAsync("/");
        var homeHtml = await homeResponse.Content.ReadAsStringAsync();
        var productsResponse = await client.GetAsync("/products");
        var productsHtml = await productsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
        Assert.Contains("class=\"public-layout public-layout--home\"", homeHtml, StringComparison.Ordinal);
        Assert.Contains("data-navbar-toggle", homeHtml, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"primary-navigation-menu\"", homeHtml, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", homeHtml, StringComparison.Ordinal);
        Assert.Contains("id=\"primary-navigation-menu\"", homeHtml, StringComparison.Ordinal);
        Assert.Contains("data-navbar-menu", homeHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"public-layout public-layout--inner\"", productsHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/products\" data-navbar-link aria-current=\"page\"", productsHtml, StringComparison.Ordinal);

        var css = await (await client.GetAsync("/css/components/navbar.css")).Content.ReadAsStringAsync();
        var script = await (await client.GetAsync("/js/components/navbar-motion.js")).Content.ReadAsStringAsync();

        Assert.Contains("background-color: transparent", css, StringComparison.Ordinal);
        Assert.Contains("[data-navbar].is-scrolled::before", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 959px)", css, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100dvh - 68px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains("classList.toggle(\"is-scrolled\"", script, StringComparison.Ordinal);
        Assert.Contains("navbar-brand__letter-mask", script, StringComparison.Ordinal);
        Assert.Contains("event.key === \"Escape\"", script, StringComparison.Ordinal);
        Assert.Contains("menu.inert", script, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", script, StringComparison.Ordinal);
        Assert.Contains("has-open-navigation", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commerce_fetch_helper_detects_followed_cookie_login_redirects_for_every_portal()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var response = await client.GetAsync("/js/core/commerce-api.js");
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("response.redirected", script, StringComparison.Ordinal);
        Assert.Contains("/account/login", script, StringComparison.Ordinal);
        Assert.Contains("/admin/login", script, StringComparison.Ordinal);
        Assert.Contains("/superadmin/login", script, StringComparison.Ordinal);
        Assert.Contains("loginPathForRequest", script, StringComparison.Ordinal);
        Assert.Contains("currentReturnUrl", script, StringComparison.Ordinal);
        Assert.Contains("window.location.assign(destination)", script, StringComparison.Ordinal);
        Assert.Contains("response.ok && payload !== null", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_transition_and_animation_layers_expose_reduced_motion_fallbacks()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var homeHtml = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        var transitionCss = await (await client.GetAsync("/css/core/page-transition.css")).Content.ReadAsStringAsync();
        var transitionScript = await (await client.GetAsync("/js/core/page-transition.js")).Content.ReadAsStringAsync();
        var heroCss = await (await client.GetAsync("/css/pages/home-hero.css")).Content.ReadAsStringAsync();
        var cardCss = await (await client.GetAsync("/css/components/product-card-motion.css")).Content.ReadAsStringAsync();

        Assert.Contains("data-page-transition-overlay aria-hidden=\"true\" hidden", homeHtml, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", transitionCss, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", heroCss, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", cardCss, StringComparison.Ordinal);
        Assert.Contains("matchMedia(\"(prefers-reduced-motion: reduce)\")", transitionScript, StringComparison.Ordinal);
        Assert.Contains("anchor.dataset.noTransition", transitionScript, StringComparison.Ordinal);
        Assert.Contains("data-hero-canvas aria-hidden=\"true\"", homeHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Form_feedback_and_idle_warning_keep_accessible_focus_and_error_semantics()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var addressScript = await (await client.GetAsync("/js/pages/addresses.js")).Content.ReadAsStringAsync();
        var toastScript = await (await client.GetAsync("/js/components/toast.js")).Content.ReadAsStringAsync();
        var idleScript = await (await client.GetAsync("/js/admin/idle-session.js")).Content.ReadAsStringAsync();

        Assert.Contains("form.reportValidity()", addressScript, StringComparison.Ordinal);
        Assert.Contains("toast.setAttribute(\"role\", \"alert\")", toastScript, StringComparison.Ordinal);
        Assert.Contains("focusBeforeWarning", idleScript, StringComparison.Ordinal);
        Assert.Contains("continueBtn.focus()", idleScript, StringComparison.Ordinal);
        Assert.Contains("event.key === \"Tab\"", idleScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_customer_and_admin_data_table_has_a_keyboard_accessible_responsive_region()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewRoots = new[]
        {
            Path.Combine(repositoryRoot, "src", "AETKAHVE.Web", "Views"),
            Path.Combine(repositoryRoot, "src", "AETKAHVE.Web", "Areas"),
        };
        var tableViews = viewRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file => file.Source.Contains("<table", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(tableViews);
        foreach (var view in tableViews)
        {
            Assert.Contains("class=\"table-scroll", view.Source, StringComparison.Ordinal);
            Assert.Contains("tabindex=\"0\"", view.Source, StringComparison.Ordinal);
            Assert.Contains("role=\"region\"", view.Source, StringComparison.Ordinal);
            Assert.Contains("aria-label=", view.Source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AETKAHVE.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
