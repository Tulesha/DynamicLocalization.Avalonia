using Xunit;

namespace DynamicAvaloniaLocalization.Tests;

public class LocalizationEntryTests
{
    [Fact]
    public void Constructor_ExposesKeyAndValues()
    {
        var values = new Dictionary<string, string> { ["En"] = "Hello", ["Ru"] = "Привет" };

        var entry = new LocalizationEntry("Greeting", values);

        Assert.Equal("Greeting", entry.Key);
        Assert.Equal("Hello", entry.Values["En"]);
        Assert.Equal("Привет", entry.Values["Ru"]);
    }

    [Fact]
    public void Constructor_Throws_WhenKeyIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LocalizationEntry(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public void Constructor_Throws_WhenValuesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalizationEntry("Key", null!));
    }

    [Fact]
    public void Constructor_AllowsEmptyValues()
    {
        var entry = new LocalizationEntry("Key", new Dictionary<string, string>());

        Assert.Empty(entry.Values);
    }
}