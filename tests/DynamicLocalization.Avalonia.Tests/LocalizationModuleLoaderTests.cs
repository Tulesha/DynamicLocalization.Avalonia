using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DynamicLocalization.Avalonia.Tests;

/// <summary>
///     Proves the exact bug this helper exists to fix: the CLR does not run a module's
///     <c>[ModuleInitializer]</c> just because the assembly was loaded - only lazily, before first
///     access of a member in that module. Here nothing ever touches a member of the compiled fixture
///     assembly before calling <see cref="LocalizationModuleLoader.EnsureLoaded(Assembly)" />.
/// </summary>
public class LocalizationModuleLoaderTests
{
    [Fact]
    public void EnsureLoaded_RunsModuleInitializer_ForAnAssemblyNothingHasTouchedYet()
    {
        var assembly = CompileAssemblyWithModuleInitializer(out var flagHolderTypeName);

        LocalizationModuleLoader.EnsureLoaded(assembly);

        var flagHolder = assembly.GetType(flagHolderTypeName)!;
        var value = (bool)flagHolder.GetField("Initialized")!.GetValue(null)!;
        Assert.True(value);
    }

    [Fact]
    public void EnsureLoaded_ByType_ResolvesAssembly_FromType()
    {
        LocalizationModuleLoader.EnsureLoaded(typeof(LocalizationModuleLoaderTests));
    }

    [Fact]
    public void EnsureLoaded_Throws_WhenAssemblyIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => LocalizationModuleLoader.EnsureLoaded((Assembly)null!));
    }

    [Fact]
    public void EnsureLoaded_Throws_WhenTypeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => LocalizationModuleLoader.EnsureLoaded((Type)null!));
    }

    private static Assembly CompileAssemblyWithModuleInitializer(out string flagHolderTypeName)
    {
        const string source = """
                              namespace DynamicallyCompiledFixture
                              {
                                  public static class Flag
                                  {
                                      public static bool Initialized;
                                  }

                                  internal static class Initializer
                                  {
                                      [System.Runtime.CompilerServices.ModuleInitializer]
                                      internal static void Init() => Flag.Initialized = true;
                                  }
                              }
                              """;

        flagHolderTypeName = "DynamicallyCompiledFixture.Flag";

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "DynamicallyCompiledFixture_" + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        stream.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(stream.ToArray());
    }
}