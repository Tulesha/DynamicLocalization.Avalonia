# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository.

## What this repo is

A library for dynamic, hot-swappable localization in Avalonia applications, plus the Roslyn
source generator that makes each plugin's `localization.json` usable as typed, resx-like C#
properties. See [README.md](README.md) for the user-facing overview and usage examples.

## Solution layout

```
src/DynamicLocalization.Avalonia/               Core library: LocalizationManager, LocalizeExtension, LocalizationModuleLoader
src/DynamicLocalization.Avalonia.SourceGenerator/  Incremental generator (netstandard2.0), turns localization.json into typed accessors
samples/Plugins/PluginA/                       Sample plugin, statically referenced by SampleApp
samples/Plugins/PluginB/                       Sample plugin, loaded at runtime via Assembly.LoadFrom
samples/SampleApp/                             Avalonia MVVM demo (CommunityToolkit.Mvvm)
tests/DynamicLocalization.Avalonia.Tests/       xUnit: manager, JSON parsing, generator, module loader
tests/DynamicLocalization.Avalonia.HeadlessTests/  Avalonia.Headless.XUnit: AXAML bindings + real ViewModel
```

## Package versions

Package versions are centralized
via [NuGet Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management):
`Directory.Packages.props` at the repo root sets `ManagePackageVersionsCentrally=true` and
imports every file under `build/`. **Each package gets its own `build/<PackageId>.props` file**
containing a single `<PackageVersion>` item - no grouping, no shared version properties. To bump
or add a package:

- Existing package: edit its one file, e.g. `build/Avalonia.props`.
- New package: add `build/<PackageId>.props` (copy an existing one, one `<PackageVersion>` item),
  then add an `<Import Project="build/<PackageId>.props" />` line to `Directory.Packages.props`.
- Every `.csproj` then references the package with **no `Version` attribute**:
  `<PackageReference Include="Avalonia" />`. Adding a `Version` back on a `PackageReference`
  breaks the build under CPM (use `VersionOverride` only if a single project genuinely needs to
  deviate, which nothing here currently does).

`NuGet.Config` at the repo root pins restore to `nuget.org` with package source mapping - CPM
warns (`NU1507`) if more than one source is registered without mapping, which a machine's global
NuGet config (extra corporate feeds, etc.) would otherwise trigger regardless of anything in this
repo.

Everything targets **.NET 8** and **Avalonia 11.3.18** (pinned via `build/Avalonia*.props` - keep
new Avalonia packages on the same version). The generator project is the one exception: it
targets `netstandard2.0` because Roslyn analyzers/
generators run inside the host IDE/compiler process, not the target app's runtime.

## Before you touch anything

Read [src/DynamicLocalization.Avalonia/LocalizationManager.cs](src/DynamicLocalization.Avalonia/LocalizationManager.cs)
and [src/DynamicLocalization.Avalonia.SourceGenerator/LocalizationIncrementalGenerator.cs](src/DynamicLocalization.Avalonia.SourceGenerator/LocalizationIncrementalGenerator.cs)
first - nearly every feature in this repo is one of those two files reacting to a change in the
other. If you're changing the generated code's shape, check `LocalizationIncrementalGeneratorTests.cs`
for the exact string assertions that will need updating too.

## Non-obvious constraints (read this before "fixing" something)

1. **`[ModuleInitializer]` is lazy, not eager.** The CLR only guarantees a module initializer
   runs "before first access of a member in that module" - not the moment the assembly is
   loaded or referenced. A plugin consumed purely from AXAML via `{loc:Localize Module.Key}`
   carries no IL-level reference to the plugin assembly at all, so its module initializer may
   never fire on its own. This is why `LocalizationModuleLoader.EnsureLoaded(...)` exists and is
   called explicitly in `App.axaml.cs` (for the static plugin) and in
   `MainWindowViewModel.LoadPluginB()` (right after `Assembly.LoadFrom`). If you add a new
   plugin-loading path, you almost certainly need to call `EnsureLoaded` there too - don't assume
   "referencing/loading the assembly is enough." This was found the hard way via the headless
   tests; see `LocalizationModuleLoaderTests.cs` for a self-contained repro that proves the bug
   without needing a real plugin.

2. **`LocalizationManager` is a true singleton (`Instance`), but the constructor is `internal`.**
   Tests new up isolated instances (`new LocalizationManager()`) via `InternalsVisibleTo` to
   `DynamicLocalization.Avalonia.Tests` and `DynamicLocalization.Avalonia.HeadlessTests` instead of
   mutating the shared `Instance` and fighting test isolation. Prefer this pattern for new unit
   tests. Tests that must use `Instance` directly (because they exercise `LocalizeExtension`,
   which hardcodes `LocalizationManager.Instance` as its binding source) live under
   `HeadlessTests` and are pinned to `[Collection(SequentialTestCollection.Name)]` with
   `DisableParallelization = true` - the singleton is process-wide, so parallel tests touching it
   are flaky by construction. Reset `CurrentLocale` in the constructor/`Dispose()` of any new test
   class in that file.

