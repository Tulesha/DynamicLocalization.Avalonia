using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace DynamicLocalization.Avalonia.Markup;

/// <summary>
///     AXAML markup extension for dynamic localization, e.g.
///     <c>Text="{loc:Localize PluginA.WindowTitle}"</c> or
///     <c>Text="{loc:Localize Module=PluginA, Key=WindowTitle}"</c>.
///     Produces a one-way binding to <see cref="LocalizationManager.Instance" />'s indexer, so the
///     bound value refreshes automatically whenever the current locale changes.
/// </summary>
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string path)
    {
        Path = path;
    }

    /// <summary>Combined "Module.Key" form, used for the positional constructor argument.</summary>
    [ConstructorArgument("path")]
    public string? Path { get; set; }

    public string? Module { get; set; }

    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var (module, key) = ResolveModuleAndKey();

        return new Binding($"[{module}.{key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };
    }

    private (string Module, string Key) ResolveModuleAndKey()
    {
        if (!string.IsNullOrEmpty(Path))
        {
            var separatorIndex = Path.IndexOf('.');
            if (separatorIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Localize path '{Path}' must be in the 'Module.Key' format.");
            }

            return (Path[..separatorIndex], Path[(separatorIndex + 1)..]);
        }

        if (string.IsNullOrEmpty(Module) || string.IsNullOrEmpty(Key))
        {
            throw new InvalidOperationException(
                "Localize requires either a 'Module.Key' path or both Module and Key to be set.");
        }

        return (Module, Key);
    }
}