using System.Reflection;
using System.Runtime.CompilerServices;

namespace DynamicLocalization.Avalonia;

/// <summary>
///     Forces a plugin assembly's <c>[ModuleInitializer]</c>-based self-registration to run
///     immediately, instead of waiting for the CLR's default behavior of running it lazily, on first
///     access of any member defined in that module.
/// </summary>
/// <remarks>
///     That laziness is invisible for a plugin consumed from code (calling any generated property,
///     e.g. from a ViewModel, already forces the module to load first). It only bites a plugin
///     consumed purely from AXAML through <c>{loc:Localize Module.Key}</c>: the compiled markup only
///     carries the "Module.Key" string, so nothing in the host's IL ever references the plugin
///     assembly, and its module initializer would otherwise never run. Call
///     <see cref="EnsureLoaded{T}" /> once per plugin - typically at startup for a statically
///     referenced one, or right after <see cref="Assembly.LoadFrom(string)" /> for one loaded at
///     runtime - to make that case work too.
/// </remarks>
public static class LocalizationModuleLoader
{
    /// <summary>
    ///     Ensures the assembly that declares <paramref name="typeFromTargetAssembly" /> has run its
    ///     module initializer(s). Takes a <see cref="Type" /> rather than a generic type parameter
    ///     because the source-generated accessor classes this is meant to target are static, and C#
    ///     does not allow a static type as a generic argument.
    /// </summary>
    public static void EnsureLoaded(Type typeFromTargetAssembly)
    {
        if (typeFromTargetAssembly is null)
        {
            throw new ArgumentNullException(nameof(typeFromTargetAssembly));
        }

        EnsureLoaded(typeFromTargetAssembly.Assembly);
    }

    /// <summary>Ensures <paramref name="assembly" /> has run its module initializer(s).</summary>
    public static void EnsureLoaded(Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        foreach (var module in assembly.GetModules()) RuntimeHelpers.RunModuleConstructor(module.ModuleHandle);
    }
}