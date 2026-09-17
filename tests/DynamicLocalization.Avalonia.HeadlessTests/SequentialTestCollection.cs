using Xunit;

namespace DynamicLocalization.Avalonia.HeadlessTests;

/// <summary>
///     All headless tests share this collection so xUnit runs them sequentially: they all read and
///     mutate the process-wide <c>LocalizationManager.Instance</c> singleton (the same instance the
///     AXAML markup extension binds to), so parallel execution would make them flaky.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class SequentialTestCollection
{
    public const string Name = "DynamicLocalization.Avalonia.HeadlessTests.Sequential";
}