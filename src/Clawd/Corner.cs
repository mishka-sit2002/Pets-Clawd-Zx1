using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd;

/// <summary>Shared flags (set from the context menu for testing).</summary>
static class World
{
    public static bool ForceDecember, RequestTux, AllReleased;   // test hooks
    public static int OutIdx = -1;                                // box whose mascot is out on the taskbar right now
    public static Action<int, double, double>? Outing;           // (slot, box screen X px, box floor) -> start the outing
}

/// <summary>One box in the corner with a mascot inside. Month = 0 means the release date is not announced yet.</summary>
class MascotSlot
{
    public string Label = "";
    public int Kind;            // which placeholder sprite (see Sprites.Mascot)
    public int Month, Day;
    public Art? Art;            // real pixel mascot (null = old placeholder Kind)
    public bool Released(DateTime n) => World.AllReleased || (Month > 0 && n >= new DateTime(n.Year, Month, Day));
}

static class Mascots
{
    static readonly string[] Mon = { "ЯНВ", "ФЕВ", "МАР", "АПР", "МАЙ", "ИЮН", "ИЮЛ", "АВГ", "СЕН", "ОКТ", "НОЯ", "ДЕК" };
    public static string MonthText(MascotSlot s) => s.Month > 0 ? Mon[s.Month - 1] : "ДАТА";
    public static string DayText(MascotSlot s, DateTime n) => s.Month == 0 ? "?" : s.Released(n) ? "ok" : s.Day.ToString();

    public const int PageSize = 8, TuxSlot = 10;
    public static int PageCount => (Slots.Length + PageSize - 1) / PageSize;

    static MascotSlot A(string label, Art art, int day) => new() { Label = label, Art = art, Month = 12, Day = day };

    // Pages of 8 boxes: 0 = "Zx1 Labs" (AI companies), then CBT / OBT / Tux and mascots 4..22 of the December list.
    // DATES ARE PLACEHOLDERS: mascot N comes out on December N, the Labs boxes on Dec 23-27 and 29-31 (28 = Tux).
    public static readonly MascotSlot[] Slots = Build();

    static MascotSlot[] Build()
    {
        var l = new List<MascotSlot>();
        int[] labDays = { 23, 24, 25, 26, 27, 29, 30, 31 };
        for (int i = 0; i < Arts.Labs.Length; i++) l.Add(A("Zx1 Labs", Arts.Labs[i], labDays[i]));
        l.Add(new() { Label = "Zx1 CBT", Kind = 0 });
        l.Add(new() { Label = "OBT", Kind = 1 });
        l.Add(new() { Label = "...", Kind = 2, Month = 12, Day = 28 });   // Tux: Linus Torvalds' birthday
        for (int i = 0; i < Arts.Dec.Length; i++) l.Add(A("...", Arts.Dec[i], 4 + i));
        return l.ToArray();
    }
}

/// <summary>Draws the stack of boxes in the taskbar corner, the click "peek" and the Dec 28 Tux scene.</summary>
class CornerView : Control
{
    public const double W = 300, Duration = 13.6, PeekDuration = 5.0;
    public double T = -1;      // Tux scene time; < 0 = no scene
    public int Peek = -1;      // slot being peeked from (global index)
    public double P;           // peek time
    public double Anim;

    public bool Busy => T >= 0 || Peek >= 0;

    static double Seg(double t, double a, double b) => Math.Clamp((t - a) / (b - a), 0, 1);
    static double Ease(double x) => x * x * (3 - 2 * x);
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public int Page;           // which page of 8 boxes is shown
    static readonly Rect PageBtn = new(78, Pet.FeetY - 22, 28, 18);

    // pyramid: 4 boxes on the floor, 3 above, 1 on top
    public static (double X, double Floor) Pos(int k)
    {
        double fy = Pet.FeetY;
        if (k < 4) return (W - 30 - 46 * k, fy);
        if (k < 7) return (W - 53 - 46 * (k - 4), fy - 26);
        return (W - 99, fy - 52);
    }

    int PageCount(int page) => Math.Min(Mascots.PageSize, Mascots.Slots.Length - page * Mascots.PageSize);

    public int HitBox(Point p)
    {
        for (int k = PageCount(Page) - 1; k >= 0; k--)
        {
            var (x, fl) = Pos(k);
            if (Math.Abs(p.X - x) <= 22 && p.Y >= fl - 26 && p.Y <= fl) return Page * Mascots.PageSize + k;
        }
        return -1;
    }

