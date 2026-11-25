using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Clawd;

public enum Eyes { Open, Closed, Happy, Sad, Wide, Love, Angry, Sleepy, Wink }

/// <summary>All drawing. Everything is placeholder pixel-art made of rectangles: swap for real sprites later.</summary>
static class Sprites
{
    public const double PX = 4;        // size of one crab "pixel" in DIPs
    public const double BedTop = 14;   // mattress height above the floor

    static IBrush Br(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
    static readonly IBrush Orange = Br(217, 119, 87), OrangeDark = Br(190, 98, 68), Ink = Br(30, 30, 30);
    static readonly IBrush Pink = Br(255, 110, 150), Red = Br(220, 40, 40);
    static readonly IBrush Wood = Br(139, 94, 60), WoodDark = Br(110, 72, 44), Gold = Br(240, 190, 60);
    static readonly IBrush Mattress = Br(228, 230, 245), Blue = Br(76, 110, 245), BlueLight = Br(140, 165, 255);
    static readonly IBrush Rack = Br(58, 63, 75), Green = Br(80, 220, 120), Amber = Br(255, 170, 50);

    static readonly string[] Body =
    {
        "..BBBBBBBBB..",
        "..BBBBBBBBB..",
        "AABBBBBBBBBAA",
        "AABBBBBBBBBAA",
        "..BBBBBBBBB..",
        "..BBBBBBBBB..",
    };

    internal static void R(DrawingContext c, IBrush b, double x, double y, double w, double h) =>
        c.DrawRectangle(b, null, new Rect(x, y, w, h));

    internal static FormattedText Txt(string s, double size, IBrush b) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, b);

