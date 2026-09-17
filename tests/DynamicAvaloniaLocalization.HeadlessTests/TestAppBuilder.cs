using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using DynamicAvaloniaLocalization.HeadlessTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace DynamicAvaloniaLocalization.HeadlessTests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HeadlessTestApp>()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

public class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}