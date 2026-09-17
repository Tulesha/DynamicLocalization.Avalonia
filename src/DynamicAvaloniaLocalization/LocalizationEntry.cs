namespace DynamicAvaloniaLocalization;

/// <summary>
///     A single localizable string: a key plus its translated text per locale.
/// </summary>
public sealed class LocalizationEntry
{
    public LocalizationEntry(string key, IReadOnlyDictionary<string, string> values)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public string Key { get; }

    /// <summary>Locale (e.g. "En", "Ru") to translated text.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }
}