3. **Avalonia refreshes indexer bindings on `PropertyChanged("Item")` / `PropertyChanged("Item[]")`**,
   not on an empty/null property name for an indexed binding. `LocalizationManager.RaiseChanged()`
   fires both, deliberately, on every `CurrentLocale`/`FallbackLocale` change *and* every
   `RegisterModule` call (so a freshly loaded/hot-reloaded module refreshes bound UI too, not just
   a locale switch). If you touch `RaiseChanged`, keep both notifications.

4. **The generator writes its own minimal JSON parser** (`MinimalJsonParser.cs`) instead of taking
   a `System.Text.Json` package dependency. This is deliberate: a generator project's package
   references run inside the compiler process and can conflict with whatever the *consuming*
   project references. Don't add a JSON package dependency to the generator project; extend the
   hand-rolled parser instead if new JSON shapes need supporting.

5. **Headless UI tests avoid constructing a real `Window`.** An earlier version of
   `LocalizeExtensionTests` called `new Window { Content = view }; window.Show();`, which
   intermittently threw `KeyNotFoundException: "fonts:SystemFonts"` from Avalonia's compositor
   diagnostics text renderer under the headless platform - a font-manager timing issue unrelated
   to what the tests actually verify. Classic Avalonia `Binding`s activate as soon as they're
   assigned (during `InitializeComponent`), so a plain unattached control is enough to test
   binding behavior; don't reintroduce `Window`/`Show()` in these tests unless you're
   specifically testing window-level behavior, and if you do, expect to deal with font-manager
   flakiness (e.g. `.WithInterFont()` in `TestAppBuilder`, which is already wired up).

6. **Module/property names are sanitized independently of the runtime module key.** The generator
   uses the raw `compilation.AssemblyName` as the *runtime* module string (the thing you pass to
   `LocalizationManager.Get(module, key)` and use in `{loc:Localize Module.Key}`), but sanitizes
   it separately into a valid C# identifier for the generated namespace/class name. Don't
   conflate the two when changing naming logic - a project named `My.Plugin-1` registers at
   runtime as module `"My.Plugin-1"` but generates `namespace My_Plugin_1.Generated`.

## Adding a new sample plugin

1. New class library project under `samples/Plugins/<Name>/`, targeting `net8.0`.
2. `<AdditionalFiles Include="localization.json" />` + a `ProjectReference` to
   `DynamicLocalization.Avalonia` + a `ProjectReference` to
   `DynamicLocalization.Avalonia.SourceGenerator` with `OutputItemType="Analyzer"` and
   `ReferenceOutputAssembly="false"`. Copy an existing plugin's `.csproj` - they're all identical
   modulo the assembly name.
3. To wire it into `SampleApp` **statically**: add a normal `ProjectReference`, and call
   `LocalizationModuleLoader.EnsureLoaded(typeof(<Name>Localization))` somewhere the app actually
   runs (see constraint #1 above) if the plugin might be consumed purely from AXAML.
4. To wire it in **dynamically** instead: add a `ProjectReference` with
   `ReferenceOutputAssembly="false"` (build-order only, no linking) plus an `AfterTargets="Build"`
   `Copy` target to stage the built DLL somewhere the host loads it from at runtime (see
   `SampleApp.csproj`'s `CopyPluginBToPluginsFolder` target for the exact pattern), then
   `Assembly.LoadFrom` + `LocalizationModuleLoader.EnsureLoaded(assembly)` in the loading code.

## Verifying a change

```bash
dotnet build DynamicLocalization.Avalonia.slnx
dotnet test DynamicLocalization.Avalonia.slnx
```

Both must be clean (0 warnings, 0 errors; all tests green) before calling anything done - the
warning budget here is zero on purpose (`Directory.Build.props` sets
`WarningsAsErrors=nullable`). If you change what the generator emits, inspect the actual output
once with:

```bash
dotnet build samples/Plugins/PluginA/PluginA.csproj -p:EmitCompilerGeneratedFiles=true
```

then read
`samples/Plugins/PluginA/obj/Debug/net8.0/generated/DynamicLocalization.Avalonia.SourceGenerator/.../PluginA.Localization.g.cs`
directly - don't trust it compiles just because the test assertions on partial strings pass.

For UI-affecting changes, there's no browser to check against (this is a native Avalonia desktop
app). Run the sample and interact with it, or at minimum smoke-test that it starts:

```bash
dotnet run --project samples/SampleApp/SampleApp.csproj
```

The headless test suite (`tests/DynamicLocalization.Avalonia.HeadlessTests`) is the primary
automated coverage for AXAML/binding behavior - extend it rather than relying on manual
verification alone.

## Conventions

- No comments explaining *what* code does; comments only for non-obvious *why* (see the
  constraints above for the kind of thing that deserves one).
- Keep `WarningsAsErrors=nullable` passing - don't suppress nullable warnings with `!` unless the
  non-null invariant is actually guaranteed at that point.
- New public API needs both a unit test (`DynamicLocalization.Avalonia.Tests`) and, if it affects
  AXAML/binding behavior, a headless test.