    // ---------------- Clawd ----------------
    public static void Crab(DrawingContext c, double x, double feetY, int dir, double squash,
                            Eyes eyes, bool walking, double anim, double wave = -1, double gaze = 0, double size = 1)
    {
        var m = Matrix.CreateScale(dir * size, squash * size) * Matrix.CreateTranslation(x, feetY);
        using (c.PushTransform(m))
        {
            double ox = -6.5 * PX, oy = -8 * PX;
            for (int r = 0; r < Body.Length; r++)
                for (int k = 0; k < 13; k++)
                {
                    char ch = Body[r][k];
                    if (ch == '.' || (wave >= 0 && ch == 'A' && k >= 11)) continue;   // right claw goes up when waving
                    R(c, ch == 'A' ? OrangeDark : Orange, ox + k * PX, oy + r * PX, PX, PX);
                }

            int[] legs = walking && (int)(anim * 8) % 2 == 1 ? new[] { 3, 5, 7, 9 } : new[] { 2, 4, 8, 10 };
            foreach (int l in legs) R(c, Orange, ox + l * PX, oy + 6 * PX, PX, 2 * PX);

            if (wave >= 0)   // raised, waving claw
            {
                double sw = Math.Sin(wave * 14) * PX * 0.9;
                R(c, OrangeDark, ox + 11 * PX, oy + 3 * PX, 2 * PX, PX);
                R(c, OrangeDark, ox + 12 * PX, oy + 1 * PX, PX, 2 * PX);
                R(c, OrangeDark, ox + 11 * PX + sw, oy - 1.5 * PX, 3 * PX, 2.5 * PX);
            }
            using var gz = c.PushTransform(Matrix.CreateTranslation(gaze * PX * 0.75, 0));   // eyes look left/right

            switch (eyes)
            {
                case Eyes.Open:
                    R(c, Ink, ox + 4 * PX, oy + 1 * PX, PX, 2 * PX);
                    R(c, Ink, ox + 8 * PX, oy + 1 * PX, PX, 2 * PX);
                    break;
                case Eyes.Sad:
                    R(c, Ink, ox + 4 * PX, oy + 2 * PX, PX, 2 * PX);
                    R(c, Ink, ox + 8 * PX, oy + 2 * PX, PX, 2 * PX);
                    break;
                case Eyes.Closed:   // "- -"
                    R(c, Ink, ox + 3 * PX, oy + 2 * PX, 2 * PX, PX * 0.7);
                    R(c, Ink, ox + 8 * PX, oy + 2 * PX, 2 * PX, PX * 0.7);
                    break;
                case Eyes.Wide:
                    R(c, Ink, ox + 3.6 * PX, oy + 0.6 * PX, 1.6 * PX, 3 * PX);
                    R(c, Ink, ox + 7.8 * PX, oy + 0.6 * PX, 1.6 * PX, 3 * PX);
                    R(c, Brushes.White, ox + 4.0 * PX, oy + 0.9 * PX, 0.6 * PX, 0.6 * PX);
                    R(c, Brushes.White, ox + 8.2 * PX, oy + 0.9 * PX, 0.6 * PX, 0.6 * PX);
                    break;
                case Eyes.Love:
                    foreach (int e in new[] { 3, 7 })
                    {
                        R(c, Pink, ox + e * PX, oy + 1 * PX, PX, PX);
                        R(c, Pink, ox + (e + 2) * PX, oy + 1 * PX, PX, PX);
                        R(c, Pink, ox + e * PX, oy + 2 * PX, 3 * PX, PX);
                        R(c, Pink, ox + (e + 1) * PX, oy + 3 * PX, PX, PX);
                    }
                    break;
                case Eyes.Angry:
                    R(c, Ink, ox + 4 * PX, oy + 2 * PX, PX, 1.5 * PX);
                    R(c, Ink, ox + 8 * PX, oy + 2 * PX, PX, 1.5 * PX);
                    R(c, Ink, ox + 3 * PX, oy + 1 * PX, PX, 0.6 * PX);
                    R(c, Ink, ox + 4 * PX, oy + 1.5 * PX, PX, 0.6 * PX);
                    R(c, Ink, ox + 9 * PX, oy + 1 * PX, PX, 0.6 * PX);
                    R(c, Ink, ox + 8 * PX, oy + 1.5 * PX, PX, 0.6 * PX);
                    break;
                case Eyes.Sleepy:
                    R(c, OrangeDark, ox + 3.6 * PX, oy + 1 * PX, 1.8 * PX, PX);
                    R(c, OrangeDark, ox + 7.6 * PX, oy + 1 * PX, 1.8 * PX, PX);
                    R(c, Ink, ox + 4 * PX, oy + 2 * PX, PX, PX);
                    R(c, Ink, ox + 8 * PX, oy + 2 * PX, PX, PX);
                    break;
                case Eyes.Wink:
                    R(c, Ink, ox + 4 * PX, oy + 1 * PX, PX, 2 * PX);
                    R(c, Ink, ox + 8 * PX, oy + 2 * PX, 2 * PX, PX * 0.7);
                    break;
                case Eyes.Happy:    // "^ ^"
                    foreach (int e in new[] { 3, 7 })
                    {
                        R(c, Ink, ox + e * PX, oy + 2 * PX, PX, PX);
                        R(c, Ink, ox + (e + 1) * PX, oy + 1 * PX, PX, PX);
                        R(c, Ink, ox + (e + 2) * PX, oy + 2 * PX, PX, PX);
                    }
                    break;
            }
        }
    }

    // ---------------- tiny humans ----------------
    public static void Human(DrawingContext c, double x, double feetY, Color shirt, int dir,
                             bool moving, double t, bool working, double off)
    {
        const double p = 3;
        var skin = Br(240, 200, 160); var sh = new SolidColorBrush(shirt);
        var pants = Br(52, 58, 90); var hat = Br(255, 200, 40);

        double s = Math.Sin((t + off) * 14);
        double bob = moving ? Math.Abs(s) * 2 : 0;
        double y0 = feetY - 7 * p - bob;
        double l = moving ? s * 2 : 0;

        R(c, pants, x - p + l, y0 + 5 * p, p, 2 * p);
        R(c, pants, x - l, y0 + 5 * p, p, 2 * p);
        R(c, sh, x - p, y0 + 2 * p, 2 * p, 3 * p);
        R(c, skin, x - p, y0, 2 * p, 2 * p);
        R(c, hat, x - p - 1, y0 - 2, 2 * p + 2, 3);

        double arm = working ? Math.Sin((t + off) * 10) * 5 : moving ? s * 3 : 0;
        double ax = dir > 0 ? x + p : x - 2 * p;
        R(c, sh, ax, y0 + 2 * p + arm * 0.5, p, 2 * p);
        if (working)
            R(c, Br(150, 150, 150), dir > 0 ? ax + p : ax - 4, y0 + 2 * p + arm * 0.5 - 3, 4, 4);   // hammer
    }

