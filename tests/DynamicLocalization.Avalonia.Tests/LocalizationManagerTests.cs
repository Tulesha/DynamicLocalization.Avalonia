using Xunit;

namespace DynamicLocalization.Avalonia.Tests;

/// <summary>
///     Exercises a fresh, isolated <see cref="LocalizationManager" /> instance (constructible only
///     internally, via <c>InternalsVisibleTo</c>) rather than the process-wide <see cref="LocalizationManager.Instance" />
///     singleton, so tests stay independent and safe to run in parallel.
/// </summary>
public class LocalizationManagerTests
{
    private static LocalizationEntry Entry(string key, params (string Locale, string Text)[] values)
    {
        return new LocalizationEntry(key, values.ToDictionary(v => v.Locale, v => v.Text));
    }

    [Fact]
    public void Instance_ReturnsSameReference_OnEveryAccess()
    {
        var first = LocalizationManager.Instance;
        var second = LocalizationManager.Instance;

        Assert.Same(first, second);
    }

    [Fact]
    public void DefaultLocale_IsEn()
    {
        var manager = new LocalizationManager();

        Assert.Equal("En", manager.CurrentLocale);
        Assert.Equal("En", manager.FallbackLocale);
    }

    [Fact]
    public void Get_ReturnsCurrentLocaleValue_AndTracksCurrentLocaleChanges()
    {
        var manager = new LocalizationManager();
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome"), ("Ru", "Добро пожаловать")) });

        Assert.Equal("Welcome", manager.Get("PluginA", "Welcome"));

        manager.CurrentLocale = "Ru";

        Assert.Equal("Добро пожаловать", manager.Get("PluginA", "Welcome"));
    }

    [Fact]
    public void Get_FallsBackToFallbackLocale_WhenKeyMissingInCurrentLocale()
    {
        var manager = new LocalizationManager { CurrentLocale = "Fr" };
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome")) });

        Assert.Equal("Welcome", manager.Get("PluginA", "Welcome"));
    }

    [Fact]
    public void Get_ReturnsBracketedPlaceholder_WhenModuleUnknown()
    {
        var manager = new LocalizationManager();

        Assert.Equal("[PluginX.Missing]", manager.Get("PluginX", "Missing"));
    }

    [Fact]
    public void Get_ReturnsBracketedPlaceholder_WhenKeyUnknownEverywhere()
    {
        var manager = new LocalizationManager();
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome")) });

        Assert.Equal("[PluginA.Missing]", manager.Get("PluginA", "Missing"));
    }

    [Fact]
    public void Get_Throws_WhenModuleIsNull()
    {
        var manager = new LocalizationManager();

        Assert.Throws<ArgumentNullException>(() => manager.Get(null!, "Key"));
    }

    [Fact]
    public void Get_Throws_WhenKeyIsNull()
    {
        var manager = new LocalizationManager();

        Assert.Throws<ArgumentNullException>(() => manager.Get("Module", null!));
    }

    [Fact]
    public void RegisterModule_Throws_WhenModuleIsNull()
    {
        var manager = new LocalizationManager();

        Assert.Throws<ArgumentNullException>(() => manager.RegisterModule(null!, Array.Empty<LocalizationEntry>()));
    }

    [Fact]
    public void RegisterModule_Throws_WhenEntriesIsNull()
    {
        var manager = new LocalizationManager();

        Assert.Throws<ArgumentNullException>(() => manager.RegisterModule("Module", null!));
    }

    [Fact]
    public void RegisterModule_CalledTwice_OverwritesMatchingKeys()
    {
        var manager = new LocalizationManager();
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome v1")) });
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome v2")) });

        Assert.Equal("Welcome v2", manager.Get("PluginA", "Welcome"));
    }

    [Fact]
    public void RegisterModule_CalledTwice_KeepsPreviouslyRegisteredKeysNotInSecondCall()
    {
        var manager = new LocalizationManager();
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome")) });
        manager.RegisterModule("PluginA", new[] { Entry("SignIn", ("En", "Sign in")) });

        Assert.Equal("Welcome", manager.Get("PluginA", "Welcome"));
        Assert.Equal("Sign in", manager.Get("PluginA", "SignIn"));
    }

    [Theory]
    [InlineData("""[{"Key":"Welcome","En":"Welcome","Ru":"Добро пожаловать"}]""")]
    public void RegisterModuleFromJsonString_ParsesAndRegistersEntries(string json)
    {
        var manager = new LocalizationManager();

        manager.RegisterModuleFromJsonString("PluginA", json);

        Assert.Equal("Welcome", manager.Get("PluginA", "Welcome"));
        manager.CurrentLocale = "Ru";
        Assert.Equal("Добро пожаловать", manager.Get("PluginA", "Welcome"));
    }

    [Fact]
    public void RegisterModuleFromJsonString_EmptyArray_RegistersNoEntries()
    {
        var manager = new LocalizationManager();

        manager.RegisterModuleFromJsonString("PluginA", "[]");

        Assert.Equal("[PluginA.Anything]", manager.Get("PluginA", "Anything"));
    }

