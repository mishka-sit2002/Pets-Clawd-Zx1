using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd;

/// <summary>Shared flags (set from the context menu for testing).</summary>
static class World
{
    public static bool ForceDecember, RequestTux;
}

/// <summary>One box in the corner with a mascot inside. Month = 0 means the release date is not announced yet.</summary>
class MascotSlot
{
    public string Label = "";
    public int Kind;            // which placeholder sprite (see Sprites.Mascot)
    public int Month, Day;
    public bool Released(DateTime n) => Month > 0 && n >= new DateTime(n.Year, Month, Day);
}

static class Mascots
{
    static readonly string[] Mon = { "ЯНВ", "ФЕВ", "МАР", "АПР", "МАЙ", "ИЮН", "ИЮЛ", "АВГ", "СЕН", "ОКТ", "НОЯ", "ДЕК" };
    public static string MonthText(MascotSlot s) => s.Month > 0 ? Mon[s.Month - 1] : "ДАТА";
    public static string DayText(MascotSlot s, DateTime n) => s.Month == 0 ? "?" : s.Released(n) ? "ok" : s.Day.ToString();

    // Order = box position: 0 bottom-left, 1 bottom-right, 2 top. The real list (up to 20) comes after Nov 29.
    public static readonly MascotSlot[] Slots =
    {
        new() { Label = "Zx1 CBT", Kind = 0 },
        new() { Label = "OBT", Kind = 1 },
        new() { Label = "...", Kind = 2, Month = 12, Day = 28 },   // Tux: Linus Torvalds' birthday
    };
}

/// <summary>Draws the stack of boxes in the taskbar corner, the click "peek" and the Dec 28 Tux scene.</summary>
class CornerView : Control
{
    public const double W = 300, Duration = 13.6, PeekDuration = 5.0;
    public double T = -1;      // Tux scene time; < 0 = no scene
    public int Peek = -1;      // box being peeked from
    public double P;           // peek time
    public double Anim;

    public bool Busy => T >= 0 || Peek >= 0;

    static double Seg(double t, double a, double b) => Math.Clamp((t - a) / (b - a), 0, 1);
    static double Ease(double x) => x * x * (3 - 2 * x);
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    static readonly double[] BoxX = { W - 76, W - 30, W - 53 };
    static double Floor(int i) => i == 2 ? Pet.FeetY - 26 : Pet.FeetY;

    public int HitBox(Point p)
    {
        double fy = Pet.FeetY;
        if (p.Y >= fy - 52 && p.Y < fy - 26) return Math.Abs(p.X - BoxX[2]) <= 22 ? 2 : -1;
        if (p.Y >= fy - 26 && p.Y <= fy)
        {
            if (Math.Abs(p.X - BoxX[0]) <= 22) return 0;
            if (Math.Abs(p.X - BoxX[1]) <= 22) return 1;
        }
        return -1;
    }

    public void StartPeek(int i) { Peek = i; P = 0; Stats.BoxClicked(); }

