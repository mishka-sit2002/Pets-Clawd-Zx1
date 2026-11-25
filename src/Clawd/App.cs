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
    static GameWindow? game;
    static StageWindow? stage;

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
            stage = new StageWindow();     // mascot outings on the taskbar
            game = new GameWindow();       // Game-mode viewer: stays hidden until the mode is switched on
        }
        base.OnFrameworkInitializationCompleted();
    }
}
