using Avalonia;
using Avalonia.Media;

namespace Clawd;

/// <summary>Two kinds of apartment in Clawd's building.</summary>
public enum HouseKind { Cinema, Window }

/// <summary>
/// The building around the apartment: the stairwell (подъезд, left of the flat), the server room under the
/// cinema flat (the TV breaks, Clawd fixes it there), the window flat (he looks at your real game through a
/// transparent window pane) and the little events of both.
/// </summary>
sealed partial class GameRoom
{
    public const double Ox = 64, TotalW = Ox + W, ServerTop = 250, ServerFloor = 336, ServerBottom = 350;
    const double WinL = 346, WinT = 124, WinW = 84, HoleL = 350, HoleT = 128, HoleW = 76, HoleH = 64;

    public HouseKind Kind;
    public double RoomH => Kind == HouseKind.Cinema ? ServerBottom : H;
    public bool TourSeen => Kind == HouseKind.Window ? GameConfig.Current.HasSeenWindowTour : GameConfig.Current.HasSeenRoomTour;

    double ay, stairDoor, srvDoor, rackOpen, fog, eventT = 120;
    int tv;                       // 0 = the TV works, 1 = glitching, 2 = dead
    bool slotEmpty, modBroken;

    public void Setup(HouseKind k) { Kind = k; tv = 0; fog = 0; }

    public void Draw(DrawingContext c, byte wallA)
    {
        Overlays.Clear();
        using (c.PushTransform(Matrix.CreateTranslation(Ox, 0)))
        {
            DrawStairwell(c);
            if (Kind == HouseKind.Cinema) DrawServer(c, wallA);
            DrawApartment(c, wallA);
        }
        for (int i = 0; i < Overlays.Count; i++) Overlays[i] = Overlays[i] with { X = Overlays[i].X + Ox };
    }

    double LookGaze() => Kind == HouseKind.Window
        ? Math.Clamp(0.3 + 0.7 * Math.Sin(anim * 0.6), -0.3, 1)      // scans the game outside the window
        : Math.Clamp(-0.2 + BallX * 1.3, -0.3, 1);                    // follows the ball on the virtual screen

    // path down the stairs: u 0..1 -> x in the stairwell and ay (how far below the apartment floor he is)
    static void Stair(double u, out double x, out double a)
    {
        if (u < 0.5) { double f = u / 0.5; x = Lerp(-12, -52, f); a = 52 * f; }
        else { double f = (u - 0.5) / 0.5; x = Lerp(-52, -12, f); a = 52 + 52 * f; }
    }

    void Stairs(bool down) =>
        Add(3.0, p => { Stair(down ? p : 1 - p, out double sx, out double sa); ax = sx; ay = sa; },
            () => { walking = true; dir = down ? 1 : -1; },
            () => { walking = false; dir = 1; ax = -12; ay = down ? ServerFloor - Floor : 0; });

    // =====================================================================================
    //  Events
    // =====================================================================================

    /// <summary>The TV breaks, he goes down to the server room, swaps the burnt module and comes back.</summary>
    void BeginRepair()
    {
        Mode = GameModeState.RepairingTv; planX = ax;
        Add(1.6, p => { tv = 1; eyes = Eyes.Wide; hop = Math.Abs(Math.Sin(p * 20)) * 2; }, () => say = "Ой! Экран мигает!", () => { say = null; tv = 2; });
        Add(1.3, p => eyes = Eyes.Sad, () => say = "Опять сервера...\nПойду чинить", () => say = null);
        Stand();
        Walk(8);
        Add(0.6, p => stairDoor = Ease(p));
        Walk(-12);
        Stairs(true);
        Add(0.6, p => srvDoor = Ease(p));
        Walk(118);
        Add(0.9, p => rackOpen = Ease(p), () => say = "Так, что тут...", () => say = null);
        Add(1.2, p => { eyes = Eyes.Wide; squash = 1 + 0.04 * Math.Sin(p * 30); }, null, () => { slotEmpty = true; held = Held.Module; modBroken = true; });
        Add(0.9, p => eyes = Eyes.Sad, () => say = "Модуль сгорел!", () => { say = null; held = Held.None; });
        Walk(270);
        Add(0.9, null, () => say = "Запасной модуль", () => { say = null; held = Held.Module; modBroken = false; });
        Walk(118);
        Add(1.3, p => squash = 1 + 0.05 * Math.Sin(p * 40), null, () => { held = Held.None; slotEmpty = false; tv = 0; });
        Add(0.7, p => rackOpen = 1 - Ease(p));
        Add(1.2, p => { eyes = Eyes.Happy; hop = Math.Abs(Math.Sin(p * Math.PI * 3)) * 4; }, () => say = "Починил!", () => say = null);
        Walk(-12);
        Add(0.4, p => srvDoor = 1 - Ease(p));
        Stairs(false);
        Add(0.4, p => stairDoor = 1 - Ease(p));
        Walk(ChairX);
        Sit(() => { Mode = GameModeState.WatchingPopcorn; eatT = 4; });
    }