    public bool HitPage(Point p) => PageBtn.Contains(p);
    public void NextPage() => Page = (Page + 1) % Mascots.PageCount;

    public void StartPeek(int i) { Peek = i; P = 0; Stats.BoxClicked(); }

    public override void Render(DrawingContext c)
    {
        double fy = Pet.FeetY;
        bool tux = T >= 0;
        var now = DateTime.Now;
        if (tux) Page = Mascots.TuxSlot / Mascots.PageSize;   // the Tux box lives on this page

        int first = Page * Mascots.PageSize, count = PageCount(Page);

        // every ~20 s one random box shakes a little: something is alive in there
        int fidgetBox = (int)(Anim / 20) % count;
        double fidgetPh = Anim % 20;

        for (int k = 0; k < count; k++)
        {
            int i = first + k;
            if (World.OutIdx == i) continue;   // his box stands in the middle of the taskbar right now
            var slot = Mascots.Slots[i];
            var (x, floor) = Pos(k);
            double open = 0, wob = 0;
            bool peeking = Peek == i, tuxHere = tux && i == Mascots.TuxSlot;

            if (tuxHere)
            {
                open = Ease(Seg(T, 1.5, 2.1)) * (1 - Ease(Seg(T, 12.6, 13.4)));
                if (T > 0.4 && T < 1.5) wob = Math.Sin(T * 40) * 1.2;
            }
            else if (peeking) open = 0.35 * Ease(Seg(P, 0, 0.6)) * (1 - Ease(Seg(P, 4.0, 4.6)));
            else if (!Busy && fidgetBox == k && fidgetPh < 0.6) wob = Math.Sin(fidgetPh * 50);

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
                    bool sad = !slot.Released(now) && P > 2.4 && P < 3.9;   // not his day: sad face, then the lid closes
                    using (c.PushClip(new Rect(0, 0, W, floor)))
                    {
                        if (slot.Art != null) MascotArt.Draw(c, slot.Art, x, floor - 10 + off, Anim, lookY * 0.12, 1, 1, sad);
                        else Sprites.Mascot(c, slot.Kind, x, floor - (slot.Kind == 2 ? 2 : 10) + off, 0, lookY, sad, Anim);
                    }
                }

                Sprites.MiniBox(c, x, floor, slot.Label, open, true);
            }
        }

        // page switch button
        Sprites.R(c, Px.Ink, PageBtn.X, PageBtn.Y, PageBtn.Width, PageBtn.Height);
        Sprites.R(c, Px.B(70, 48, 28), PageBtn.X + 1, PageBtn.Y + 1, PageBtn.Width - 2, PageBtn.Height - 2);
        Sprites.R(c, Px.Orange, PageBtn.X + 1, PageBtn.Y + 1, PageBtn.Width - 2, 1);
        var pt = Sprites.Txt($"{Page + 1}/{Mascots.PageCount}", 8, Brushes.White);
        c.DrawText(pt, new Point(PageBtn.X + (PageBtn.Width - pt.Width) / 2, PageBtn.Y + 4));

        // calendar stuck on the front of the box the mascot is looking at
        if (Peek >= 0 && Peek / Mascots.PageSize == Page)
        {
            var slot = Mascots.Slots[Peek];
            var (cx, cf) = Pos(Peek - first);
            double a = Seg(P, 1.2, 1.6) * (1 - Seg(P, 3.4, 3.9));
            if (a > 0)
                using (c.PushOpacity(a))
                    Sprites.Calendar(c, cx, cf - 1, Mascots.MonthText(slot), Mascots.DayText(slot, now));
        }

        if (!tux || T < 3.2) return;

        // Tux outside: hop down, waddle left, birthday cake, bubble, poof
        var (xT, topFloor) = Pos(Mascots.TuxSlot - Page * Mascots.PageSize);
        double px, py;
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
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || view.Busy || World.OutIdx >= 0) return;
            var pos = e.GetPosition(view);
            if (view.HitPage(pos)) { view.NextPage(); return; }
            int i = view.HitBox(pos);
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
            var sl = Mascots.Slots[view.Peek];
            if (sl.Art != null && sl.Released(now) && view.P >= 3.0)
            {
                // it is his day: the box goes to the middle of the taskbar and he climbs out
                var (bx, fl) = CornerView.Pos(view.Peek - view.Page * Mascots.PageSize);
                World.Outing?.Invoke(view.Peek, Position.X + bx * RenderScaling, fl);
                view.Peek = -1;
            }
            else if (view.P >= CornerView.PeekDuration) view.Peek = -1;
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
