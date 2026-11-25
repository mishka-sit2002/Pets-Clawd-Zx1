using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd;

class StatsView : Control
{
    public bool Expanded;
    public double H = 40;

    static readonly string[] Names = { "Сытость", "Общение", "Привязанность", "АФК", "Ревность" };
    static readonly Color[] Cols =
    {
        Color.FromRgb(80, 200, 120), Color.FromRgb(90, 150, 255), Color.FromRgb(255, 110, 150),
        Color.FromRgb(160, 165, 180), Color.FromRgb(235, 90, 70),
    };

    static readonly IBrush Bg = new SolidColorBrush(Color.FromArgb(235, 24, 26, 34));
    static readonly Pen Border = new(new SolidColorBrush(Color.FromRgb(217, 119, 87)), 1);

    public override void Render(DrawingContext c)
    {
        double floor = H - 12;   // taskbar top edge in view coordinates
        var orange = new SolidColorBrush(Color.FromRgb(217, 119, 87));
        var ink = new SolidColorBrush(Color.FromRgb(30, 30, 30));

        if (!Expanded)
        {
            double y = floor - 24;
            c.DrawRectangle(Bg, Border, new Rect(4, y, 34, 22), 5, 5);
            Sprites.R(c, orange, 11, y + 6, 20, 9);                       // tiny crab icon
            Sprites.R(c, ink, 16, y + 8, 2, 4);
            Sprites.R(c, ink, 24, y + 8, 2, 4);
            foreach (double lx in new[] { 13, 17, 25, 29 }) Sprites.R(c, orange, lx, y + 15, 2, 3);
            return;
        }

        double top = floor - 2 - 97;
        c.DrawRectangle(Bg, Border, new Rect(4, top, 160, 97), 6, 6);
        double[] v =
        {
            (1 - Stats.Hunger) * 100, Stats.Together, Stats.Attachment,
            Math.Min(100, Stats.AfkSeconds / 18),   // 30 minutes without input = full bar
            Stats.Jealousy,
        };
        for (int i = 0; i < 5; i++)
        {
            double y = top + 6 + i * 18;
            c.DrawText(Sprites.Txt(Names[i], 9, Brushes.White), new Point(12, y));
            var val = Sprites.Txt(((int)Math.Round(v[i])) + "%", 9, new SolidColorBrush(Cols[i]));
            c.DrawText(val, new Point(156 - val.Width, y));
            Sprites.R(c, new SolidColorBrush(Color.FromRgb(55, 58, 70)), 12, y + 12, 144, 4);
            Sprites.R(c, new SolidColorBrush(Cols[i]), 12, y + 12, 144 * Math.Clamp(v[i], 0, 100) / 100, 4);
        }
    }
}

/// <summary>Small panel at the LEFT end of the taskbar: a tab you click to open Clawd's feelings.</summary>
class StatsWindow : Window
{
    readonly StatsView view = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    DateTime lastGeo = DateTime.MinValue;
    IntPtr hwnd = IntPtr.Zero;
    double groundPx, leftPx;

    public StatsWindow()
    {
        Content = view;
        Width = 44; Height = 40;
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
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) view.Expanded = !view.Expanded;
        };
        timer.Tick += (_, _) => OnTick();
        timer.Start();
    }

    void RefreshGeometry()
    {
        lastGeo = DateTime.UtcNow;
        var scr = Screens.Primary;
        if (scr == null) return;
        var b = scr.Bounds;
        if (Win.TryGetTaskbar(out var tb) && tb.Width > tb.Height && tb.Y > b.Y + b.Height / 2)
        { groundPx = tb.Y; leftPx = tb.X; }
        else
        { groundPx = scr.WorkingArea.Bottom; leftPx = b.X; }
        Win.Raise(hwnd);
    }

    void OnTick()
    {
        if (!Stats.PanelVisible) { if (IsVisible) Hide(); return; }
        if (!IsVisible)
        {
            Show();
            hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            Win.MakeToolWindow(hwnd);
            lastGeo = DateTime.MinValue;
        }
        if ((DateTime.UtcNow - lastGeo).TotalSeconds >= 1) RefreshGeometry();

        double h = view.Expanded ? 120 : 40, w = view.Expanded ? 170 : 44;
        if (Math.Abs(Height - h) > 0.5) Height = h;
        if (Math.Abs(Width - w) > 0.5) Width = w;
        view.H = h;

        double scale = RenderScaling;
        Position = new PixelPoint((int)(leftPx + 6 * scale), (int)(groundPx + 12 * scale - h * scale));
        view.InvalidateVisual();
    }
}
