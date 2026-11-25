using Avalonia;
using Avalonia.Media;

namespace Clawd;

/// <summary>Pixel matrices and drawing helpers for the Game-mode room. Everything is built from rectangles.</summary>
static class Px
{
    public static IBrush B(byte r, byte g, byte b, byte a = 255) => new SolidColorBrush(Color.FromArgb(a, r, g, b));

    public static readonly IBrush Ink = B(24, 24, 32), White = B(250, 250, 252), Orange = B(217, 119, 87),
        PopY = B(255, 214, 90), PopW = B(255, 247, 224), Gold = B(240, 190, 60);

    static readonly Dictionary<char, IBrush> BucketPal = new()
        { ['D'] = B(150, 24, 32), ['R'] = B(222, 44, 52), ['W'] = B(250, 245, 235) };
    static readonly Dictionary<char, IBrush> CanPal = new()
        { ['S'] = B(190, 196, 208), ['B'] = B(56, 104, 232), ['L'] = B(150, 188, 255), ['W'] = B(250, 250, 252) };
    static readonly Dictionary<char, IBrush> HatPal = new()
        { ['R'] = B(212, 48, 56), ['r'] = B(160, 30, 40), ['W'] = B(250, 250, 252) };
    static readonly Dictionary<char, IBrush> BugPal = new()
        { ['G'] = B(61, 220, 132), ['l'] = B(30, 120, 70), ['W'] = B(250, 250, 252), ['K'] = B(24, 24, 32) };

    static readonly string[] BucketMap =
    {
        "DDDDDDDDDDD",
        "RWRWRWRWRWR",
        "RWRWRWRWRWR",
        "RWRWRWRWRWR",
        ".RWRWRWRWR.",
        ".RWRWRWRWR.",
        ".RWRWRWRWR.",
        "..RWRWRWR..",
        "..DDDDDDD..",
    };

    static readonly string[] CanMap =
    {
        "SSSSSS",
        "BBBBBB",
        "BLWWLB",
        "BLBBLB",
        "BWWWWB",
        "BLBBLB",
        "BBBBBB",
        "BBBBBB",
        "SSSSSS",
    };

    static readonly string[] HatMap =
    {
        "........WW..",
        "......RRRWW.",
        ".....RRRRRW.",
        "....RRRRRr..",
        "..RRRRRRRr..",
        ".RRRRRRRRrr.",
        "WWWWWWWWWWWW",
    };

    static readonly string[] BugMap =
    {
        ".l.....l.",
        "..lGGGl..",
        "lGGGGGGGl",
        ".GWKGWKG.",
        "lGGGGGGGl",
        "..l...l..",
    };

    // where the popcorn pieces of the heap sit (x in bucket units, y = height above the rim)
    static readonly (double X, double Y)[] HeapPos =
    {
        (1, 0), (3, 0), (5, 0), (7, 0), (9, 0), (2, 1.5), (4, 1.5), (6, 1.5), (8, 1.5),
        (3, 3), (5, 3), (7, 3), (4.5, 4.5), (6, 4.5),
    };

    public static void Map(DrawingContext c, string[] rows, Dictionary<char, IBrush> pal, double x, double y, double s)
    {
        for (int r = 0; r < rows.Length; r++)
            for (int k = 0; k < rows[r].Length; k++)
                if (pal.TryGetValue(rows[r][k], out var b))
                    Sprites.R(c, b, x + k * s, y + r * s, s + 0.2, s + 0.2);   // +0.2: no hairline gaps
    }

    static readonly Dictionary<char, IBrush> ModPal = new()
        { ['G'] = B(40, 160, 90), ['K'] = B(24, 24, 32), ['C'] = B(80, 220, 255), ['Y'] = B(240, 190, 60) };
    static readonly Dictionary<char, IBrush> ModBadPal = new()
        { ['G'] = B(52, 48, 50), ['K'] = B(14, 14, 16), ['C'] = B(220, 50, 50), ['Y'] = B(120, 100, 60) };
    static readonly string[] ModMap = { "GGGGGGGGGG", "GKKGGCCGGG", "GKKGGCCGGG", "GGGGGGGGGG", "GGGGGGGGGG", "YYYYYYYYYY" };

    /// <summary>Server module (a little circuit board). broken = burnt black with red marks.</summary>
    public static void Module(DrawingContext c, double x, double bottom, bool broken) =>
        Map(c, ModMap, broken ? ModBadPal : ModPal, x - 9, bottom - ModMap.Length * 1.8, 1.8);

    public static void Kernel(DrawingContext c, double x, double y, int seed)
    {
        Sprites.R(c, seed % 2 == 0 ? PopW : PopY, x, y, 3.2, 3.2);
        Sprites.R(c, seed % 2 == 0 ? PopY : PopW, x + 1.4, y + 1.4, 2, 2);
    }

    /// <summary>Popcorn bucket, x = centre, bottom = lower edge, level 0..1 = how full it is.</summary>
    public static void Bucket(DrawingContext c, double x, double bottom, double level)
    {
        const double s = 1.6;
        double w = 11 * s, h = BucketMap.Length * s, left = x - w / 2, top = bottom - h;
        int n = (int)Math.Round(Math.Clamp(level, 0, 1) * HeapPos.Length);
        for (int i = 0; i < n; i++) Kernel(c, left + HeapPos[i].X * s, top - HeapPos[i].Y * s - 1, i);
        Map(c, BucketMap, BucketPal, left, top, s);
    }

