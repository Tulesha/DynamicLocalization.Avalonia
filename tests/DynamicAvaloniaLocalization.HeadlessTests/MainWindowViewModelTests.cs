using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using SampleApp.ViewModels;
using Xunit;

namespace DynamicAvaloniaLocalization.HeadlessTests;

/// <summary>
///     Verifies the ViewModel/code side of dynamic localization using the real, shipped
///     <see cref="MainWindowViewModel" /> from the sample app: PluginA (static, generated typed
///     accessors) and PluginB (dynamic, loaded via Assembly.LoadFrom, consumed through the
///     module/key string API) both notify bound controls live when the locale changes.
/// </summary>
[Collection(SequentialTestCollection.Name)]
public class MainWindowViewModelTests : IDisposable
{
    public MainWindowViewModelTests()
    {
        LocalizationManager.Instance.CurrentLocale = "En";
    }

    public void Dispose()
    {
        LocalizationManager.Instance.CurrentLocale = "En";
    }

    private static TextBlock BindText(MainWindowViewModel vm, string path)
    {
        var textBlock = new TextBlock();
        textBlock.Bind(TextBlock.TextProperty, new Binding(path) { Source = vm });
        return textBlock;
    }

    [AvaloniaFact]
    public void StaticWindowTitle_ReflectsPluginAGeneratedAccessor()
    {
        var vm = new MainWindowViewModel();
        var textBlock = BindText(vm, nameof(MainWindowViewModel.StaticWindowTitle));

        Assert.Equal("Authentication A", textBlock.Text);
    }

    [AvaloniaFact]
    public void BoundTextBlock_UpdatesAutomatically_WhenLocaleChanges()
    {
        var vm = new MainWindowViewModel();
        var textBlock = BindText(vm, nameof(MainWindowViewModel.StaticWindowTitle));
        Assert.Equal("Authentication A", textBlock.Text);

        vm.ChangeLocaleCommand.Execute("Ru");

        Assert.Equal("Авторизация A", textBlock.Text);
    }

    [AvaloniaFact]
    public void BoundTextBlock_SwitchingBackAndForth_IsFullyReversible()
    {
        var vm = new MainWindowViewModel();
        var textBlock = BindText(vm, nameof(MainWindowViewModel.StaticWindowTitle));

        vm.ChangeLocaleCommand.Execute("Ru");
        vm.ChangeLocaleCommand.Execute("En");

        Assert.Equal("Authentication A", textBlock.Text);
    }

    [AvaloniaFact]
    public void StaticGreeting_FormatsUserNameIntoTemplate()
    {
        var vm = new MainWindowViewModel { UserName = "Bob" };
        var textBlock = BindText(vm, nameof(MainWindowViewModel.StaticGreeting));

        Assert.Equal("Hello, Bob!", textBlock.Text);
    }

    [AvaloniaFact]
    public void StaticGreeting_UpdatesWhenUserNameChanges()
    {
        var vm = new MainWindowViewModel { UserName = "Bob" };
        var textBlock = BindText(vm, nameof(MainWindowViewModel.StaticGreeting));

        vm.UserName = "Carol";

        Assert.Equal("Hello, Carol!", textBlock.Text);
    }

    [AvaloniaFact]
    public void StaticGreeting_ReactsToLocaleChange()
    {
        var vm = new MainWindowViewModel { UserName = "Bob" };
        var textBlock = BindText(vm, nameof(MainWindowViewModel.StaticGreeting));

        vm.ChangeLocaleCommand.Execute("Ru");

        Assert.Equal("Привет, Bob!", textBlock.Text);
    }

    [AvaloniaFact]
    public void NewViewModel_StartsWithPluginBNotLoaded()
    {
        var vm = new MainWindowViewModel();

        Assert.False(vm.IsPluginBLoaded);
        Assert.True(vm.LoadPluginBCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void LoadPluginB_RegistersModule_AndBoundTextReflectsItImmediately()
    {
        var vm = new MainWindowViewModel();
        var textBlock = BindText(vm, nameof(MainWindowViewModel.DynamicWindowTitle));

        vm.LoadPluginBCommand.Execute(null);

        Assert.True(vm.IsPluginBLoaded);
        Assert.False(vm.LoadPluginBCommand.CanExecute(null));
        Assert.Equal("Authentication B", textBlock.Text);
    }

    [AvaloniaFact]
    public void LoadPluginB_DynamicGreeting_FormatsUserNameAfterLoad()
    {
        var vm = new MainWindowViewModel { UserName = "Dana" };
        var textBlock = BindText(vm, nameof(MainWindowViewModel.DynamicGreeting));

        vm.LoadPluginBCommand.Execute(null);

        Assert.Equal("Hello, Dana!", textBlock.Text);
    }

    [AvaloniaFact]
    public void LoadPluginB_ThenLocaleSwitch_UpdatesDynamicTextToo()
    {
        var vm = new MainWindowViewModel();
        var textBlock = BindText(vm, nameof(MainWindowViewModel.DynamicWindowTitle));

        vm.LoadPluginBCommand.Execute(null);
        Assert.Equal("Authentication B", textBlock.Text);

        vm.ChangeLocaleCommand.Execute("Ru");

        Assert.Equal("Авторизация B", textBlock.Text);
    }
}