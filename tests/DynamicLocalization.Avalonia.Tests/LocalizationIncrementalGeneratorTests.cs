using System.Collections.Immutable;
using System.Text;
using DynamicLocalization.Avalonia.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DynamicLocalization.Avalonia.Tests;

/// <summary>
///     Drives <see cref="LocalizationIncrementalGenerator" /> directly through <see cref="GeneratorDriver" />,
///     the same mechanism MSBuild uses, against in-memory "localization.json" content - no project on
///     disk required.
/// </summary>
public class LocalizationIncrementalGeneratorTests
{
    private static (Compilation OutputCompilation, ImmutableArray<Diagnostic> Diagnostics, string? GeneratedSource)
        RunGenerator(
            string assemblyName, string localizationJson)
    {
        // typeof(...) forces DynamicLocalization.Avalonia.dll to be loaded regardless of test
        // execution order, so the generated code's references to it always resolve below.
        _ = typeof(LocalizationManager);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            Array.Empty<SyntaxTree>(),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new LocalizationIncrementalGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("localization.json", localizationJson)));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var runResult = driver.GetRunResult();
        var generatedSource = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .FirstOrDefault();

        return (outputCompilation, runResult.Diagnostics, generatedSource);
    }

    [Fact]
    public void Generator_ProducesTypedPropertyPerKey_AndModuleInitializer()
    {
        const string json = """[{"Key":"Welcome","En":"Welcome","Ru":"Добро пожаловать"}]""";

        var (_, diagnostics, source) = RunGenerator("PluginA", json);

        Assert.Empty(diagnostics);
        Assert.NotNull(source);
        Assert.Contains("public static string Welcome =>", source);
        Assert.Contains("ModuleInitializer", source);
        Assert.Contains("RegisterModule(\"PluginA\"", source);
    }

    [Fact]
    public void Generator_UsesCompilationAssemblyNameAsModuleNameAndNamespace()
    {
        const string json = """[{"Key":"Welcome","En":"Welcome"}]""";

        var (_, _, source) = RunGenerator("MyPlugin", json);

        Assert.Contains("namespace MyPlugin.Generated", source);
        Assert.Contains("public const string ModuleName = \"MyPlugin\"", source);
    }

    [Fact]
    public void Generator_RegeneratesDifferentOutput_WhenJsonContentChanges()
    {
        var first = RunGenerator("PluginA", """[{"Key":"A","En":"1"}]""").GeneratedSource;
        var second = RunGenerator("PluginA", """[{"Key":"A","En":"2"},{"Key":"B","En":"3"}]""").GeneratedSource;

        Assert.NotEqual(first, second);
        Assert.Contains("public static string B =>", second);
    }

    [Fact]
    public void Generator_ReportsDiagnostic_ForMalformedJson_InsteadOfThrowing()
    {
        var (_, diagnostics, source) = RunGenerator("PluginA", "not json");

        Assert.Null(source);
        Assert.Contains(diagnostics, d => d.Id == "DALOC001");
    }

    [Fact]
    public void Generator_ReportsDiagnostic_WhenEntryIsMissingKey()
    {
        var (_, diagnostics, source) = RunGenerator("PluginA", """[{"En":"Welcome"}]""");

        Assert.Null(source);
        Assert.Contains(diagnostics, d => d.Id == "DALOC001");
    }

    [Fact]
    public void Generator_SanitizesNonIdentifierCharacters_InAssemblyName()
    {
        var (_, _, source) = RunGenerator("My.Plugin-1", """[{"Key":"A","En":"1"}]""");

        Assert.Contains("namespace My_Plugin_1.Generated", source);
    }

    [Fact]
    public void Generator_EmptyArray_ProducesClassWithNoProperties()
    {
        var (_, diagnostics, source) = RunGenerator("PluginA", "[]");

        Assert.Empty(diagnostics);
        Assert.Contains("public static class PluginALocalization", source);
        Assert.DoesNotContain("=>", source);
    }

    [Fact]
    public void GeneratedCode_CompilesWithoutErrors()
    {
        const string json = """
                            [
                              {"Key":"Welcome","En":"Welcome","Ru":"Добро пожаловать"},
                              {"Key":"Greeting","En":"Hi {0}","Ru":"Привет {0}"}
                            ]
                            """;

        var (outputCompilation, diagnostics, _) = RunGenerator("PluginA", json);

        Assert.Empty(diagnostics);
        var errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return _text;
        }
    }
}