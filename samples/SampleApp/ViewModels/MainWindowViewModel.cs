using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicLocalization.Avalonia;
using PluginA.Generated;

namespace SampleApp.ViewModels;

/// <summary>
///     Demonstrates the two ways to consume DynamicLocalization.Avalonia from a ViewModel:
///     - PluginA (statically referenced) via the source-generated, resx-like typed accessors.
///     - PluginB (loaded at runtime, unknown at compile time) via ILocalizationManager.Get(module, key),
///     which is the only option available for an assembly the host never compiled against.
///     Both refresh live: the constructor subscribes to ILocalizationManager.CultureChanged and
///     re-raises PropertyChanged(string.Empty), which CommunityToolkit.Mvvm treats as "every
///     property may have changed" - the standard MVVM idiom for a global locale switch.
/// </summary>
/// <remarks>
///     The <see cref="ILocalizationManager" /> is constructor-injected rather than read from
///     <see cref="LocalizationManager.Instance" /> directly, so this ViewModel can be unit-tested
///     against an isolated instance (or a fake) instead of the process-wide singleton - see
///     <c>App.axaml.cs</c> for where the DI container binds the interface to
///     <see cref="LocalizationManager.Instance" />. The generated <c>PluginALocalization</c>
///     accessors below still call <see cref="LocalizationManager.Instance" /> internally: they're
///     static, source-generated properties with no constructor to inject into, so DI can't reach
///     them - only the hand-written PluginB/locale-switching code in this class benefits.
/// </remarks>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILocalizationManager _localization;

    // --- PluginB (dynamic plugin), consumed via the module/key string API -------------------

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LoadPluginBCommand))]
    private bool _isPluginBLoaded;

    [ObservableProperty] private string? _pluginBStatus;

    [ObservableProperty] private string _userName = "Alice";

    public MainWindowViewModel(ILocalizationManager localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _localization.CultureChanged += OnCultureChanged;
    }

    // --- PluginA (static plugin), consumed via generated typed properties -------------------

    public string StaticWindowTitle => PluginALocalization.WindowTitle;

    public string StaticWelcome => PluginALocalization.Welcome;

    public string StaticSignIn => PluginALocalization.SignIn;

    public string StaticLogout => PluginALocalization.Logout;

    public string StaticGreeting => string.Format(PluginALocalization.Greeting, UserName);

    public string DynamicWindowTitle => _localization.Get("PluginB", "WindowTitle");

    public string DynamicWelcome => _localization.Get("PluginB", "Welcome");

    public string DynamicSignIn => _localization.Get("PluginB", "SignIn");

    public string DynamicLogout => _localization.Get("PluginB", "Logout");

    public string DynamicGreeting => string.Format(_localization.Get("PluginB", "Greeting"), UserName);

    // --- Locale switching ---------------------------------------------------------------------

    public string CurrentLocale => _localization.CurrentLocale;

    partial void OnUserNameChanged(string value)
    {
        OnPropertyChanged(nameof(StaticGreeting));
        OnPropertyChanged(nameof(DynamicGreeting));
    }

    [RelayCommand]
    private void ChangeLocale(string locale)
    {
        _localization.CurrentLocale = locale;
    }

    [RelayCommand(CanExecute = nameof(CanLoadPluginB))]
    private void LoadPluginB()
    {
        try
        {
            var pluginPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "PluginB.dll");
            var assembly = Assembly.LoadFrom(pluginPath);
            // Assembly.LoadFrom alone doesn't run [ModuleInitializer] - the CLR only guarantees
            // that before first access of a member in that module, which nothing here forces on
            // its own. EnsureLoaded runs it now, so PluginB is registered by the time this
            // method returns instead of on some unpredictable later access.
            LocalizationModuleLoader.EnsureLoaded(assembly);
            IsPluginBLoaded = true;
            PluginBStatus = $"PluginB loaded from \"{pluginPath}\".";
        }
        catch (Exception ex)
        {
            PluginBStatus = $"Failed to load PluginB: {ex.Message}";
        }
    }

    private bool CanLoadPluginB()
    {
        return !IsPluginBLoaded;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);
    }
}