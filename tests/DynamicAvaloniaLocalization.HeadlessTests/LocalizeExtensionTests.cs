using Avalonia.Headless.XUnit;
using DynamicAvaloniaLocalization.HeadlessTests.Views;
using PluginA.Generated;
using Xunit;

namespace DynamicAvaloniaLocalization.HeadlessTests;

/// <summary>
///     Verifies the AXAML side of dynamic localization: {loc:Localize ...} bindings resolve
///     immediately, and refresh in an already-built control tree when the locale changes - no
///     control recreation, no app restart. A classic Avalonia Binding activates as soon as it's
///     assigned (during InitializeComponent), so a plain, unattached control is enough here - no
///     Window/TopLevel needed to exercise the binding itself.
/// </summary>
[Collection(SequentialTestCollection.Name)]
public class LocalizeExtensionTests : IDisposable
{
    public LocalizeExtensionTests()
    {
        // This fixture only ever binds to PluginA through {loc:Localize "Module.Key"} strings, so
        // nothing here has an IL-level reference that would otherwise load PluginA.dll and run
        // its [ModuleInitializer]. Force it explicitly - see LocalizationModuleLoader's remarks.
        LocalizationModuleLoader.EnsureLoaded(typeof(PluginALocalization));
        LocalizationManager.Instance.CurrentLocale = "En";
    }

    public void Dispose()
    {
        LocalizationManager.Instance.CurrentLocale = "En";
    }

    [AvaloniaFact]
    public void PathSyntax_BindsCurrentLocaleValue()
    {
        var view = new LocalizeExtensionTestView();

        Assert.Equal("Authentication A", view.WindowTitleText.Text);
    }

    [AvaloniaFact]
    public void ModuleKeySyntax_BindsCurrentLocaleValue()
    {
        var view = new LocalizeExtensionTestView();

        Assert.Equal("Welcome A", view.WelcomeText.Text);
    }

    [AvaloniaFact]
    public void Text_UpdatesAutomatically_WhenLocaleChanges_WithoutRecreatingTheControl()
    {
        var view = new LocalizeExtensionTestView();
        Assert.Equal("Authentication A", view.WindowTitleText.Text);

        LocalizationManager.Instance.CurrentLocale = "Ru";

        Assert.Equal("Авторизация A", view.WindowTitleText.Text);
    }

    [AvaloniaFact]
    public void SwitchingLocaleBackAndForth_IsFullyReversible()
    {
        var view = new LocalizeExtensionTestView();

        LocalizationManager.Instance.CurrentLocale = "Ru";
        LocalizationManager.Instance.CurrentLocale = "En";

        Assert.Equal("Authentication A", view.WindowTitleText.Text);
    }

    [AvaloniaFact]
    public void MissingModuleAndKey_ShowsBracketedFallback_AndDoesNotThrow()
    {
        var view = new LocalizeExtensionTestView();

        Assert.Equal("[NoSuchModule.NoSuchKey]", view.MissingKeyText.Text);
    }
}