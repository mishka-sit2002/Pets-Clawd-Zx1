using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd;

/// <summary>Draws the room cross-section (mirrored for left corners) plus the pixel frame, tab and buttons.</summary>
class RoomView : Control
{
    public readonly GameRoom Room;
    public bool Flip;
    public double E;   // eased expansion 0..1
    public double Vy0;  // top of the visible part of the building (room coordinates)

    public static readonly (string Text, double W)[] Btns = { ("Румтур", 52), ("Угол", 40), ("Выйти", 46), ("Свернуть", 62) };

    public RoomView(GameRoom r) { Room = r; }

    public Rect TabRect(double w) => new(w - 66, 5, 60, 15);

    public Rect BtnRect(int i, double w)
    {
        double total = Btns.Sum(b => b.W) + 4 * (Btns.Length - 1), x = w - 6 - total;
        for (int k = 0; k < i; k++) x += Btns[k].W + 4;
        return new Rect(x, 4, Btns[i].W, 15);
    }

    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 10 || h < 10) return;

        // the room is anchored to its bottom-right corner: the compact window shows just the viewing zone
        var m = Matrix.CreateTranslation(w - GameRoom.TotalW, -Vy0);
        if (Flip) m = m * Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(w, 0);

        using (c.PushClip(new Rect(0, 0, w, h)))
        {
            using (c.PushTransform(m)) Room.Draw(c, (byte)(110 + 125 * E));
            foreach (var o in Room.Overlays)   // text is never mirrored
            {
                var p = m.Transform(new Point(o.X, o.Y));
                if (o.Cloud) Px.Cloud(c, p, o.Text, w);
                else Px.Label(c, p, o.Text, 7, Brushes.White);
            }
            Frame(c, w, h);
            if (E > 0.85) Buttons(c, w);
            else if (E < 0.3) Tab(c, w);
        }
    }

    static void Frame(DrawingContext c, double w, double h)
    {
        var ink = Px.Ink; var org = Px.Orange;
        Sprites.R(c, ink, 0, 0, w, 2); Sprites.R(c, ink, 0, h - 2, w, 2);
        Sprites.R(c, ink, 0, 0, 2, h); Sprites.R(c, ink, w - 2, 0, 2, h);
        Sprites.R(c, org, 2, 2, w - 4, 1); Sprites.R(c, org, 2, h - 3, w - 4, 1);
        Sprites.R(c, org, 2, 2, 1, h - 4); Sprites.R(c, org, w - 3, 2, 1, h - 4);
        foreach (var (cx, cy) in new[] { (0.0, 0.0), (w - 5, 0.0), (0.0, h - 5), (w - 5, h - 5) })
            Sprites.R(c, org, cx, cy, 5, 5);
    }

    static void Button(DrawingContext c, Rect r, string text)
    {
        Sprites.R(c, Px.Ink, r.X, r.Y, r.Width, r.Height);
        Sprites.R(c, Px.B(30, 34, 48, 240), r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        Sprites.R(c, Px.Orange, r.X + 1, r.Y + 1, r.Width - 2, 1);
        var ft = Sprites.Txt(text, 8, Brushes.White);
        c.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + 3));
    }

    void Tab(DrawingContext c, double w) => Button(c, TabRect(w), "▼ Комната");

    void Buttons(DrawingContext c, double w)
    {
        for (int i = 0; i < Btns.Length; i++) Button(c, BtnRect(i, w), Btns[i].Text);
    }
}

/// <summary>
/// The viewer window (always on top, in a chosen screen corner). Compact = click-through, shows only the
/// viewing zone; the tab (or Alt+click) smoothly expands it into the whole room cross-section.
/// </summary>
class GameWindow : Window
{
    const double CW = 200, CH = 150;

    readonly GameRoom room = new();
    readonly RoomView view;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    DateTime last = DateTime.UtcNow, lastRaise = DateTime.MinValue;
    IntPtr hwnd = IntPtr.Zero;
    bool expanded, leaving, clickThrough;
    double exp, fade = 1;

    public GameWindow()
    {
        view = new RoomView(room);
        Content = view;
        Width = CW; Height = CH;
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
            var pp = e.GetCurrentPoint(this);
            if (!pp.Properties.IsLeftButtonPressed) return;
            var pt = pp.Position;
            double w = view.Bounds.Width;
            if (!expanded)
            {
                if (view.TabRect(w).Contains(pt) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) SetExpanded(true);
                return;
            }
            for (int i = 0; i < RoomView.Btns.Length; i++)
                if (view.BtnRect(i, w).Contains(pt)) { OnButton(i); return; }
        };

