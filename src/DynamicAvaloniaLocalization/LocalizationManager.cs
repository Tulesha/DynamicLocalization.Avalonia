using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;

namespace DynamicAvaloniaLocalization;

/// <summary>
///     Central registry of localization modules and the current application locale.
///     Raises change notifications so that Avalonia bindings and view models can refresh
///     without an application restart.
/// </summary>
/// <remarks>
///     Implements <see cref="ILocalizationManager" /> so hand-written consumers can depend on the
///     interface (e.g. via DI) instead of the concrete type - see that interface's remarks for why
///     <see cref="Markup.LocalizeExtension" /> and generated accessors don't.
/// </remarks>
public sealed class LocalizationManager : ILocalizationManager
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, LocalizationEntry>> _modules = new();

    private string _currentLocale;
    private string _fallbackLocale = "En";

    /// <summary>
    ///     Internal on purpose: application code must use <see cref="Instance" />. A constructible
    ///     type (rather than a fully static class) lets tests create isolated instances instead of
    ///     mutating global state.
    /// </summary>
    internal LocalizationManager()
    {
        _currentLocale = "En";
    }

    public static LocalizationManager Instance { get; } = new();

    public string CurrentLocale
    {
        get => _currentLocale;
        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (_currentLocale == value)
            {
                return;
            }

            _currentLocale = value;
            RaiseChanged();
        }
    }

    public string FallbackLocale
    {
        get => _fallbackLocale;
        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (_fallbackLocale == value)
            {
                return;
            }

            _fallbackLocale = value;
            RaiseChanged();
        }
    }

    /// <summary>
    ///     Indexer used by Avalonia bindings, e.g.
    ///     <c>{Binding "[Module.Key]", Source={x:Static loc:LocalizationManager.Instance}}</c>.
    ///     Avalonia refreshes indexer bindings when <see cref="PropertyChanged" /> fires for "Item"/"Item[]",
    ///     which <see cref="RaiseChanged" /> does on every locale change.
    /// </summary>
    public string this[string moduleDotKey]
    {
        get
        {
            if (string.IsNullOrEmpty(moduleDotKey))
            {
                return string.Empty;
            }

            var separatorIndex = moduleDotKey.IndexOf('.');
            if (separatorIndex < 0)
            {
                return $"[{moduleDotKey}]";
            }

            var module = moduleDotKey[..separatorIndex];
            var key = moduleDotKey[(separatorIndex + 1)..];
            return Get(module, key);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Raised whenever a previously returned <see cref="Get" /> result may now be stale: when
    ///     <see cref="CurrentLocale" /> or <see cref="FallbackLocale" /> changes, and whenever a module
    ///     is (re-)registered (e.g. a plugin is loaded at runtime, or translations are hot-reloaded).
    /// </summary>
    public event EventHandler? CultureChanged;

    /// <summary>Registers (or updates) a module's localization entries.</summary>
    public void RegisterModule(string module, IEnumerable<LocalizationEntry> entries)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        var moduleEntries =
            _modules.GetOrAdd(module, static _ => new ConcurrentDictionary<string, LocalizationEntry>());
        foreach (var entry in entries) moduleEntries[entry.Key] = entry;

        RaiseChanged();
    }

    /// <summary>Parses a localization.json payload and registers it as <paramref name="module" />.</summary>
    public void RegisterModuleFromJsonString(string module, string json)
    {
        if (json is null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        RegisterModule(module, ParseJson(json));
    }

    /// <summary>Reads a localization.json file from disk and registers it as <paramref name="module" />.</summary>
    public void RegisterModuleFromJsonFile(string module, string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        RegisterModuleFromJsonString(module, File.ReadAllText(path));
    }

    /// <summary>
    ///     Returns the localized text for <paramref name="module" />/<paramref name="key" /> in the current
    ///     locale, falling back to <see cref="FallbackLocale" />, and finally to a visible
    ///     "[module.key]" placeholder when no translation exists.
    /// </summary>
    public string Get(string module, string key)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (_modules.TryGetValue(module, out var entries) && entries.TryGetValue(key, out var entry))
        {
            if (entry.Values.TryGetValue(_currentLocale, out var value))
            {
                return value;
            }

            if (entry.Values.TryGetValue(_fallbackLocale, out var fallbackValue))
            {
                return fallbackValue;
            }
        }

        return $"[{module}.{key}]";
    }

    private static List<LocalizationEntry> ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var entries = new List<LocalizationEntry>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            string? key = null;
            var values = new Dictionary<string, string>();

            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, "Key", StringComparison.OrdinalIgnoreCase))
                {
                    key = property.Value.GetString();
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    values[property.Name] = property.Value.GetString()!;
                }

            if (string.IsNullOrEmpty(key))
            {
                throw new FormatException("A localization entry is missing a non-empty 'Key' property.");
            }

            entries.Add(new LocalizationEntry(key, values));
        }

        return entries;
    }

    private void RaiseChanged()
    {
        CultureChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}