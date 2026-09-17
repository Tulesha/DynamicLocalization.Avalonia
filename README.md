# DynamicLocalization.Avalonia

[![NuGet](https://img.shields.io/nuget/v/Tulesha.DynamicLocalization.Avalonia.svg)](https://www.nuget.org/packages/Tulesha.DynamicLocalization.Avalonia)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Tulesha.DynamicLocalization.Avalonia.svg)](https://www.nuget.org/packages/Tulesha.DynamicLocalization.Avalonia)

A localization library for [Avalonia](https://avaloniaui.net/) applications built around one
idea: **modules (plugins) ship their own `localization.json`, and the current UI language can
change at runtime - in AXAML and in code - without restarting the app.**

- A single `LocalizationManager` singleton holds every registered module's translations and the
  current locale.
- A Roslyn source generator turns each module's `localization.json` into a resx-like, statically
  typed C# class - regenerated automatically whenever the JSON file changes.
- Modules can be wired in **statically** (a normal project reference, resolved at compile time)
  or **dynamically** (loaded from disk at runtime via `Assembly.LoadFrom`), using the same
  registration mechanism either way.
- Switching the locale updates every bound `TextBlock`, `Button`, etc. immediately, whether it's
  bound from AXAML markup or from a ViewModel property.

Targets **.NET 8** and **Avalonia 11.3.18**.

## Why

Real apps are often composed of several plugin-like modules, each with its own strings. Baking
all of that into a single `.resx` per language defeats modularity, and `.resx` doesn't support
changing the language while the app is running. This library keeps each module's translations
next to the module, and makes runtime language switching a first-class feature instead of an
afterthought.

## The `localization.json` shape

```json
[
  {
    "Key": "WindowTitle",
    "Ru": "Авторизация",
    "En": "Authentication"
  },
  {
    "Key": "Greeting",
    "Ru": "Привет, {0}!",
    "En": "Hello, {0}!"
  }
]
```

Each entry is a `Key` plus one property per locale. Locale names are plain strings (`"En"`,
`"Ru"`, ...) - there's no dependency on `CultureInfo` or ISO codes, so you can use whatever
identifiers your app already uses. Values containing `{0}`-style placeholders are returned as-is;
format them with `string.Format` on the consuming side (see the `Greeting` examples below).

## Quick start

### 1. Reference the library and the generator from a plugin project

Install the NuGet package in the project that owns the `localization.json` (a plugin, or the app
itself if it isn't split into plugins):

```xml
<ItemGroup>
  <PackageReference Include="Tulesha.DynamicLocalization.Avalonia" Version="x.y.z" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="localization.json" />
</ItemGroup>
```

or via the CLI:

```bash
dotnet add package Tulesha.DynamicLocalization.Avalonia
```

That's the whole reference - no `OutputItemType="Analyzer"` or `ReferenceOutputAssembly="false"`
needed. The package bundles the source generator as a Roslyn analyzer
(`analyzers/dotnet/cs/DynamicLocalization.Avalonia.SourceGenerator.dll`), which NuGet wires up
automatically for a `PackageReference`; that extra wiring is only needed when referencing the
generator project directly, e.g. from inside this repo:

```xml
<ItemGroup>
  <AdditionalFiles Include="localization.json" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="path\to\DynamicLocalization.Avalonia.csproj" />
  <ProjectReference Include="path\to\DynamicLocalization.Avalonia.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Add a `localization.json` next to the project (shape above). Building the project generates a
static, resx-like class named after the assembly - e.g. a project named `PluginA` gets
`PluginA.Generated.PluginALocalization`:

```csharp
namespace PluginA.Generated;

public static class PluginALocalization
{
    public const string ModuleName = "PluginA";

    public static string WindowTitle => /* looks up the current locale via LocalizationManager */;
    public static string Greeting => /* ... */;
}
```

The generator also emits a `[ModuleInitializer]` that self-registers the module's entries with
`LocalizationManager.Instance` the first time anything in that module is used - see
[Loading modules](#loading-modules-static-vs-dynamic) below for the one thing to know about that.

The generator re-runs automatically whenever `localization.json` changes and you rebuild.

### 2. Bind to it from AXAML

```xml
<Window xmlns:loc="using:DynamicLocalization.Avalonia.Markup">
    <TextBlock Text="{loc:Localize PluginA.WindowTitle}" />
    <!-- equivalent, explicit form: -->
    <TextBlock Text="{loc:Localize Module=PluginA, Key=WindowTitle}" />
</Window>
```

No code-behind needed. The binding refreshes automatically whenever the locale changes.

### 3. Or use the generated properties from code / a ViewModel

```csharp
public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        // Re-raise "everything changed" whenever the locale switches, so every bound
        // property re-evaluates - the standard MVVM idiom for a global locale change.
        LocalizationManager.Instance.CultureChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    public string WindowTitle => PluginALocalization.WindowTitle;
    public string Greeting => string.Format(PluginALocalization.Greeting, UserName);

    [ObservableProperty]
    private string _userName = "Alice";
}
```

### 4. Switch the locale

```csharp
LocalizationManager.Instance.CurrentLocale = "Ru";
```

That's it - every AXAML binding and every ViewModel property wired up as above updates
immediately, with no restart and no manual refresh calls.

## Loading modules: static vs. dynamic

Both paths register with the exact same `LocalizationManager.Instance` - the only difference is
*when* and *how* the plugin assembly reaches the process.

|                              | Static                                                          | Dynamic                                                                                                  |
|------------------------------|-----------------------------------------------------------------|----------------------------------------------------------------------------------------------------------|
| How the plugin is referenced | Normal `ProjectReference` (or NuGet package)                    | Not referenced at compile time at all                                                                    |
| How it reaches the process   | Loaded automatically as part of normal .NET assembly resolution | `Assembly.LoadFrom("path/to/Plugin.dll")` at runtime, e.g. from a "plugins" folder                       |
| How you consume it           | The generated typed class (`PluginALocalization.WindowTitle`)   | `LocalizationManager.Instance.Get("PluginB", "WindowTitle")` - there's no compile-time type to reference |
| AXAML                        | `{loc:Localize PluginA.WindowTitle}`                            | `{loc:Localize PluginB.WindowTitle}` - identical syntax, since it's just a module/key string either way  |

One CLR subtlety both paths need to account for: a `[ModuleInitializer]` only runs **before the
first access of any member in that module** - not simply because the assembly was loaded or
referenced. A plugin consumed purely from AXAML carries no IL-level reference to the plugin
assembly (the compiled markup only holds the "Module.Key" string), so nothing may ever trigger
that first access on its own. `LocalizationModuleLoader.EnsureLoaded(...)` forces it explicitly:

```csharp
// Static plugin, once at startup (needed only if it's consumed purely from AXAML):
LocalizationModuleLoader.EnsureLoaded(typeof(PluginALocalization));

// Dynamic plugin, right after loading it:
var assembly = Assembly.LoadFrom(pluginPath);
LocalizationModuleLoader.EnsureLoaded(assembly);
```

See the sample app's `App.axaml.cs` and `MainWindowViewModel.LoadPluginB()` for both in context.

## Core API

- **`LocalizationManager.Instance`** - the singleton. `CurrentLocale` / `FallbackLocale` (used
  when a key is missing in the current locale) are plain, freely settable strings.
- **`Get(module, key)`** - returns the translated string for the current locale, falling back to
  `FallbackLocale`, and finally to a visible `"[module.key]"` placeholder if nothing matches (so
  missing translations are obvious instead of silently blank).
- **`RegisterModule(module, entries)`** - register or update a module's entries directly.
- **`RegisterModuleFromJsonString` / `RegisterModuleFromJsonFile`** - parse and register a
  `localization.json` payload without generated code, for modules that ship translations as data
  only (e.g. user-editable/hot-reloadable content).
- **`CultureChanged`** event - fires whenever `CurrentLocale`, `FallbackLocale`, or the set of
  registered modules changes. Subscribe from a ViewModel to refresh bound properties.
- **`LocalizeExtension`** (`xmlns:loc="using:DynamicLocalization.Avalonia.Markup"`) - the AXAML
  markup extension shown above.
- **`LocalizationModuleLoader.EnsureLoaded(...)`** - forces a plugin's module initializer to run
  immediately; see [Loading modules](#loading-modules-static-vs-dynamic).
- **`ILocalizationManager`** - the interface `LocalizationManager` implements, for hand-written
  code (ViewModels, services) that wants to depend on an abstraction instead of the concrete
  singleton; see [Dependency injection](#dependency-injection) below.

## Dependency injection

`LocalizationManager` implements `ILocalizationManager`, so a ViewModel or service can take it as
a constructor dependency instead of reading `LocalizationManager.Instance` directly - useful for
unit-testing that code against a fake/isolated instance instead of process-wide global state.

```csharp
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILocalizationManager _localization;

    public MainWindowViewModel(ILocalizationManager localization)
    {
        _localization = localization;
        _localization.CultureChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    public string DynamicWindowTitle => _localization.Get("PluginB", "WindowTitle");
}
```

Bind the interface to the singleton once, at composition root, with whatever container you use -
e.g. `Microsoft.Extensions.DependencyInjection`:

```csharp
var services = new ServiceCollection();
services.AddSingleton<ILocalizationManager>(LocalizationManager.Instance);
services.AddTransient<MainWindowViewModel>();

var provider = services.BuildServiceProvider();
var vm = provider.GetRequiredService<MainWindowViewModel>();
```

Bind to `LocalizationManager.Instance` itself, not a fresh `new LocalizationManager()` - both
`LocalizeExtension` (AXAML `{loc:Localize ...}` bindings) and the source generator's emitted
accessors (`PluginALocalization.WindowTitle`) are hardwired to that same singleton instance and
can't be redirected via DI (a markup extension is instantiated by the XAML loader, and a generated
accessor is a static property - neither has a constructor to inject into). Registering a different
instance would just split the app into two locales that never see each other's `RegisterModule`
calls or locale switches. What DI buys you here is a seam for the code you *do* write by hand, not
a fully container-managed localization stack - see the sample app's `App.axaml.cs` for the
container wiring and `MainWindowViewModel`'s constructor for the consuming side.

## Repository layout

```
src/DynamicLocalization.Avalonia/               Core library
src/DynamicLocalization.Avalonia.SourceGenerator/  Roslyn incremental generator
samples/Plugins/PluginA/                       Sample plugin, referenced statically by SampleApp
samples/Plugins/PluginB/                       Sample plugin, loaded dynamically at runtime
samples/SampleApp/                             Avalonia MVVM demo app
tests/DynamicLocalization.Avalonia.Tests/       xUnit unit tests
tests/DynamicLocalization.Avalonia.HeadlessTests/  Avalonia.Headless.XUnit UI/binding tests
```

See [AGENTS.md](AGENTS.md) if you're extending the library or the generator - it documents a few
non-obvious CLR/Avalonia behaviors this codebase works around.

## Running the sample

```bash
dotnet run --project samples/SampleApp/SampleApp.csproj
```

The demo shows PluginA's strings both from pure AXAML bindings and from a ViewModel, lets you
switch between `En` and `Ru` live, and has a "Load Plugin B" button that loads a second module at
runtime (via `Assembly.LoadFrom`) and immediately shows its localized, correctly-formatted
strings.

## Building and testing

```bash
dotnet build DynamicLocalization.Avalonia.slnx
dotnet test DynamicLocalization.Avalonia.slnx
```

To inspect what the generator actually produces for a given plugin:

```bash
dotnet build samples/Plugins/PluginA/PluginA.csproj -p:EmitCompilerGeneratedFiles=true
```

The generated file lands under
`samples/Plugins/PluginA/obj/Debug/net8.0/generated/DynamicLocalization.Avalonia.SourceGenerator/.../PluginA.Localization.g.cs`.

## Requirements

- .NET 8 SDK
- Avalonia 11.3.18 (pinned across every project in the solution)
