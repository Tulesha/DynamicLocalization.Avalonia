using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DynamicAvaloniaLocalization;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}