    /// <summary>The window flat: the glass mists over, he wipes it with his claw.</summary>
    void BeginWipe()
    {
        Mode = GameModeState.WipingWindow; planX = ax;
        Add(4.0, p => fog = p);
        Add(0.9, p => eyes = Eyes.Sad, () => say = "Окно запотело!", () => say = null);
        Stand();
        Walk(340);
        Add(3.2, p => { fog = 1 - p; wave = anim; eyes = Eyes.Happy; ax = 340 + Math.Sin(p * Math.PI * 6) * 6; }, null, () => { fog = 0; ax = 340; });
        Add(0.9, p => eyes = Eyes.Happy, () => say = "Теперь видно!", () => say = null);
        Walk(ChairX);
        Sit(() => { Mode = GameModeState.WatchingPopcorn; eatT = 4; });
    }

    // =====================================================================================
    //  Drawing
    // =====================================================================================
    void DrawWall(DrawingContext c, byte a)
    {
        var wall = B(34, 38, 56, a);
        if (Kind != HouseKind.Window) { R(c, wall, 0, 0, W, Floor); return; }
        var g = new StreamGeometry();
        using (var cx = g.Open())   // the wall with a see-through hole: you look at the real desktop through it
        {
            cx.SetFillRule(FillRule.EvenOdd);
            cx.BeginFigure(new Point(0, 0), true);
            cx.LineTo(new Point(W, 0)); cx.LineTo(new Point(W, Floor)); cx.LineTo(new Point(0, Floor));
            cx.EndFigure(true);
            cx.BeginFigure(new Point(HoleL, HoleT), true);
            cx.LineTo(new Point(HoleL + HoleW, HoleT)); cx.LineTo(new Point(HoleL + HoleW, HoleT + HoleH)); cx.LineTo(new Point(HoleL, HoleT + HoleH));
            cx.EndFigure(true);
        }
        c.DrawGeometry(wall, null, g);
    }

    void DrawStairwell(DrawingContext c)
    {
        R(c, B(52, 56, 74), -Ox, 0, Ox, RoomH);
        R(c, B(38, 42, 58), -Ox, 0, Ox, 12);
        var step = B(96, 104, 128); var riser = B(70, 76, 98); var rail = B(130, 136, 160);
        void Flight(double x0, double y0, double x1, double y1)
        {
            for (int i = 0; i <= 12; i++)
            {
                double f = i / 12.0, sx = Lerp(x0, x1, f), sy = Lerp(y0, y1, f);
                R(c, step, sx - 4, sy, 8, 3); R(c, riser, sx - 4, sy + 3, 8, 2);
            }
            Px.Line(c, rail, x0, y0 - 14, x1, y1 - 14, 1.5);
        }
        R(c, riser, -Ox, Floor, Ox, 4);
        R(c, riser, -Ox, 128, Ox, 4);
        Flight(-12, Floor, -52, 180); Flight(-52, 180, -12, 128);   // up: the other flats (for the other pets)
        if (Kind == HouseKind.Cinema)
        {
            Flight(-12, Floor, -52, Floor + 52); Flight(-52, Floor + 52, -12, ServerFloor);
            R(c, riser, -Ox, ServerFloor, Ox, 4);
        }

        // flat 1 = Clawd, flat 2 = a neighbour who will move in later
        Sprites.DoorBack(c, -10, Floor); Sprites.DoorFront(c, -10, Floor, stairDoor, 0);
        R(c, Px.Orange, -22, Floor - 58, 24, 9); Overlays.Add(new Ov(-10, Floor - 58, "кв.1", false));
        Sprites.DoorBack(c, -10, 128); Sprites.DoorFront(c, -10, 128, 0, 0);
        R(c, B(90, 96, 120), -22, 70, 24, 9); Overlays.Add(new Ov(-10, 70, "кв.2", false));
        if (Kind == HouseKind.Cinema)
        {
            Sprites.DoorBack(c, -10, ServerFloor); Sprites.DoorFront(c, -10, ServerFloor, srvDoor, srvShake);
            R(c, B(80, 200, 255), -22, ServerFloor - 58, 24, 9); Overlays.Add(new Ov(-10, ServerFloor - 58, "СЕРВ.", false));
        }
    }