    // ---------------- data center (x = left edge) ----------------
    public static void DataCenter(DrawingContext c, double x, double feetY, double progress, double anim)
    {
        const int total = 6;
        const double w = 20, h = 10, gap = 2;
        double built = progress * total;
        for (int i = 0; i < total; i++)
        {
            double f = Math.Clamp(built - i, 0, 1);
            if (f <= 0) continue;
            int col = i % 2, row = i / 2;
            double rx = x + col * (w + gap);
            double ry = feetY - 4 - (row + 1) * (h + gap);
            R(c, Rack, rx, ry + h * (1 - f), w, h * f);
            if (f >= 1)
            {
                R(c, ((int)(anim * 3) + i) % 2 == 0 ? Green : Amber, rx + 3, ry + 3, 3, 3);
                R(c, Green, rx + 9, ry + 4, 8, 2);
            }
        }
    }

    // ---------------- bed & friends ----------------
    public static void Bed(DrawingContext c, double x, double feetY)
    {
        R(c, WoodDark, x - 42, feetY - 4, 4, 4);
        R(c, WoodDark, x + 38, feetY - 4, 4, 4);
        R(c, Wood, x - 44, feetY - 32, 4, 28);                 // headboard
        R(c, Mattress, x - 42, feetY - BedTop, 84, 10);
        R(c, Br(250, 250, 255), x - 41, feetY - BedTop - 6, 13, 6);   // pillow
    }

    public static void Blanket(DrawingContext c, double x, double feetY, double prog)
    {
        if (prog <= 0) return;
        double top = feetY - BedTop - 16 * prog;
        double bottom = feetY - 6;
        R(c, Blue, x - 38, top, 80, bottom - top);
        R(c, BlueLight, x - 38, top, 80, 3);
    }

    public static void Nightstand(DrawingContext c, double x, double feetY, bool withCup)
    {
        R(c, Wood, x - 10, feetY - 18, 20, 18);
        R(c, WoodDark, x - 10, feetY - 10, 20, 2);
        R(c, Gold, x - 1, feetY - 14, 2, 2);
        if (withCup) Cup(c, x - 4, feetY - 26, 0, false);
    }

    public static void Cup(DrawingContext c, double x, double y, double anim, bool steam)
    {
        R(c, Brushes.White, x, y, 8, 8);
        R(c, Br(100, 60, 30), x, y, 8, 2);
        R(c, Brushes.White, x + 8, y + 2, 2, 4);
        if (!steam) return;
        for (int i = 0; i < 2; i++)
            R(c, new SolidColorBrush(Colors.White, 0.6),
              x + 2 + i * 3 + Math.Sin(anim * 4 + i) * 1.5, y - 5 - i * 3, 1.5, 3);
    }

    // ---------------- effects ----------------
    public static void Zzz(DrawingContext c, double x, double y, double anim)
    {
        for (int i = 0; i < 3; i++)
        {
            double t = (anim * 0.5 + i * 0.33) % 1;
            var ft = Txt("z", 8 + i * 3, new SolidColorBrush(Colors.White, 1 - t));
            c.DrawText(ft, new Point(x + i * 6 + t * 10, y - t * 28));
        }
    }

