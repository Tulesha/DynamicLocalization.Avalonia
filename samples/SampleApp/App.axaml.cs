using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DynamicLocalization.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using PluginA.Generated;
using SampleApp.ViewModels;
using SampleApp.Views;

namespace SampleApp;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // PluginA is statically referenced, but its [ModuleInitializer] only runs lazily, on
        // first access of a member in its module. This app's MainWindow.axaml binds to PluginA
        // purely through {loc:Localize "PluginA.Key"} strings, which carry no IL-level reference
        // to PluginA.dll - so without this explicit nudge, that section would show fallback
        // "[PluginA.Key]" placeholders until something else (e.g. the ViewModel) happened to
        // touch PluginA first.
        LocalizationModuleLoader.EnsureLoaded(typeof(PluginALocalization));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = ConfigureServices();

            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Binds <see cref="ILocalizationManager" /> to the process-wide
    ///     <see cref="LocalizationManager.Instance" /> singleton - AXAML bindings
    ///     (<c>{loc:Localize ...}</c>) and the generated <c>PluginALocalization</c> accessors are
    ///     hardwired to that same instance (see <see cref="ILocalizationManager" />'s remarks), so
    ///     registering anything else here would just split the app into two disconnected locales.
    ///     What DI buys you instead: <see cref="MainWindowViewModel" /> depends on the interface,
    ///     not the concrete singleton, so it can be constructed in a unit test with a fake/isolated
    ///     <see cref="ILocalizationManager" /> instead of touching global state.
    /// </summary>
    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILocalizationManager>(LocalizationManager.Instance);
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}