    void DrawRack(DrawingContext c, double x, int idx)
    {
        double top = ServerFloor - 74;
        R(c, B(40, 44, 60), x, top, 34, 74);
        Outline(c, Px.Ink, x, top, 34, 74);
        bool broken = idx == 1 && tv != 0;
        for (int s = 0; s < 7; s++)
        {
            double sy = top + 4 + s * 9.5;
            bool hole = idx == 1 && s == 3 && slotEmpty;
            R(c, hole ? B(10, 12, 18) : B(58, 64, 86), x + 3, sy, 28, 8);
            if (hole) continue;
            bool bad = broken && s == 3, on = ((int)(anim * 3) + s + idx) % 4 != 0;
            R(c, bad ? ((int)(anim * 8) % 2 == 0 ? Red : B(120, 20, 20)) : on ? B(80, 230, 120) : B(30, 90, 50), x + 5, sy + 3, 3, 2);
            R(c, B(24, 26, 36), x + 11, sy + 2, 16, 4);
        }
        if (idx != 1) return;
        if (broken && !slotEmpty)   // smoke from the burnt module
            for (int i = 0; i < 3; i++)
            {
                double u = (anim * 0.6 + i / 3.0) % 1;
                R(c, B(180, 180, 190, (byte)(170 * (1 - u))), x + 14 + Math.Sin(u * 6 + i) * 4, top - u * 24, 5, 5);
            }
        double lw = 32 * (1 - rackOpen);   // glass door, hinged on the right
        if (lw > 0.5)
        {
            R(c, B(70, 80, 108, 235), x + 33 - lw, top + 1, lw, 72);
            if (lw > 8) R(c, Handle, x + 33 - lw + 3, top + 32, 2, 10);
        }
    }

    void DrawServer(DrawingContext c, byte a)
    {
        byte a2 = (byte)Math.Min(255, a + 20);
        R(c, B(24, 28, 40, a2), 0, ServerTop, W, ServerFloor - ServerTop);
        R(c, B(64, 70, 92), 0, ServerTop + 4, W, 4);
        R(c, B(44, 50, 70, a2), 0, ServerFloor, W, ServerBottom - ServerFloor);
        Px.Line(c, B(20, 20, 26), 0, 270, W, 270, 2);   // cables under the ceiling
        DrawRack(c, 90, 0); DrawRack(c, 130, 1); DrawRack(c, 170, 2);

        // workbench: spare modules and a toolbox
        R(c, WoodDark, 250, ServerFloor - 22, 84, 4);
        R(c, WoodDark, 254, ServerFloor - 18, 3, 18); R(c, WoodDark, 327, ServerFloor - 18, 3, 18);
        if (held != Held.Module) { Px.Module(c, 272, ServerFloor - 22, false); Px.Module(c, 291, ServerFloor - 22, false); }
        R(c, Red, 304, ServerFloor - 30, 22, 8); R(c, Amber, 312, ServerFloor - 33, 6, 3);

        // wall monitor: TV status
        R(c, Px.Ink, 262, ServerTop + 16, 52, 34); R(c, B(8, 14, 24), 264, ServerTop + 18, 48, 30);
        for (int i = 0; i < 8; i++)
            R(c, tv == 0 ? B(80, 230, 120) : (int)(anim * 6) % 2 == 0 ? Red : B(120, 20, 20), 268 + i * 5, ServerTop + 40 - (tv == 0 ? i % 3 * 4 : 14 - i * 2), 4, 3);
        Overlays.Add(new Ov(288, ServerTop + 8, "СЕРВЕРНАЯ", false));

        // fan and UPS
        if ((int)(anim * 10) % 2 == 0) { R(c, Metal, 386, ServerTop + 20, 30, 4); R(c, Metal, 399, ServerTop + 7, 4, 30); }
        else { Px.Line(c, Metal, 388, ServerTop + 10, 414, ServerTop + 34, 3); Px.Line(c, Metal, 414, ServerTop + 10, 388, ServerTop + 34, 3); }
        R(c, Metal, 350, ServerFloor - 30, 28, 30); R(c, tv == 0 ? B(80, 230, 120) : Red, 354, ServerFloor - 26, 4, 3);

        DrawJunk(c);
        if (bedA > 0.01 && bedInServer)   // night mattress on the floor
            using (c.PushOpacity(bedA))
            {
                R(c, B(120, 128, 160), MattX - 36, ServerFloor - MattLift, 72, MattLift);
                R(c, B(150, 158, 190), MattX - 36, ServerFloor - MattLift, 72, 2);
                R(c, B(235, 238, 250), MattX - 34, ServerFloor - MattLift - 5, 14, 5);   // pillow
            }
    }

