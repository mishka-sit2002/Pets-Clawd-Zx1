using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd;

class PetView : Control
{
    readonly Pet pet;
    public PetView(Pet p) { pet = p; }
    public override void Render(DrawingContext c) => pet.Render(c);
}

public class PetWindow : Window
{
    readonly Pet pet = new();
    readonly PetView view;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    DateTime last = DateTime.UtcNow, lastGeo = DateTime.MinValue;
    IntPtr hwnd = IntPtr.Zero;
    double groundPx, minX, maxX = 1000;
    int lastMx, lastMy;

    public PetWindow()
    {
        view = new PetView(pet);

        // first launch ever -> play the "climbing out of a box" intro (marker file in LocalAppData)
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clawd");
            var flag = Path.Combine(dir, "intro_done");
            if (!File.Exists(flag))
            {
                pet.PlayIntro = true;
                Directory.CreateDirectory(dir);
                File.WriteAllText(flag, "1");
            }
        }
        catch { }
        try { if (File.Exists(NameFile)) { pet.Name = File.ReadAllText(NameFile).Trim(); pet.ShowName(); } } catch { }
        Content = view;
        Width = Pet.NormalW; Height = Pet.WinH;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) pet.OnClick();
        };

        // Right click menu: test hooks so you don't have to wait for 22:00 / hunger.
        ContextMenu = new ContextMenu
        {
            ItemsSource = new[]
            {
                Item("Дать имя…", AskName),
                Item("Панель Клавда", () => Stats.PanelVisible = !Stats.PanelVisible),
#if DEBUG   // test hooks: only in Debug builds (dotnet run), never in the released exe
                Item("Хочу есть (тест)", () => pet.Hunger = 1),
                Item("Спать (тест)", () => pet.ForceNight = true),
                Item("Разбудить (тест)", () => pet.ForceNight = false),
                Item("Реальное время", () => pet.ForceNight = null),
                Item("Цветок (тест)", () => pet.RequestFlower = true),
                Item("Экспедиция Zx1 (тест)", () => pet.RequestZx = true),
                Item("Коробка (тест)", () => pet.RequestBox = true),
                Item("Декабрь: коробки (тест)", () => World.ForceDecember = !World.ForceDecember),
                Item("Tux 28.12 (тест)", () => { World.ForceDecember = true; World.RequestTux = true; }),
                Item("Уход с дверью (тест, сначала «Спать»)", () => pet.RequestStorm = true),
#endif
                Item("Выход", Quit),
            }
        };

        Opened += (_, _) =>
        {
            hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            Win.MakeToolWindow(hwnd);
            RefreshGeometry();
            timer.Start();
        };
        Stats.Load();
        Closed += (_, _) => Stats.Save();
        timer.Tick += (_, _) => OnTick();
    }

    static string NameFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clawd", "name.txt");

    void AskName()
    {
        new NameDialog(pet.Name, n =>
        {
            pet.Name = n;
            try { Directory.CreateDirectory(Path.GetDirectoryName(NameFile)!); File.WriteAllText(NameFile, n); } catch { }
            pet.ShowName();
        }).Show();
    }

    static MenuItem Item(string header, Action a)
    {
        var m = new MenuItem { Header = header };
        m.Click += (_, _) => a();
        return m;
    }

    static void Quit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown();
    }

    void RefreshGeometry()
    {
        lastGeo = DateTime.UtcNow;
        var scr = Screens.Primary;
        if (scr == null) return;
        var b = scr.Bounds;
        // Windows with a bottom taskbar: stand on its top edge. Otherwise (Linux etc.): bottom of the work area.
        if (Win.TryGetTaskbar(out var tb) && tb.Width > tb.Height && tb.Y > b.Y + b.Height / 2)
        {
            groundPx = tb.Y; minX = tb.X; maxX = tb.Right;
        }
        else
        {
            groundPx = scr.WorkingArea.Bottom; minX = b.X; maxX = b.Right;
        }
        Win.Raise(hwnd);
    }

    void OnTick()
    {
        var now = DateTime.UtcNow;
        double dt = Math.Min((now - last).TotalSeconds, 0.1);
        last = now;
        double scale = RenderScaling;

        if ((now - lastGeo).TotalSeconds >= 1) RefreshGeometry();

        if (Win.TryGetCursor(out int mx, out int my))
        {
            if (mx != lastMx || my != lastMy) { pet.MouseIdle = 0; lastMx = mx; lastMy = my; }
            else pet.MouseIdle += dt;
        }

        Stats.Tick(dt, Win.IdleSeconds(), pet.IsAsleep);
        Audio.Poll(dt);
        pet.Tick(dt, DateTime.Now, minX, maxX, scale);

        if (Math.Abs(Width - pet.WinW) > 0.5) Width = pet.WinW;
        Position = new PixelPoint(
            (int)(pet.X - pet.WinW * scale / 2),
            (int)(groundPx - (Pet.FeetY - Pet.Sink) * scale));
        view.InvalidateVisual();
    }
}