    public static void Bubble(DrawingContext c, double x, double y, string text)
    {
        var ft = Txt(text, 12, Brushes.Black);
        double w = ft.Width + 14, h = ft.Height + 8;
        c.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1), new Rect(x - w / 2, y - h, w, h), 6, 6);
        R(c, Brushes.White, x - 3, y - 1, 6, 4);
        c.DrawText(ft, new Point(x - w / 2 + 7, y - h + 4));
    }

    public static void Heart(DrawingContext c, double x, double y, double s)
    {
        string[] h = { ".X.X.", "XXXXX", "XXXXX", ".XXX.", "..X.." };
        for (int r = 0; r < 5; r++)
            for (int k = 0; k < 5; k++)
                if (h[r][k] == 'X') R(c, Pink, x - 2.5 * s + k * s, y + r * s, s, s);
    }

    public static void IconAbove(DrawingContext c, double x, double y, Icon icon, double anim)
    {
        double bob = Math.Sin(anim * 6) * 2;
        switch (icon)
        {
            case Icon.Heart: Heart(c, x, y - 16 + bob, 3); break;
            case Icon.Exclaim: c.DrawText(Txt("!", 20, Br(255, 214, 80)), new Point(x - 4, y - 28 + bob)); break;
            case Icon.Question: c.DrawText(Txt("?", 20, Brushes.White), new Point(x - 6, y - 28 + bob)); break;
            case Icon.Anger: c.DrawText(Txt("#!", 18, Red), new Point(x - 9, y - 26 + bob)); break;
            case Icon.Note: c.DrawText(Txt("\u266A", 18, Brushes.White), new Point(x - 4 + Math.Sin(anim * 3) * 3, y - 28 + bob)); break;
        }
    }

    public static void Poppy(DrawingContext c, double x, double y)
    {
        var g = Br(60, 160, 60);
        R(c, g, x - 0.75, y + 2, 1.5, 9);
        R(c, g, x + 0.75, y + 7, 3, 1.5);
        R(c, Red, x - 3, y - 3, 6, 6);
        R(c, Br(255, 120, 120), x - 3, y - 3, 2, 2);
        R(c, Ink, x - 1, y - 1, 2, 2);
    }

    // a tiny "villager": brown robe, unibrow, big nose
    public static void Villager(DrawingContext c, double x, double feetY, int dir, bool moving, double t)
    {
        const double p = 3;
        var skin = Br(222, 170, 130); var robe = Br(120, 84, 56); var dark = Br(90, 62, 40);
        double s = Math.Sin(t * 14), bob = moving ? Math.Abs(s) * 2 : 0;
        double y0 = feetY - 7 * p - bob, l = moving ? s * 2 : 0;
        R(c, dark, x - p + l, y0 + 6 * p, p, p);
        R(c, dark, x - l, y0 + 6 * p, p, p);
        R(c, robe, x - p - 1, y0 + 2 * p, 2 * p + 2, 4 * p);
        R(c, dark, x - p - 1, y0 + 3.5 * p, 2 * p + 2, 3);
        R(c, skin, x - p, y0, 2 * p, 2 * p);
        R(c, Br(60, 40, 25), x - p, y0 + 1, 2 * p, 2);
        R(c, Br(200, 150, 110), dir > 0 ? x + p : x - p - 2, y0 + p, 2, 3);
    }

    public static void NameTag(DrawingContext c, double x, double y, string name, double alpha)
    {
        var ft = Txt(name, 11, Brushes.White);
        double w = ft.Width + 14, h = ft.Height + 6;
        using (c.PushOpacity(alpha))
        {
            c.DrawRectangle(new SolidColorBrush(Color.FromArgb(225, 20, 22, 30)), new Pen(Orange, 1),
                            new Rect(x - w / 2, y - h, w, h), 4, 4);
            c.DrawText(ft, new Point(x - w / 2 + 7, y - h + 3));
        }
    }

    // ---- mascots living in the corner boxes (PLACEHOLDERS until the real list is approved) ----
    static void Eyes2(DrawingContext c, double x, double y, double gap, double look, double lookY, bool sad)
    {
        foreach (int s in new[] { -1, 1 })
        {
            double ex = x + s * gap, h = sad ? 3 : 5, dy = sad ? 1 : 0;
            R(c, Brushes.White, ex - 2.5, y - 2.5 + dy, 5, h);
            R(c, Ink, ex - 1 + look, y - 1 + lookY + dy, 2, 2);
        }
    }

    // kind 0 = green bot, 1 = rainbow fluff, 2 = Tux. baseY = bottom of the mascot's body.
    public static void Mascot(DrawingContext c, int kind, double x, double baseY, double look, double lookY, bool sad, double t)
    {
        double tearX, tearY;
        switch (kind)
        {
            case 0:
            {
                var g = Br(61, 220, 132);
                R(c, g, x - 12, baseY - 20, 24, 20);
                R(c, g, x - 10, baseY - 22, 20, 2);
                R(c, g, x - 9, baseY - 29, 2, 7);
                R(c, g, x + 7, baseY - 29, 2, 7);
                Eyes2(c, x, baseY - 14, 5, look, lookY, sad);
                tearX = x - 8; tearY = baseY - 10;
                break;
            }
            case 1:
            {
                IBrush[] bands = { Br(66, 133, 244), Br(155, 114, 255), Br(255, 120, 170), Br(255, 170, 60), Br(255, 215, 80) };
                for (int i = 0; i < 5; i++) R(c, bands[i], x - 14, baseY - 28 + i * 5.6, 28, 5.6);
                R(c, bands[0], x - 14, baseY - 31, 5, 4);
                R(c, bands[0], x + 9, baseY - 31, 5, 4);
                Eyes2(c, x, baseY - 20, 6, look, lookY, sad);
                tearX = x - 9; tearY = baseY - 15;
                break;
            }
            default:
                Tux(c, x, baseY, 0, false, t, look, lookY);
                tearX = x - 6; tearY = baseY - 26;
                break;
        }
        if (sad) R(c, Br(120, 190, 255), tearX, tearY + (t * 20) % 8, 2, 3);
    }

    public static void Calendar(DrawingContext c, double x, double y, string top, string big)
    {
        R(c, Brushes.White, x - 12, y - 26, 24, 26);
        R(c, Br(220, 60, 60), x - 12, y - 26, 24, 8);
        var tt = Txt(top, 6, Brushes.White);
        c.DrawText(tt, new Point(x - tt.Width / 2, y - 26));
        var bt = Txt(big, 14, Brushes.Black);
        c.DrawText(bt, new Point(x - bt.Width / 2, y - 18));
    }

    // ---- corner boxes + Tux ----
    public static void MiniBox(DrawingContext c, double x, double fy, string label, double open, bool front)
    {
        const double w = 44, h = 26;
        if (!front)
        {
            R(c, Br(150, 108, 62), x - w / 2, fy - h, w, h);
            R(c, Br(70, 48, 28), x - w / 2 + 2, fy - h - 2, w - 4, 4);
            return;
        }
        double a = Math.PI * 0.6 * open;
        foreach (int s in new[] { -1, 1 })
        {
            double hx = x + s * w / 2, hy = fy - h;
            var m = Matrix.CreateTranslation(-hx, -hy) * Matrix.CreateRotation(s * a) * Matrix.CreateTranslation(hx, hy);
            using (c.PushTransform(m))
                R(c, Br(205, 160, 100), s < 0 ? hx : hx - w / 2, hy - 3, w / 2, 3);
        }
        R(c, Br(196, 150, 90), x - w / 2, fy - h, w, h);
        R(c, Br(170, 125, 70), x - w / 2, fy - 3, w, 3);
        R(c, Br(225, 205, 160), x - 3, fy - h, 6, 9);
        var ft = Txt(label, 7, Brushes.Black);
        R(c, Brushes.White, x - w / 2 + 3, fy - 15, w - 6, 9);
        c.DrawText(ft, new Point(x - ft.Width / 2, fy - 15));
    }

    static readonly string[] TuxMap =
    {
        "...KKKK...",
        "..KKKKKK..",
        "..KWKKWK..",
        "..KKOOKK..",
        "..KKKKKK..",
        ".KKWWWWKK.",
        ".KKWWWWKK.",
        ".KKWWWWKK.",
        ".KKWWWWKK.",
        "..KWWWWK..",
        "..KKKKKK..",
    };

    public static void Tux(DrawingContext c, double x, double feetY, double flap, bool walking, double t,
                           double look = 0, double lookY = 0)
    {
        const double p = 3;
        var K = Br(30, 30, 40); var W = Br(235, 235, 240); var O = Br(240, 170, 40);
        double ox = x - 5 * p, oy = feetY - 12 * p;
        for (int r = 0; r < TuxMap.Length; r++)
            for (int k = 0; k < 10; k++)
            {
                char ch = TuxMap[r][k];
                if (ch == '.') continue;
                R(c, ch == 'K' ? K : ch == 'W' ? W : O, ox + k * p, oy + r * p, p, p);
            }
        R(c, K, ox + 3 * p + 1 + look, oy + 2 * p + 1 + lookY, p - 2, p - 2);   // pupils
        R(c, K, ox + 6 * p + 1 + look, oy + 2 * p + 1 + lookY, p - 2, p - 2);
        double fl = flap * 3, s = walking ? Math.Sin(t * 14) * 2 : 0;
        R(c, K, ox - p, oy + 5 * p - fl, p, 3 * p);              // flippers
        R(c, K, ox + 10 * p, oy + 5 * p + fl, p, 3 * p);
        R(c, O, ox + p + s, oy + 11 * p, 3 * p, p);              // feet
        R(c, O, ox + 6 * p - s, oy + 11 * p, 3 * p, p);
    }

    public static void Cake(DrawingContext c, double x, double fy, double t)
    {
        R(c, Br(240, 200, 150), x - 11, fy - 10, 22, 10);
        R(c, Br(255, 150, 190), x - 11, fy - 14, 22, 4);
        R(c, Br(255, 240, 200), x - 1, fy - 22, 2, 8);
        double f = 3 + Math.Sin(t * 20);
        R(c, Br(255, 190, 60), x - 1.5, fy - 22 - f, 3, f);
    }

    // ---- cardboard box (intro) ----
    public static void BoxBack(DrawingContext c, double x, double fy)
    {
        R(c, Br(150, 108, 62), x - 28, fy - 30, 56, 30);
        R(c, Br(70, 48, 28), x - 26, fy - 32, 52, 4);
    }

    public static void BoxFront(DrawingContext c, double x, double fy, double open)
    {
        double a = Math.PI * 0.6 * open;
        foreach (int s in new[] { -1, 1 })   // flaps hinged on the left/right top edges
        {
            double hx = x + s * 28, hy = fy - 30;
            var m = Matrix.CreateTranslation(-hx, -hy) * Matrix.CreateRotation(s * a) * Matrix.CreateTranslation(hx, hy);
            using (c.PushTransform(m))
                R(c, Br(205, 160, 100), s < 0 ? hx : hx - 28, hy - 3, 28, 3);
        }
        R(c, Br(196, 150, 90), x - 28, fy - 30, 56, 30);
        R(c, Br(170, 125, 70), x - 28, fy - 4, 56, 4);
        R(c, Br(225, 205, 160), x - 4, fy - 30, 8, 12);          // tape
        R(c, Brushes.White, x - 18, fy - 16, 30, 10);            // label
        c.DrawText(Txt("Clawd", 8, Brushes.Black), new Point(x - 16, fy - 16));
    }

    // ---- Zx1 expedition props ----
    public static void Portal(DrawingContext c, double x, double y, double open, double t, bool busy)
    {
        double rx = 14 * open, ry = 24 * open;
        var green = Br(118, 185, 0);
        bool flash = busy && (int)(t * 20) % 2 == 0;
        c.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 18, 8, 40)),
                      new Pen(flash ? Brushes.White : green, 3), new Point(x, y), rx, ry);
        for (int i = 0; i < 6; i++)
        {
            double a = t * 3 + i * 1.05;
            R(c, green, x + Math.Cos(a) * rx * 0.6 - 1.5, y + Math.Sin(a) * ry * 0.6 - 1.5, 3, 3);
        }
        if (open > 0.6)
        {
            var ft = Txt("x", 20, Brushes.White);
            c.DrawText(ft, new Point(x - ft.Width / 2, y - ft.Height / 2));
        }
    }

    public static void Flag(DrawingContext c, double x, double fy, double grow, double banner)
    {
        if (grow <= 0) return;
        R(c, Br(90, 70, 50), x - 5, fy - 4, 10, 4);
        double h = 34 * grow;
        R(c, Br(200, 200, 200), x - 1, fy - 4 - h, 2, h);
        if (banner <= 0) return;
        double w = 34 * banner;
        R(c, Br(25, 30, 45), x + 1, fy - 4 - h, w, 14);
        R(c, Br(118, 185, 0), x + 1, fy - 4 - h + 12, w, 2);
        if (banner >= 1) c.DrawText(Txt("Zx1", 10, Brushes.White), new Point(x + 5, fy - 4 - h + 0.5));
    }

    public static void ZArrow(DrawingContext c, double x, double y)
    {
        var cy = Br(120, 220, 255);
        R(c, cy, x - 1, y - 30, 2, 24);
        R(c, cy, x - 1.5, y - 33, 3, 3);
        R(c, cy, x - 3.5, y - 30, 7, 2);
        c.DrawText(Txt("Z", 14, cy), new Point(x - 5, y - 52));
    }

    public static void Hat(DrawingContext c, double x, double top)
    {
        R(c, Br(140, 95, 50), x - 9, top - 12, 18, 9);
        R(c, Br(60, 40, 25), x - 9, top - 6, 18, 3);
        R(c, Br(120, 80, 40), x - 16, top - 3, 32, 4);
    }

    public static void Chip(DrawingContext c, double x, double y, double t)
    {
        var green = Br(118, 185, 0);
        R(c, new SolidColorBrush(Color.FromRgb(118, 185, 0), 0.25 + 0.1 * Math.Sin(t * 6)), x - 10, y - 10, 20, 20);
        for (int i = 0; i < 3; i++)
        {
            R(c, Gold, x - 8, y - 5 + i * 4, 2, 2);
            R(c, Gold, x + 6, y - 5 + i * 4, 2, 2);
        }
        R(c, green, x - 6, y - 6, 12, 12);
        R(c, Br(50, 90, 0), x - 3, y - 3, 6, 6);
    }

    // ---- Shlepa: a big caracal lying on the taskbar edge (head on the right), front paws hanging over it ----
    static readonly IBrush Tan = Br(205, 155, 98), TanDark = Br(170, 122, 74), Cream = Br(240, 226, 198),
                           Blk = Br(30, 26, 24), EarPink = Br(228, 172, 152), Iris = Br(232, 172, 60);

    static readonly string[] ShlepaMap =
    {
        "..................B..........B",
        "..................B..........B",
        "..................B..........B",
        "..................BT........TB",
        "..................TPT......TPT",
        "..................TPPT....TPPT",
        "..................TTTTTDDTTTTT",
        "..................TTBBTTTTBBTT",
        "..................TBTTBTTBTTBT",
        ".......TTTTTTTTTTTTTTTBTTBTTTT",
        "....TTTTTTTTTTTTTTTTCCCBBCCCTT",
        "..TDTTTTTTTTTTTTTTTCCBCCCCBCC.",
        ".TTDDTTTTTTTTTTTTCCCCCCCCCCCC.",
        ".TTDDTTTTTTTTTTTTCCCTCCCCCCT..",
        ".DDDDDDDDDDDDDDDDCCCD.........",
    };

    // x = center of the sprite, groundY = top edge of the taskbar: the body rests on it, the paws hang below it
    public static void Shlepa(DrawingContext c, double x, double groundY, Eyes eyes, double anim,
                              bool held, bool purring, bool twitch)
    {
        const double p = 3;
        double ox = x - 15 * p + (purring ? Math.Sin(anim * 45) * 0.6 : 0), oy = groundY - 15 * p;
        double breath = 1 + 0.02 * Math.Sin(anim * 2);
        var m = Matrix.CreateTranslation(-x, -groundY) * Matrix.CreateScale(1, breath) * Matrix.CreateTranslation(x, groundY);
        using (c.PushTransform(m))
        {
            for (int i = 0; i < 6; i++)   // swishing tail with a black tip, behind the body
            {
                double tx = ox + p - i * 3.2, ty = groundY - 8 - i * 2.2 + Math.Sin(anim * 2.5 + i * 0.7) * 2.5;
                R(c, i == 5 ? Blk : Tan, tx, ty, p + 1, p + 1);
            }
            for (int r = 0; r < ShlepaMap.Length; r++)
                for (int k = 0; k < 30; k++)
                {
                    char ch = ShlepaMap[r][k];
                    if (ch == '.') continue;
                    double ey = twitch && r < 4 && k >= 25 ? -1 : 0;   // right ear twitches
                    IBrush col = ch == 'T' ? Tan : ch == 'D' ? TanDark : ch == 'B' ? Blk : ch == 'P' ? EarPink : Cream;
                    R(c, col, ox + k * p, oy + r * p + ey, p, p);
                }

            foreach (int e in new[] { 20, 26 })
            {
                double ex = ox + e * p, ey = oy + 8 * p;
                bool left = e == 20;
                Eyes kind = eyes == Eyes.Wink ? (left ? Eyes.Open : Eyes.Closed) : eyes;
                switch (kind)
                {
                    case Eyes.Closed:
                    case Eyes.Sleepy:
                        R(c, Blk, ex, ey + 1.2 * p, 2 * p, 2);
                        break;
                    case Eyes.Happy:
                        R(c, Blk, ex, ey + 1.4 * p, p * 0.8, p * 0.8);
                        R(c, Blk, ex + 0.7 * p, ey + 0.8 * p, p * 0.8, p * 0.8);
                        R(c, Blk, ex + 1.4 * p, ey + 1.4 * p, p * 0.8, p * 0.8);
                        break;
                    case Eyes.Love:
                        R(c, Pink, ex, ey + 0.2 * p, p * 0.8, p * 0.8);
                        R(c, Pink, ex + p, ey + 0.2 * p, p * 0.8, p * 0.8);
                        R(c, Pink, ex, ey + p, 2 * p, p * 0.8);
                        R(c, Pink, ex + 0.5 * p, ey + 1.8 * p, p, p * 0.7);
                        break;
                    case Eyes.Wide:
                        R(c, Iris, ex - 1, ey - 1, 2 * p + 2, 2 * p + 2);
                        R(c, Blk, ex + 0.5 * p, ey - 1, p + 1, 2 * p + 2);
                        break;
                    case Eyes.Angry:
                        R(c, Iris, ex, ey + 0.6 * p, 2 * p, 1.4 * p);
                        R(c, Blk, ex + 0.8 * p, ey + 0.6 * p, 0.4 * p + 1, 1.4 * p);
                        R(c, Blk, ex - 1, ey + 0.2 * p, 2 * p + 2, 2);
                        break;
                    case Eyes.Sad:
                        R(c, Iris, ex, ey + 0.4 * p, 2 * p, 1.6 * p);
                        R(c, Blk, ex + 0.8 * p, ey + 0.4 * p, 0.4 * p + 1, 1.6 * p);
                        break;
                    default:   // open: amber iris with a slit pupil
                        R(c, Iris, ex, ey, 2 * p, 2 * p);
                        R(c, Blk, ex + 0.8 * p, ey, 0.4 * p + 1, 2 * p);
                        R(c, Brushes.White, ex + 0.2 * p, ey + 0.2 * p, 1.5, 1.5);
                        break;
                }
            }
            var wh = new SolidColorBrush(Colors.White, 0.55);       // whiskers
            foreach (double wx in new[] { ox + 13 * p, ox + 28 * p })
            {
                R(c, wh, wx, oy + 11 * p, 6 * p, 1);
                R(c, wh, wx, oy + 12 * p, 6 * p, 1);
            }
        }

        // front paws hanging over the edge (they flail when he is picked up)
        double dang = held ? 14 : 9, sw = held ? Math.Sin(anim * 12) * 3 : 0;
        foreach (int px0 in new[] { 19, 24 })
        {
            double wx = ox + px0 * p + (px0 == 19 ? sw : -sw);
            R(c, Tan, wx, groundY - 1, 3 * p, dang - 3);
            R(c, Cream, wx, groundY - 1 + dang - 3, 3 * p, 3);
            R(c, TanDark, wx + p, groundY - 1 + dang - 3, 1, 3);
            R(c, TanDark, wx + 2 * p, groundY - 1 + dang - 3, 1, 3);
        }
    }

    // ---- the door Clawd slams when you ignore him ----
    public static void DoorBack(DrawingContext c, double x, double fy)
    {
        R(c, Br(80, 55, 35), x - 14, fy - 46, 28, 46);     // frame
        R(c, Br(22, 18, 28), x - 11, fy - 43, 22, 43);     // dark doorway
    }

    public static void DoorFront(DrawingContext c, double x, double fy, double open, double shake)
    {
        double w = 22 * (1 - open);   // the leaf narrows as the door swings open
        if (w < 1) return;
        using (c.PushTransform(Matrix.CreateTranslation(shake, 0)))
        {
            R(c, Br(150, 100, 60), x - 11, fy - 43, w, 43);
            if (w > 10) R(c, Br(128, 84, 50), x - 8, fy - 39, w - 6, 14);
            R(c, Gold, x - 11 + w - 5, fy - 22, 3, 3);     // knob
        }
    }

    public static void DoorSign(DrawingContext c, double x, double fy, double dy)
    {
        R(c, Br(230, 230, 230), x + 6.5, fy - 22 + dy, 1, 3);
        R(c, Br(220, 60, 60), x - 4, fy - 19 + dy, 20, 9);
        c.DrawText(Txt("ТИХО", 6, Brushes.White), new Point(x - 2, fy - 19.5 + dy));
    }

    public static void Headphones(DrawingContext c, double x, double top)
    {
        var dark = Br(45, 48, 60);
        R(c, dark, x - 18, top - 5, 36, 3);                // band
        R(c, dark, x - 20, top - 3, 3, 8);                 // uprights
        R(c, dark, x + 17, top - 3, 3, 8);
        R(c, Br(90, 190, 255), x - 23, top + 4, 7, 11);    // ear cups
        R(c, Br(90, 190, 255), x + 16, top + 4, 7, 11);
        R(c, dark, x - 22, top + 6, 5, 7);
        R(c, dark, x + 17, top + 6, 5, 7);
    }

    public static void Token(DrawingContext c, double x, double y)
    {
        R(c, Br(255, 209, 102), x - 3, y - 3, 6, 6);
        R(c, Amber, x - 1, y - 1, 2, 2);
    }
}