    public override void Render(DrawingContext c)
    {
        double fy = Pet.FeetY;
        bool tux = T >= 0;
        var now = DateTime.Now;

        // every ~20 s one random box shakes a little: something is alive in there
        int fidgetBox = (int)(Anim / 20) % 3;
        double fidgetPh = Anim % 20;

        for (int i = 0; i < 3; i++)
        {
            var slot = Mascots.Slots[i];
            double x = BoxX[i], floor = Floor(i), open = 0, wob = 0;
            bool peeking = Peek == i, tuxHere = i == 2 && tux;

            if (tuxHere)
            {
                open = Ease(Seg(T, 1.5, 2.1)) * (1 - Ease(Seg(T, 12.6, 13.4)));
                if (T > 0.4 && T < 1.5) wob = Math.Sin(T * 40) * 1.2;
            }
            else if (peeking) open = 0.35 * Ease(Seg(P, 0, 0.6)) * (1 - Ease(Seg(P, 4.0, 4.6)));
            else if (!Busy && fidgetBox == i && fidgetPh < 0.6) wob = Math.Sin(fidgetPh * 50);

            using (c.PushTransform(Matrix.CreateTranslation(wob, 0)))
            {
                Sprites.MiniBox(c, x, floor, slot.Label, 0, false);

                if (tuxHere)
                {
                    if (T < 3.2)
                    {
                        double ty = Lerp(floor + 36, floor - 14, Ease(Seg(T, 2.1, 3.2)));
                        using (c.PushClip(new Rect(0, 0, W, floor)))
                            Sprites.Tux(c, x, ty, 0, false, T);
                    }
                }
                else if (peeking)
                {
                    double off = P < 3.6 ? 40 * (1 - Ease(Seg(P, 0.6, 1.4))) : 40 * Ease(Seg(P, 3.6, 4.2));
                    double lookY = P > 1.4 && P < 3.6 ? 1 : 0;
                    bool sad = !slot.Released(now) && P > 2.4 && P < 3.9;
                    double baseY = floor - (slot.Kind == 2 ? 2 : 10) + off;
                    using (c.PushClip(new Rect(0, 0, W, floor)))
                        Sprites.Mascot(c, slot.Kind, x, baseY, 0, lookY, sad, Anim);
                }

                Sprites.MiniBox(c, x, floor, slot.Label, open, true);
            }
        }

        // calendar stuck on the front of the box the mascot is looking at
        if (Peek >= 0)
        {
            var slot = Mascots.Slots[Peek];
            double a = Seg(P, 1.2, 1.6) * (1 - Seg(P, 3.4, 3.9));
            if (a > 0)
                using (c.PushOpacity(a))
                    Sprites.Calendar(c, BoxX[Peek], Floor(Peek) - 1, Mascots.MonthText(slot), Mascots.DayText(slot, now));
        }

        if (!tux || T < 3.2) return;

        // Tux outside: hop down, waddle left, birthday cake, bubble, poof
        double topFloor = Floor(2), xT = BoxX[2], px, py;
        bool walking = false;
        if (T < 4.4)
        {
            double u = Seg(T, 3.2, 4.4);
            px = Lerp(xT, 190, Ease(u));
            py = Lerp(topFloor - 14, fy, u) - 20 * Math.Sin(Math.PI * u);
        }
        else if (T < 6.6)
        {
            double u = Seg(T, 4.4, 6.6);
            px = Lerp(190, 110, Ease(u));
            py = fy - Math.Abs(Math.Sin(T * 14)) * 2;
            walking = true;
        }
        else { px = 110; py = fy; }

        double cakeOp = Seg(T, 7.0, 7.6) * (1 - Seg(T, 12.0, 13.0));
        if (cakeOp > 0)
            using (c.PushOpacity(cakeOp)) Sprites.Cake(c, 150, fy, Anim);

        double tuxOp = 1 - Seg(T, 12.0, 12.6);
        if (tuxOp > 0)
        {
            double flap = T > 7.4 && T < 11.6 ? Math.Sin(T * 8) : 0;
            using (c.PushOpacity(tuxOp)) Sprites.Tux(c, px, py, flap, walking, T);
        }
        if (T >= 7.4 && T < 11.6)
            Sprites.Bubble(c, Math.Clamp(px, 95, 205), fy - 48, "С днём рождения, Линус!");

        if (T >= 12.0 && T < 13.0)   // poof
        {
            double r = 22 * Seg(T, 12.0, 12.8);
            using (c.PushOpacity(1 - Seg(T, 12.3, 13.0)))
                for (int i = 0; i < 8; i++)
                {
                    double ang = i * Math.PI / 4;
                    c.DrawRectangle(Brushes.White, null, new Rect(110 + Math.Cos(ang) * r - 2.5, fy - 18 + Math.Sin(ang) * r - 2.5, 5, 5));
                }
        }
    }
}

/// <summary>Small topmost window at the right end of the taskbar; visible only in December.</summary>
class CornerWindow : Window
{
    readonly CornerView view = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    DateTime last = DateTime.UtcNow, lastGeo = DateTime.MinValue, lastTux = DateTime.MinValue;
    IntPtr hwnd = IntPtr.Zero;
    double groundPx, rightPx = 1000;

    public CornerWindow()
    {
        Content = view;
        Width = CornerView.W; Height = Pet.WinH;
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
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || view.Busy) return;
            int i = view.HitBox(e.GetPosition(view));
            if (i >= 0) view.StartPeek(i);
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
        { groundPx = tb.Y; rightPx = tb.Right; }
        else
        { groundPx = scr.WorkingArea.Bottom; rightPx = b.Right; }
        Win.Raise(hwnd);
    }

    void OnTick()
    {
        var now = DateTime.Now; var utc = DateTime.UtcNow;
        double dt = Math.Min((utc - last).TotalSeconds, 0.1); last = utc;

        bool show = World.ForceDecember || now.Month == 12;
        if (!show) { if (IsVisible) Hide(); return; }
        if (!IsVisible)
        {
            Show();
            hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            Win.MakeToolWindow(hwnd);
            lastGeo = DateTime.MinValue;
        }
        if ((utc - lastGeo).TotalSeconds >= 1) RefreshGeometry();

        if (view.Peek >= 0)
        {
            view.P += dt;
            if (view.P >= CornerView.PeekDuration) view.Peek = -1;
        }

        if (view.T < 0)
        {
            bool dayOk = now.Month == 12 && now.Day == 28 && now.Hour >= 8 && now.Hour < 22 && lastTux.Date != now.Date;
            if (view.Peek < 0 && (World.RequestTux || dayOk)) { World.RequestTux = false; lastTux = now; view.T = 0; }
        }
        else
        {
            view.T += dt;
            if (view.T >= CornerView.Duration) view.T = -1;
        }
        view.Anim += dt;

        double scale = RenderScaling;
        Position = new PixelPoint(
            (int)(rightPx - (CornerView.W + 6) * scale),
            (int)(groundPx - (Pet.FeetY - Pet.Sink) * scale));
        view.InvalidateVisual();
    }
}