        timer.Tick += (_, _) => OnTick();
        timer.Start();
    }

    ViewerCorner Corner => room.Kind == HouseKind.Window ? GameConfig.Current.WindowCorner : GameConfig.Current.Corner;

    void SetExpanded(bool v)
    {
        if (expanded == v || leaving) return;
        expanded = v;
        if (v) { if (!room.TourSeen) room.RequestTour(); }   // first time: room tour
        else room.CancelTour();
    }

    void OnButton(int i)
    {
        switch (i)
        {
            case 0: room.RequestTour(); break;
            case 1:   // cycle the screen corner
                var cfg = GameConfig.Current;
                var next = (ViewerCorner)(((int)Corner + 1) % 4);
                if (room.Kind == HouseKind.Window) cfg.WindowCorner = next; else cfg.Corner = next;
                GameConfig.Save();
                break;
            case 2: GameMode.Enabled = false; break;
            case 3: SetExpanded(false); break;
        }
    }

    void Open()
    {
        GameMode.ViewerOpen = true;
        leaving = false; expanded = false; exp = 0; fade = 0; Opacity = 0;
        room.Setup(GameMode.House);
        ApplyLayout(CW, CH);
        Show();
        hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        Win.MakeToolWindow(hwnd);
        Win.SetClickThrough(hwnd, true);
        clickThrough = true;
        room.Arrive();
    }

    void ApplyLayout(double w, double h)
    {
        if (Math.Abs(Width - w) > 0.3) Width = w;
        if (Math.Abs(Height - h) > 0.3) Height = h;
        var scr = Screens.Primary;
        if (scr == null) return;
        var wa = scr.WorkingArea;
        double s = RenderScaling;
        int m = (int)(10 * s), wpx = (int)Math.Round(w * s), hpx = (int)Math.Round(h * s);
        var corner = Corner;
        bool right = corner is ViewerCorner.BottomRight or ViewerCorner.TopRight;
        bool bottom = corner is ViewerCorner.BottomRight or ViewerCorner.BottomLeft;
        Position = new PixelPoint(right ? wa.Right - wpx - m : wa.X + m, bottom ? wa.Bottom - hpx - m : wa.Y + m);
    }

    void OnTick()
    {
        var now = DateTime.UtcNow;
        double dt = Math.Min((now - last).TotalSeconds, 0.1);
        last = now;

        if (!GameMode.ViewerOpen)
        {
            if (GameMode.Enabled && GameMode.PetInside) Open();
            return;
        }

        if (!GameMode.Enabled && !leaving)   // switched off: collapse, wave goodbye, close
        {
            leaving = true; expanded = false;
            room.CancelTour();
            room.Leave();
        }

        double target = expanded ? 1 : 0;
        exp = target > exp ? Math.Min(target, exp + dt / 0.45) : Math.Max(target, exp - dt / 0.35);
        double e = exp * exp * (3 - 2 * exp);
        view.E = e;
        view.Flip = Corner is ViewerCorner.BottomLeft or ViewerCorner.TopLeft;
        view.Vy0 = 100 * (1 - e);

        room.Update(dt);

        if (leaving && room.LeaveDone && exp <= 0.001)
        {
            fade -= dt / 0.35;
            if (fade <= 0)
            {
                Hide();
                GameMode.ViewerOpen = false;
                leaving = false;
                return;
            }
        }
        else if (fade < 1) fade = Math.Min(1, fade + dt / 0.3);
        Opacity = Math.Clamp(fade, 0, 1);

        double w = CW + (GameRoom.TotalW - CW) * e, h = CH + (room.RoomH - CH) * e;
        ApplyLayout(w, h);

        if ((now - lastRaise).TotalSeconds >= 1) { lastRaise = now; Win.Raise(hwnd); }

        // click-through while compact; the tab (or Alt held) over the window makes it clickable
        bool through = !expanded && exp < 0.02;
        if (through && Win.TryGetCursor(out int cx, out int cy))
        {
            double s = RenderScaling, lx = (cx - Position.X) / s, ly = (cy - Position.Y) / s;
            bool inside = lx >= 0 && ly >= 0 && lx < w && ly < h;
            if (inside && (view.TabRect(w).Contains(new Point(lx, ly)) || Win.IsAltDown())) through = false;
        }
        if (through != clickThrough) { Win.SetClickThrough(hwnd, through); clickThrough = through; }

        view.InvalidateVisual();
    }
}