    /// <summary>Burnt-out server parts dumped on the floor next to the sleeping corner.</summary>
    void DrawJunk(DrawingContext c)
    {
        double f = ServerFloor;
        var pcb = B(34, 40, 36); var burn = B(18, 18, 20);
        Px.Module(c, 93, f, true);                                                    // charred module
        var m = Matrix.CreateTranslation(-106, -f) * Matrix.CreateRotation(-0.35) * Matrix.CreateTranslation(106, f);
        using (c.PushTransform(m)) Px.Module(c, 106, f, true);                        // another one leaning on the rack
        R(c, burn, 82, f - 3, 18, 3); R(c, Red, 86, f - 4, 3, 1); R(c, Metal, 92, f - 4, 5, 1);   // flat dead boards
        R(c, pcb, 112, f - 4, 16, 4); R(c, Red, 122, f - 5, 2, 1);
        R(c, Metal, 126, f - 8, 14, 5); R(c, Handle, 128, f - 7, 9, 1);                // dead disk
        if ((int)(anim * 2) % 3 == 0) R(c, Red, 137, f - 7, 2, 2);
        Px.Line(c, B(20, 20, 26), 144, f - 2, 150, f - 7, 2); Px.Line(c, B(20, 20, 26), 150, f - 7, 156, f - 2, 2);   // tangled cable
        Px.Line(c, B(50, 54, 70), 148, f - 2, 158, f - 3, 2);
    }

    void DrawWindow(DrawingContext c)
    {
        // the pane itself is a hole in the wall: the real desktop / your game shows through
        for (double ry = HoleT + 6; ry < HoleT + HoleH; ry += 18) Px.Line(c, B(255, 255, 255, 38), HoleL + 6, ry + 14, HoleL + 22, ry, 2);   // glass glare
        if (fog > 0.01) R(c, B(235, 240, 250, (byte)(165 * Math.Clamp(fog, 0, 1))), HoleL, HoleT, HoleW, HoleH);
        R(c, WoodDark, WinL, WinT, WinW, 4); R(c, WoodDark, WinL, HoleT + HoleH, WinW, 4);
        R(c, WoodDark, WinL, WinT, 4, HoleH + 8); R(c, WoodDark, WinL + WinW - 4, WinT, 4, HoleH + 8);
        R(c, WoodDark, WinL + 40, HoleT, 3, HoleH); R(c, WoodDark, HoleL, HoleT + 30, HoleW, 3);
        R(c, Wood, WinL - 4, WinT + HoleH + 8, WinW + 8, 5);                                    // sill
        R(c, B(150, 90, 60), WinL + 8, WinT + HoleH + 1, 10, 7);                                // flower pot
        foreach (var (lx, ly) in new[] { (0.0, 0.0), (-4.0, -5.0), (4.0, -6.0), (0.0, -10.0) }) R(c, B(70, 190, 100), WinL + 11 + lx, WinT + HoleH - 3 + ly, 4, 4);
        R(c, B(176, 66, 88), WinL - 12, WinT - 6, 14, HoleH + 14);                              // curtains
        R(c, B(176, 66, 88), WinL + WinW - 2, WinT - 6, 14, HoleH + 14);
        for (int i = 0; i < 3; i++) { R(c, B(206, 96, 118), WinL - 10 + i * 4, WinT - 4, 1.5, HoleH + 10); R(c, B(206, 96, 118), WinL + WinW + i * 4, WinT - 4, 1.5, HoleH + 10); }
    }
}