    public static void Can(DrawingContext c, double x, double bottom, double s) =>
        Map(c, CanMap, CanPal, x - 3 * s, bottom - CanMap.Length * s, s);

    /// <summary>Santa hat with a pompom. x = centre, bottom = lower edge of the white brim.</summary>
    public static void Hat(DrawingContext c, double x, double bottom, double s) =>
        Map(c, HatMap, HatPal, x - 6 * s, bottom - HatMap.Length * s, s);

    /// <summary>Zex Bug Jr. (a little green bug). x = centre, y = top.</summary>
    public static void Bug(DrawingContext c, double x, double y, double s) =>
        Map(c, BugMap, BugPal, x - 4.5 * s, y, s);

    public static void Line(DrawingContext c, IBrush b, double x0, double y0, double x1, double y1, double s)
    {
        double dx = x1 - x0, dy = y1 - y0;
        int n = (int)Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dy)) / s);
        for (int i = 0; i <= n; i++)
            Sprites.R(c, b, x0 + dx * i / n - s / 2, y0 + dy * i / n - s / 2, s, s);
    }

    public static void Puff(DrawingContext c, double x, double y, double k)
    {
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4, r = 6 + 22 * k, s = 4 * (1 - k) + 1;
            Sprites.R(c, i % 2 == 0 ? PopY : White, x + Math.Cos(a) * r - s / 2, y + Math.Sin(a) * r * 0.8 - s / 2, s, s);
        }
    }

    /// <summary>Pixel "cloud" with a trail of squares. p = the speaker's head, maxX = width of the window.</summary>
    public static void Cloud(DrawingContext c, Point p, string text, double maxX)
    {
        var fts = text.Split('\n').Select(l => Sprites.Txt(l, 9, Brushes.Black)).ToArray();
        double tw = fts.Max(f => f.Width), th = fts.Sum(f => f.Height);
        double w = Math.Ceiling(tw) + 14, h = Math.Ceiling(th) + 8;
        double x0 = Math.Clamp(p.X - w / 2, 3, Math.Max(3, maxX - 3 - w)), y0 = Math.Max(3, p.Y - h - 12);
        Sprites.R(c, Ink, x0, y0 + 3, w, h - 6);            // stepped corners = pixel cloud
        Sprites.R(c, Ink, x0 + 3, y0, w - 6, h);
        Sprites.R(c, White, x0 + 1, y0 + 4, w - 2, h - 8);
        Sprites.R(c, White, x0 + 4, y0 + 1, w - 8, h - 2);
        Sprites.R(c, Ink, x0 + 8, y0 - 2, 8, 2);             // little bumps on top
        Sprites.R(c, White, x0 + 9, y0 - 1, 6, 2);
        Sprites.R(c, Ink, x0 + w - 18, y0 - 2, 6, 2);
        Sprites.R(c, White, x0 + w - 17, y0 - 1, 4, 2);
        double tx = Math.Clamp(p.X, x0 + 8, x0 + w - 8);
        Sprites.R(c, Ink, tx - 3, y0 + h - 1, 6, 6);
        Sprites.R(c, White, tx - 2, y0 + h, 4, 4);
        Sprites.R(c, Ink, tx - 2, y0 + h + 6, 4, 4);
        Sprites.R(c, White, tx - 1, y0 + h + 7, 2, 2);
        double ty = y0 + 4;
        foreach (var f in fts) { c.DrawText(f, new Point(x0 + 7, ty)); ty += f.Height; }
    }

    public static void Label(DrawingContext c, Point p, string text, double size, IBrush b)
    {
        var f = Sprites.Txt(text, size, b);
        c.DrawText(f, new Point(p.X - f.Width / 2, p.Y));
    }
}

/// <summary>The little door that leads into the system tray (used by the enter/exit scenes).</summary>
static class TrayDoor
{
    public static void Draw(DrawingContext c, double x, double fy, double grow, double open, bool front)
    {
        if (grow <= 0.01) return;
        var m = Matrix.CreateTranslation(-x, -fy) * Matrix.CreateScale(1, grow) * Matrix.CreateTranslation(x, fy);
        using (c.PushTransform(m))
        {
            if (front) { Sprites.DoorFront(c, x, fy, open, 0); return; }
            Sprites.DoorBack(c, x, fy);
            if (open > 0.3)   // tiny tray icons glow in the doorway
            {
                byte a = (byte)(255 * Math.Clamp((open - 0.3) / 0.5, 0, 1));
                Sprites.R(c, Px.B(90, 190, 255, a), x - 8, fy - 30, 5, 5);
                Sprites.R(c, Px.B(80, 220, 120, a), x - 1, fy - 30, 5, 5);
                Sprites.R(c, Px.B(255, 170, 50, a), x + 6, fy - 30, 4, 5);
                Sprites.R(c, Px.B(250, 250, 252, a), x - 8, fy - 20, 18, 3);
            }
            Sprites.R(c, Px.B(217, 119, 87), x - 15, fy - 58, 30, 10);   // sign
            Sprites.R(c, Px.Ink, x - 15, fy - 58, 30, 1);
            c.DrawText(Sprites.Txt("ТРЕЙ", 7, Brushes.White), new Point(x - 9, fy - 58));
        }
    }
}