    [Fact]
    public void RegisterModuleFromJsonString_DuplicateKeys_LastOneWins()
    {
        var manager = new LocalizationManager();
        const string json = """
                            [
                              {"Key":"Welcome","En":"First"},
                              {"Key":"Welcome","En":"Second"}
                            ]
                            """;

        manager.RegisterModuleFromJsonString("PluginA", json);

        Assert.Equal("Second", manager.Get("PluginA", "Welcome"));
    }

    [Fact]
    public void RegisterModuleFromJsonString_MissingKeyProperty_Throws()
    {
        var manager = new LocalizationManager();
        const string json = """[{"En":"Welcome"}]""";

        Assert.Throws<FormatException>(() => manager.RegisterModuleFromJsonString("PluginA", json));
    }

    [Fact]
    public void RegisterModuleFromJsonString_MalformedJson_Throws()
    {
        var manager = new LocalizationManager();

        Assert.ThrowsAny<Exception>(() => manager.RegisterModuleFromJsonString("PluginA", "not json"));
    }

    [Fact]
    public void RegisterModuleFromJsonFile_ReadsFileFromDisk()
    {
        var manager = new LocalizationManager();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """[{"Key":"Welcome","En":"Welcome"}]""");

            manager.RegisterModuleFromJsonFile("PluginA", path);

            Assert.Equal("Welcome", manager.Get("PluginA", "Welcome"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Indexer_ParsesModuleDotKey()
    {
        var manager = new LocalizationManager();
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome")) });

        Assert.Equal("Welcome", manager["PluginA.Welcome"]);
    }

    [Fact]
    public void Indexer_ReturnsBracketedValue_WhenNoDotSeparator()
    {
        var manager = new LocalizationManager();

        Assert.Equal("[NoDot]", manager["NoDot"]);
    }

    [Fact]
    public void Indexer_ReturnsEmpty_WhenKeyIsEmpty()
    {
        var manager = new LocalizationManager();

        Assert.Equal(string.Empty, manager[string.Empty]);
    }

    [Fact]
    public void CurrentLocale_Setter_Throws_WhenValueIsNull()
    {
        var manager = new LocalizationManager();

        Assert.Throws<ArgumentNullException>(() => manager.CurrentLocale = null!);
    }

    [Fact]
    public void FallbackLocale_Setter_Throws_WhenValueIsNull()
    {
        var manager = new LocalizationManager();

        Assert.Throws<ArgumentNullException>(() => manager.FallbackLocale = null!);
    }

    [Fact]
    public void CurrentLocale_Setter_RaisesCultureChangedExactlyOnce()
    {
        var manager = new LocalizationManager();
        var raiseCount = 0;
        manager.CultureChanged += (_, _) => raiseCount++;

        manager.CurrentLocale = "Ru";

        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void CurrentLocale_Setter_DoesNotRaise_WhenSettingSameValue()
    {
        var manager = new LocalizationManager { CurrentLocale = "Ru" };
        var raiseCount = 0;
        manager.CultureChanged += (_, _) => raiseCount++;

        manager.CurrentLocale = "Ru";

        Assert.Equal(0, raiseCount);
    }

    [Fact]
    public void CurrentLocale_Setter_RaisesPropertyChanged_ForItemAndItemArray()
    {
        var manager = new LocalizationManager();
        var propertyNames = new List<string>();
        manager.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName ?? string.Empty);

        manager.CurrentLocale = "Ru";

        Assert.Contains("Item", propertyNames);
        Assert.Contains("Item[]", propertyNames);
    }

    [Fact]
    public void FallbackLocale_Setter_RaisesCultureChanged()
    {
        var manager = new LocalizationManager();
        var raised = false;
        manager.CultureChanged += (_, _) => raised = true;

        manager.FallbackLocale = "Ru";

        Assert.True(raised);
    }

    [Fact]
    public void RegisterModule_RaisesCultureChanged_SoBoundUiCanRefreshForNewlyLoadedModules()
    {
        var manager = new LocalizationManager();
        var raised = false;
        manager.CultureChanged += (_, _) => raised = true;

        manager.RegisterModule("PluginB", new[] { Entry("Welcome", ("En", "Welcome")) });

        Assert.True(raised);
    }

    [Fact]
    public void LocalizationManager_ImplementsILocalizationManager()
    {
        var manager = new LocalizationManager();

        Assert.IsAssignableFrom<ILocalizationManager>(manager);
    }

    [Fact]
    public void ILocalizationManager_Get_WorksThroughTheInterface()
    {
        ILocalizationManager manager = new LocalizationManager();
        manager.RegisterModule("PluginA", new[] { Entry("Welcome", ("En", "Welcome"), ("Ru", "Добро пожаловать")) });

        manager.CurrentLocale = "Ru";

        Assert.Equal("Добро пожаловать", manager.Get("PluginA", "Welcome"));
    }

    [Fact]
    public void RegisterModule_FromMultipleThreads_DoesNotLoseEntries()
    {
        var manager = new LocalizationManager();

        Parallel.For(0, 200,
            i => { manager.RegisterModule("PluginA", new[] { Entry($"Key{i}", ("En", $"Value{i}")) }); });

        for (var i = 0; i < 200; i++)
        {
            Assert.Equal($"Value{i}", manager.Get("PluginA", $"Key{i}"));
        }
    }
}