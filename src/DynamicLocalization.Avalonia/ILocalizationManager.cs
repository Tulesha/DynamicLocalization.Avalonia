using System.ComponentModel;

namespace DynamicLocalization.Avalonia;

/// <summary>
///     Abstraction over <see cref="LocalizationManager" />'s public surface, so hand-written code
///     (ViewModels, services) can depend on an interface instead of the concrete singleton - e.g.
///     for constructor injection via a DI container, or to substitute a fake/mock in a unit test.
/// </summary>
/// <remarks>
///     <see cref="Markup.LocalizeExtension" /> and the source generator's emitted accessors (e.g.
///     <c>PluginALocalization.WindowTitle</c>) still bind directly to
///     <see cref="LocalizationManager.Instance" />: an AXAML markup extension is instantiated by
///     the XAML loader, and a generated static property has no constructor, so neither has a seam
///     to inject into. This interface only decouples the code you write by hand.
/// </remarks>
public interface ILocalizationManager : INotifyPropertyChanged
{
    /// <summary>The locale <see cref="Get" /> and the indexer currently resolve against.</summary>
    string CurrentLocale { get; set; }

    /// <summary>The locale <see cref="Get" /> falls back to when a key is missing in <see cref="CurrentLocale" />.</summary>
    string FallbackLocale { get; set; }

    /// <summary>Indexer used by Avalonia bindings, e.g. <c>{Binding "[Module.Key]"}</c>.</summary>
    string this[string moduleDotKey] { get; }

    /// <summary>
    ///     Raised whenever a previously returned <see cref="Get" /> result may now be stale: when
    ///     <see cref="CurrentLocale" /> or <see cref="FallbackLocale" /> changes, and whenever a
    ///     module is (re-)registered.
    /// </summary>
    event EventHandler? CultureChanged;

    /// <summary>Registers (or updates) a module's localization entries.</summary>
    void RegisterModule(string module, IEnumerable<LocalizationEntry> entries);

    /// <summary>Parses a localization.json payload and registers it as <paramref name="module" />.</summary>
    void RegisterModuleFromJsonString(string module, string json);

    /// <summary>Reads a localization.json file from disk and registers it as <paramref name="module" />.</summary>
    void RegisterModuleFromJsonFile(string module, string path);

    /// <summary>
    ///     Returns the localized text for <paramref name="module" />/<paramref name="key" /> in the
    ///     current locale, falling back to <see cref="FallbackLocale" />, and finally to a visible
    ///     "[module.key]" placeholder when no translation exists.
    /// </summary>
    string Get(string module, string key);
}