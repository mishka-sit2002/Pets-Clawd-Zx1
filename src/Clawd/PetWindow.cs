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
    bool pressed, dragging;
    PixelPoint pressPt, lastPt;

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
        try { if (File.Exists(NameFile)) { pet.SetName(File.ReadAllText(NameFile).Trim(), false); pet.ShowName(); } } catch { }
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

        // click = poke, press + move = pick him up and carry him, moving over him = petting
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            pressed = true; dragging = false;
            pressPt = CursorPx(e);
            e.Pointer.Capture(this);
        };
        PointerEntered += (_, e) => lastPt = CursorPx(e);
        PointerMoved += (_, e) =>
        {
            var pt = CursorPx(e);
            double sc = RenderScaling;
            if (pressed)
            {
                if (!dragging && pet.CanDrag && (Math.Abs(pt.X - pressPt.X) > 6 || Math.Abs(pt.Y - pressPt.Y) > 6))
                { dragging = true; pet.BeginDrag(); }
                if (dragging)
                {
                    pet.X = pt.X;
                    pet.Lift = Math.Max(0, (groundPx - pt.Y) / sc - 16);
                }
            }
            else
            {
                double dx = pt.X - lastPt.X, dy = pt.Y - lastPt.Y;
                pet.Stroke(Math.Sqrt(dx * dx + dy * dy) / sc);
            }
            lastPt = pt;
        };
        PointerReleased += (_, e) =>
        {
            e.Pointer.Capture(null);
            if (dragging) pet.EndDrag();
            else if (pressed) pet.OnClick();
            pressed = dragging = false;
        };

        var gameItem = new MenuItem { Header = "[ ] Игровой режим" };
        gameItem.Click += (_, _) =>
        {
            if (pet.Form != Form.Clawd) return;   // Shlepa doesn't go to the movies
            GameMode.Enabled = !GameMode.Enabled;
            gameItem.Header = (GameMode.Enabled ? "[x] " : "[ ] ") + "Игровой режим";
        };

        // Right click menu
        ContextMenu = new ContextMenu
        {
            ItemsSource = new[]
            {
                Item("Дать имя…", AskName),
                Item("Панель Клавда", () => Stats.PanelVisible = !Stats.PanelVisible),
                gameItem,
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

    PixelPoint CursorPx(Avalonia.Input.PointerEventArgs e) =>
        Win.TryGetCursor(out int x, out int y) ? new PixelPoint(x, y) : this.PointToScreen(e.GetPosition(this));

    static string NameFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clawd", "name.txt");

    void AskName()
    {
        new NameDialog(pet.Name, n =>
        {
            pet.SetName(n, true);
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

        if (pet.State == State.InGame)   // Clawd is in the game room: hide the tray window
        {
            if (IsVisible) Hide();
            return;
        }
        if (!IsVisible)
        {
            Show();
            hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            Win.MakeToolWindow(hwnd);
            RefreshGeometry();
        }

        if (Math.Abs(Width - pet.WinW) > 0.5) Width = pet.WinW;
        Position = new PixelPoint(
            (int)(pet.X - pet.WinW * scale / 2),
            (int)(groundPx - (Pet.FeetY - Pet.Sink) * scale - pet.Lift * scale));
        view.InvalidateVisual();
    }
}
