using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Clawd;

public class App : Application
{
    static CornerWindow? corner;
    static StatsWindow? stats;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        {
            d.ShutdownMode = ShutdownMode.OnMainWindowClose;
            d.MainWindow = new PetWindow();
            corner = new CornerWindow();   // stays hidden until December
            stats = new StatsWindow();     // feelings panel at the left end of the taskbar
        }
        base.OnFrameworkInitializationCompleted();
    }
}
