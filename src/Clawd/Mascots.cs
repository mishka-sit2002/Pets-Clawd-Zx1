using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd;

enum Fx { None, Claw, Spout, Bubbles, Sparks, Cluck, Hue, Wink, Wave, Tail, Roll, Snake, Fly, Yawn, Notes, Look }

/// <summary>A mascot: a 12-column pixel matrix + palette + how it behaves on the taskbar. Built only from rectangles.</summary>
sealed class Art
{
    public readonly string Name;
    public readonly string[] Rows;
    readonly Dictionary<char, IBrush> pal = new();
    public readonly Color MainColor;

    public double Amp = 100, Spd = 0.6, Hop, HopHz = 1.5, Bob, Tilt, Lift;   // walk range, speed, hops, floating, wobble
    public bool NoFlip;
    public int Wig = -1;       // first row that sways (tentacles, trunk)
    public int Arms;           // waving arms (0, 1 or 2)
    public int EyeX = -1, EyeY = -1;   // eye cell for winking
    public Fx Fx;

    public Art(string name, string palette, params string[] rows)
    {
        Name = name; Rows = rows;
        var p = new Dictionary<char, Color> { ['K'] = Color.Parse("#181818"), ['W'] = Color.Parse("#FFFFFF") };
        Color? main = null;
        foreach (var tok in palette.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            p[tok[0]] = Color.Parse("#" + tok.Substring(2));
            main ??= p[tok[0]];
        }
        MainColor = main ?? Color.Parse("#888888");
        foreach (var kv in p) pal[kv.Key] = new SolidColorBrush(kv.Value);
    }

    public IBrush? Brush(char ch) => pal.TryGetValue(ch, out var b) ? b : null;
    public IBrush Main => new SolidColorBrush(MainColor);
    public double Height => Rows.Length * MascotArt.S;
}

static class Arts
{
    // ---- "Zx1 Labs": mascots of the big AI companies (8 boxes) ----
    public static readonly Art[] Labs =
    {
        new("OpenAI", "G=10A37F",
            "...GGGGGG...", "..GGGGGGGG..", ".GGWWWWWWGG.", "GGGWKGGKWGGG", "GGGWGGGGWGGG", "GGGWGKKGWGGG", ".GGWWWWWWGG.", "..GGGGGGGG..", "...GG..GG...")
            { Amp = 90, Spd = 0.6, Hop = 7, HopHz = 1.2, Tilt = 0.1 },
        new("Llama", "C=F2E6D0 N=E8A0A0 D=C9B79A",
            "..CC....CC..", "..CC....CC..", "..CCCCCCCC..", ".CCCKCCKCCC.", ".CCCCCCCCCC.", "..CCCNNCCC..", "...CCCCCC...", "...CCDDCC...", "..CCCCCCCC..", ".CCCCCCCCCC.", "..CC....CC..")
            { Amp = 110, Spd = 0.5, Hop = 3, HopHz = 2.2 },
        new("Meta", "B=0A66FF C=9CC4FF",
            ".BBBB..BBBB.", "BBCCBBBBCCBB", "BCKKCBBCKKCB", "BCKKCBBCKKCB", "BBCCBBBBCCBB", ".BBBB..BBBB.", "....BBBB....", "...BB..BB...")
            { Amp = 80, Spd = 0.7, Hop = 9, HopHz = 1.4, Tilt = 0.15 },
        new("Grok", "K=181818 W=F2F2F2",
            "...KKKKKK...", "..KKKKKKKK..", ".KKWKKWKWKK.", ".KKKKKKWKKK.", ".KKKKKWKKKK.", ".KKKKWKKKKK.", ".KKKWKKKKKK.", "..KKKKKKKK..", "...KKKKKK...")
            { Amp = 100, Spd = 0.8, Hop = 5, HopHz = 1.8, Fx = Fx.Look },
        new("Gemini", "P=5B8CFF Q=B58CFF",
            ".....QQ.....", ".....QQ.....", "....PPPP....", "...PPPPPP...", "QPPPKPPKPPPQ", "QPPPPPPPPPPQ", "...PPPPPP...", "....PPPP....", ".....PP.....", ".....PP.....")
            { Amp = 70, Spd = 0.7, Hop = 6, HopHz = 1.3, Tilt = 0.5, Bob = 3, Lift = 4 },
        new("Nano Banana", "Y=FFD93B B=8A5A1E D=E0B020",
            ".........BB.", "........YYY.", ".......YYYY.", "......YYKYKY", ".....YYYYYYY", "...YYYYYYYY.", "..YYYYYYYY..", ".DYYYYYYY...", ".DDYYYY.....")
            { Amp = 90, Spd = 0.9, Hop = 8, HopHz = 1.6, Tilt = 0.25 },
        new("Le Chat", "O=FF8A00 Y=FFD000 N=7A3B00",
            "YY........YY", "YYY......YYY", "YYYYYYYYYYYY", "OOKKOOOOKKOO", "OOOOOOOOOOOO", "OOOOONNOOOOO", ".OOOOOOOOOO.", "..OOOOOOOO..", "..OO....OO..")
            { Amp = 120, Spd = 0.6, Hop = 4, HopHz = 2.4 },
        new("Copilot", "B=3D8BFF P=B25AFF O=FF8A3D",
            "...BBBPPP...", "..BBBBPPPP..", ".BBWKBPWKPP.", ".BBBBBPPPPP.", ".BBBBPPPPPO.", "..BBPPPPOO..", "...PPPOOO...", "....OOOO....")
            { Amp = 60, Spd = 0.9, Hop = 7, HopHz = 1.5, Bob = 2 },
    };

    // ---- December list, numbers 4..22 = the day of December they come out ----
    public static readonly Art[] Dec =
    {
        new("Ferris", "R=E4572E",   // 4: scuttles sideways, claws click
            "RR..W..W..RR", "RRR.K..K.RRR", ".RRRRRRRRRR.", "..RRRRRRRR..", ".RRRRRRRRRR.", "R.R.R..R.R.R")
            { Amp = 150, Spd = 0.45, Hop = 1.5, HopHz = 5, NoFlip = true, Fx = Fx.Claw },
        new("Gopher", "C=6AD7E5 N=7A5A3A",   // 5: stands like a post, pops up now and then
            "..CC....CC..", "..CCCCCCCC..", ".CCWWCCWWCC.", ".CCWKCCWKCC.", ".CCCCNNCCCC.", ".CCCCWWCCCC.", "..CCCCCCCC..", "..CCCCCCCC..", "..CCCCCCCC..", "...CC..CC...")
            { Amp = 12, Spd = 0.5, Hop = 14, HopHz = 0.35 },
        new("Octocat", "K=2B2B3A W=FFFFFF",   // 6: tentacles wiggle, hops
            "KK........KK", "KKKKKKKKKKKK", "KKWWKKKKWWKK", "KKWKKKKKWKKK", ".KKKKKKKKKK.", "..KKKKKKKK..", ".K.K.KK.K.K.", "K..K.KK.K..K")
            { Amp = 50, Spd = 1.1, Hop = 10, HopHz = 1.5, Tilt = 0.12, Wig = 6 },
        new("Docker", "B=2496ED L=A9D8FF",   // 7: whale swims along the edge, spouts
            "...LL.LL.L..", "...LL.LL.L..", "BBBBBBBBBBBB", "BKBBBBBBBBBB", "BBBBBBBBBBBB", ".BBBBBBBBBB.", "..BBBBBBBB..")
            { Amp = 190, Spd = 0.35, Bob = 2.5, Lift = 2, Fx = Fx.Spout },
        new("DeepSeek", "B=4D6BFE L=9FB4FF",   // 8: sways, blows bubbles
            "..........B.", ".BBBBB...BB.", "BBBBBBBBBBB.", "BWKBBBBBBBB.", "BBBBBBBBBB..", ".BBLLLLBB...", "..BBBBBB....")
            { Amp = 40, Spd = 0.5, Bob = 4, Lift = 8, Tilt = 0.1, Fx = Fx.Bubbles },
        new("Bugdroid", "G=3DDC84",   // 9: marches, waves
            "..G......G..", "...GGGGGG...", "..GGGGGGGG..", "..GWGGGGWG..", "..GGGGGGGG..", "GGGGGGGGGGGG", "GGGGGGGGGGGG", ".GGGGGGGGGG.", "..GG....GG..", "..GG....GG..")
            { Amp = 120, Spd = 0.7, Hop = 3, HopHz = 3.5, Arms = 1, Fx = Fx.Wave },
        new("Slonik", "B=336791 W=F5F5F5",   // 10: stomps, swings the trunk
            "BBB......BBB", "BBBBBBBBBBBB", "BBBWKBBKWBBB", "BBBBBBBBBBBB", ".BBBBBBBBBB.", "....BBBB....", "....BBBB....", "....BBBB....", "...BB..BB...")
            { Amp = 130, Spd = 0.5, Hop = 2, HopHz = 2.5, Wig = 5 },
        new("Duke", "W=F4F4F4 R=E5352C B=2D7DD2",   // 11: hops, waves both arms
            "....WWWW....", "...WWWWWW...", "..WWKWWKWW..", "..WWWWRRRR..", "..WWWWWRRR..", "...WWWWWW...", "..BBBBBBBB..", ".WBBBBBBBBW.", "..BBBBBBBB..", "..WW....WW..")
            { Amp = 60, Spd = 1.0, Hop = 12, HopHz = 1.6, Arms = 2, Fx = Fx.Wave },
        new("Clyde", "B=5865F2",   // 12: dances to the music
            "..BBBBBBBB..", ".BBBBBBBBBB.", "BBWWBBBBWWBB", "BBWKBBBBWKBB", "BBBBBBBBBBBB", "BBBWWWWWWBBB", ".BBBBBBBBBB.", "..BB....BB..")
            { Amp = 25, Spd = 2.0, Hop = 6, HopHz = 2, Tilt = 0.15, Fx = Fx.Notes },
        new("Glitch", "P=9146FF",   // 13: winks and twitches to the beat
            "PPPPPPPPPPPP", "PPPPPPPPPPPP", "PPPWPPPPWPPP", "PPPWPPPPWPPP", "PPPPPPPPPPPP", "PPPPPPPPPPPP", ".PPPPPPPPPP.", "..PPPP......", "...PP.......")
            { Amp = 20, Spd = 1.0, Hop = 4, HopHz = 3, EyeX = 8, EyeY = 2, Fx = Fx.Wink },
        new("Python", "B=3776AB Y=FFD43B R=E53935",   // 14: a living snake, slithers in a wave
            "..BBBB......", ".BBBBBB.....", "BBWKBBBB....", "BBBBBBBBRR..", ".BBBBBBB....", "..BYYYYB....", "...BBBB.....")
            { Amp = 200, Spd = 0.5, Fx = Fx.Snake },
        new("Chameleon", "G=3FBF4F R=F2C14E",   // 15: crawls slowly, changes colour
            "..GGGG......", ".GGWKGGG....", "GGGGGGGGGGG.", ".GGGGGGGGGGG", "..GG..GG..GG", "..RR..RR.RR.")
            { Amp = 100, Spd = 0.25, Hop = 0.8, HopHz = 2, Fx = Fx.Hue },
        new("Firefox", "O=FF7A1A W=FFF1E0",   // 16: runs, wags its tail
            "OO........OO", "OOO......OOO", "OOOOOOOOOOOO", "OOKOOOOOOKOO", ".OOOWWWWOOO.", "..OOWKKWOO..", "..OOOOOOOO..", "...OO..OO...")
            { Amp = 180, Spd = 1.4, Hop = 3, HopHz = 4, Fx = Fx.Tail },
        new("Sonic", "B=2B6BFF T=F0C898 R=E53935",   // 17: runs, then curls up and rolls
            ".B..B..B....", "BBBBBBBBB...", "BBWWBBWWBB..", "BBWKBBWKBB..", "BBBBBTTTT...", ".BBBBBTTT...", "..BBBBBB....", "..RR..RR....")
            { Amp = 220, Spd = 1.0, Hop = 2, HopHz = 4, Fx = Fx.Roll },
        new("Headcrab", "T=D9B38C R=B3303C",   // 18: jumps along the panel
            "...TTTT.....", "..TTTTTT....", ".TTTTTTTT...", ".TTRRRRTT...", "TTTTTTTTTT..", "T.T.T..T.T.T", "T.T......T.T")
            { Amp = 180, Spd = 0.9, Hop = 22, HopHz = 1.4, Tilt = 0.2 },
        new("Chicken", "W=F8F8F8 R=E53935 Y=F2B01E",   // 19: walks, pecks, clucks
            "....RR......", "...WWWW.....", "..WWKWWYY...", "..WWWWWWY...", "..WWWWW.....", ".WWWWWWWW...", ".WWWWWWWWW..", "..WWWWWWW...", "...YY.YY....")
            { Amp = 80, Spd = 0.5, Hop = 1.5, HopHz = 2.5, Fx = Fx.Cluck },
        new("Creeper", "G=5BBF4A K=1B3A14",   // 20: walks, looks around, sometimes sparks
            "..GGGGGGGG..", "..GGGGGGGG..", "..GKKGGKKG..", "..GKKGGKKG..", "..GGGKKGGG..", "..GGKKKKGG..", "..GGKGGKGG..", "..GGGGGGGG..", "..GGGGGGGG..", "..GG....GG..", "..GG....GG..")
            { Amp = 90, Spd = 0.4, Hop = 1, HopHz = 2, Fx = Fx.Sparks },
        new("Axolotl", "P=F7A8C8 R=E0507A N=B8486A",   // 21: paddles along, yawns
            "R.R......R.R", "RRR.PPPP.RRR", ".RPPPPPPPPR.", "..PKPPPPKP..", "..PPPPPPPP..", "..PPPNNPPP..", "...PPPPPP...", "..PPPPPPPP..", ".PP......PP.")
            { Amp = 70, Spd = 0.45, Hop = 2, HopHz = 2.5, Fx = Fx.Yawn },
        new("Bee", "Y=FFD23F K=181818 W=DDF4FF",   // 22: flies over the panel and buzzes
            "..WWW..WWW..", "..WWW..WWW..", "....YYYY....", "...YYKYYK...", "..YYYYYYYY..", "..KKKKKKKK..", "..YYYYYYYY..", "..KKKKKKKK..", "...YYYYYY...", "....KK......")
            { Amp = 180, Spd = 0.8, Bob = 7, Lift = 22, Fx = Fx.Fly },
    };

    public static readonly string[] BallRows =
    {
        "..BBBB..", ".BBBBBB.", "BBBBTTBB", "BBBBBTBB", "BBTBBBBB", "BBTTBBBB", ".BBBBBB.", "..BBBB..",
    };
}

/// <summary>Draws one mascot sprite (also used for the peek out of the box).</summary>
static class MascotArt
{
    public const double S = 2.6;

    static IBrush Hsv(double h)   // pastel colour for the chameleon
    {
        double F(double n) { double k = (n + h / 60) % 6; return 0.85 - 0.425 * Math.Max(0, Math.Min(Math.Min(k, 4 - k), 1)); }
        return Px.B((byte)(255 * F(5)), (byte)(255 * F(3)), (byte)(255 * F(1)));
    }

    /// <summary>The bottom row of the sprite stands on baseY, centre at x.</summary>
    public static void Draw(DrawingContext c, Art a, double x, double baseY, double t,
        double rot = 0, double sx = 1, double sy = 1, bool sad = false, double mouth = 0)
    {
        if (Math.Abs(sx) < 0.05) sx = sx < 0 ? -0.05 : 0.05;
        var m = Matrix.CreateTranslation(-x, -baseY) * Matrix.CreateScale(sx, sy) * Matrix.CreateRotation(rot) * Matrix.CreateTranslation(x, baseY);
        IBrush? hue = a.Fx == Fx.Hue ? Hsv(t * 40) : null;
        double left = x - 6 * S, top = baseY - a.Rows.Length * S;
        using (c.PushTransform(m))
        {
            for (int r = 0; r < a.Rows.Length; r++)
            {
                double sh = a.Wig >= 0 && r >= a.Wig ? Math.Sin(t * 5) * (r - a.Wig + 1) * 0.35 * S : 0;
                string row = a.Rows[r];
                for (int k = 0; k < row.Length; k++)
                {
                    var b = row[k] == 'G' && hue != null ? hue : a.Brush(row[k]);
                    if (b != null) Sprites.R(c, b, left + k * S + sh, top + r * S, S + 0.2, S + 0.2);
                }
            }
            if (mouth > 0) Sprites.R(c, Px.Ink, left + 4 * S, top + 5 * S, 4 * S, mouth * 2 * S);
            if (sad)   // a tear on each side
            {
                var tear = Px.B(120, 190, 255);
                Sprites.R(c, tear, x - 3.2 * S, top + 3.6 * S, 2, 5);
                Sprites.R(c, tear, x + 2.4 * S, top + 3.6 * S, 2, 5);
            }
        }
    }

    public static void Map(DrawingContext c, Art a, string[] rows, double x, double baseY, double s, double rot)
    {
        var m = Matrix.CreateTranslation(-x, -baseY + rows.Length * s / 2) * Matrix.CreateRotation(rot) * Matrix.CreateTranslation(x, baseY - rows.Length * s / 2);
        using (c.PushTransform(m))
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < rows[r].Length; k++)
                {
                    var b = a.Brush(rows[r][k]);
                    if (b != null) Sprites.R(c, b, x - rows[r].Length * s / 2 + k * s, baseY - (rows.Length - r) * s, s + 0.2, s + 0.2);
                }
    }
}

/// <summary>What a mascot does on the taskbar while it is "out" (t = seconds since it started acting).</summary>
static class MascotAct
{
    public static double XAt(Art a, double bx, double t) => bx + a.Amp * Math.Sin(a.Spd * t);

    public static void Draw(DrawingContext c, Art a, double bx, double gy, double t)
    {
        if (a.Fx == Fx.Snake) { Snake(c, a, bx, gy, t); return; }

        double ph = a.Spd * t, x = XAt(a, bx, t);
        int dir = a.NoFlip || Math.Cos(ph) >= 0 ? 1 : -1;
        if (a.Fx == Fx.Wink && Math.Sin(t * 47) > 0.92) x += 2;   // twitch
        double h = a.Lift + a.Hop * Math.Abs(Math.Sin(Math.PI * a.HopHz * t)) + a.Bob * Math.Sin(t * 2.2);
        double rot = a.Tilt * Math.Sin(t * 7), sx = dir, sy = 1, mouth = 0;
        double baseY = gy - h, top = baseY - a.Height;

        if (a.Fx == Fx.Tail)   // behind the body
        {
            var m = Matrix.CreateTranslation(-(x - dir * 5.5 * MascotArt.S), -(top + 6 * MascotArt.S))
                    * Matrix.CreateRotation(Math.Sin(t * 12) * 0.5) * Matrix.CreateTranslation(x - dir * 5.5 * MascotArt.S, top + 6 * MascotArt.S);
            using (c.PushTransform(m))
            {
                double tx = x - dir * 5.5 * MascotArt.S - (dir > 0 ? 11 : 0), ty = top + 6 * MascotArt.S - 3;
                Sprites.R(c, a.Main, tx, ty, 11, 7);
                Sprites.R(c, Px.White, tx + (dir > 0 ? 0 : 7), ty, 4, 7);
            }
        }

        switch (a.Fx)
        {
            case Fx.Roll when (int)(t / 4) % 2 == 1:   // curled into a ball
                MascotArt.Map(c, a, Arts.BallRows, x, gy - 1, 3.2, x / 7);
                return;
            case Fx.Look:
                {
                    double p5 = t % 5;
                    if (p5 > 3.4 && p5 < 4.6) sx = dir * Math.Cos((p5 - 3.4) / 1.2 * 2 * Math.PI);   // turns around and back
                    break;
                }
            case Fx.Cluck:
                {
                    double pk = t % 3;
                    if (pk > 2 && pk < 2.6) rot += dir * 0.55 * Math.Sin((pk - 2) / 0.6 * Math.PI);   // peck
                    break;
                }
            case Fx.Yawn:
                {
                    double yp = t % 6;
                    if (yp > 4.6 && yp < 5.8) { double u = Math.Sin((yp - 4.6) / 1.2 * Math.PI); mouth = u; sy = 1 + 0.06 * u; }
                    break;
                }
        }

        MascotArt.Draw(c, a, x, baseY, t, rot, sx, sy, false, mouth);
        Extras(c, a, x, baseY, top, dir, t);
    }

    static void Extras(DrawingContext c, Art a, double x, double baseY, double top, int dir, double t)
    {
        double S = MascotArt.S;
        switch (a.Fx)
        {
            case Fx.Claw:   // click-click
                if (t % 0.7 < 0.2)
                    foreach (int s in new[] { -1, 1 })
                    {
                        Sprites.R(c, Px.Gold, x + s * 17, top - 2, 3, 2);
                        Sprites.R(c, Px.Gold, x + s * 20, top - 5, 2, 3);
                    }
                break;
            case Fx.Spout:   // fountain from the back
                {
                    double ph = t % 4;
                    if (ph < 2.4)
                        for (int i = 0; i < 6; i++)
                        {
                            double u = Math.Clamp((ph - i * 0.1) / 1.4, 0, 1);
                            if (u <= 0 || u >= 1) continue;
                            Sprites.R(c, Px.B(170, 220, 255), x + (i - 2.5) * 2.6 * u - 1 + 4, top - 2 - Math.Sin(u * Math.PI) * 20, 2.4, 2.4);
                        }
                    break;
                }
            case Fx.Bubbles:
                for (int i = 0; i < 4; i++)
                {
                    double u = (t * 0.45 + i * 0.25) % 1, s = 2 + u * 2.5;
                    Sprites.R(c, Px.B(220, 235, 255, (byte)(220 * (1 - u))), x - dir * 8 + Math.Sin(u * 9 + i) * 3, top + 3 * S - u * 28, s, s);
                }
                break;
            case Fx.Sparks:
                {
                    double sp = t % 8;
                    if (sp > 6.6 && sp < 7.6)
                    {
                        double r = 8 + 20 * (sp - 6.6);
                        for (int i = 0; i < 8; i++)
                        {
                            double ang = i * Math.PI / 4;
                            Sprites.R(c, i % 2 == 0 ? Px.PopY : Px.White, x + Math.Cos(ang) * r - 2, top + a.Height / 2 + Math.Sin(ang) * r - 2, 4, 4);
                        }
                    }
                    break;
                }
            case Fx.Notes:
                for (int i = 0; i < 3; i++)
                {
                    double u = (t * 0.5 + i / 3.0) % 1, nx = x + (i - 1) * 10 + Math.Sin(u * 6) * 3, ny = top - 2 - u * 24;
                    var nb = Px.B(190, 170, 255, (byte)(255 * (1 - u)));
                    Sprites.R(c, nb, nx, ny + 5, 4, 3);
                    Sprites.R(c, nb, nx + 3, ny, 1.5, 7);
                }
                break;
            case Fx.Wave:
                for (int s = 0; s < a.Arms; s++)
                {
                    int side = a.Arms == 2 ? (s == 0 ? -1 : 1) : dir;
                    double px = x + side * 6 * S, py = top + 4.5 * S;
                    var m = Matrix.CreateTranslation(-px, -py) * Matrix.CreateRotation(side * (2.5 + 0.55 * Math.Sin(t * 10 + s * 2))) * Matrix.CreateTranslation(px, py);
                    using (c.PushTransform(m)) Sprites.R(c, a.Main, px - 1.2, py, 2.4, 10);
                }
                break;
            case Fx.Wink:
                if (t % 1.4 < 0.18 && a.EyeX >= 0)
                {
                    double ex = x + dir * (a.EyeX - 6) * S - (dir < 0 ? S : 0);
                    Sprites.R(c, a.Main, ex, top + a.EyeY * S, S, 2 * S);
                    Sprites.R(c, Px.White, ex, top + (a.EyeY + 0.9) * S, S, 1.5);
                }
                break;
            case Fx.Cluck:
                {
                    double cp = t % 6;
                    if (cp > 3.4 && cp < 5.0) Sprites.Bubble(c, x, top - 6, "куд-кудах!");
                    break;
                }
            case Fx.Fly:
                {
                    double fp = t % 5;
                    if (fp < 1.4) Sprites.Bubble(c, x, top - 4, "бзз~");
                    var w = Px.B(221, 244, 255, 150);   // wings blur
                    Sprites.R(c, w, x - 17, top - 3 - (t * 30 % 2), 10, 3);
                    Sprites.R(c, w, x + 7, top - 3 - (t * 30 % 2), 10, 3);
                    break;
                }
        }
    }

    static void Snake(DrawingContext c, Art a, double bx, double gy, double t)
    {
        IBrush blue = a.Brush('B')!, yellow = a.Brush('Y')!;
        for (int i = 14; i >= 0; i--)
        {
            double tt = t - i * 0.11, x = bx + a.Amp * Math.Sin(a.Spd * tt);
            double y = gy - 9 - 7 * Math.Sin(i * 0.7 - t * 6), s = i == 0 ? 11 : 9.5 - i * 0.12;
            Sprites.R(c, i % 2 == 0 ? blue : yellow, x - s / 2, y - s / 2, s, s);
            if (i == 0)
            {
                int dir = Math.Cos(a.Spd * t) >= 0 ? 1 : -1;
                Sprites.R(c, Px.White, x + dir * 2 - 1.5, y - 3.5, 3, 3);
                Sprites.R(c, Px.Ink, x + dir * 3 - 0.8, y - 2.5, 1.6, 1.6);
                if (t * 6 % 1 < 0.5) Sprites.R(c, a.Brush('R')!, x + dir * 6 - (dir < 0 ? 5 : 0), y + 0.5, 5, 1.4);   // tongue
            }
        }
    }
}

/// <summary>One trip of a mascot: the box slides to the middle of the taskbar, he climbs out, acts, climbs back.</summary>
sealed class Outing
{
    public Art Art = null!;
    public string Label = "";
    public double Cx, StartX, StartFloor, T;

    // timeline (seconds)
    public const double SlideIn = 1.6, LidOpen = 2.4, Risen = 3.6, HopOut = 4.4, ActEnd = 31.4, WalkBack = 32.6,
        HopIn = 33.4, Sunk = 34.6, LidShut = 35.4, Total = 37.0;
}

class StageView : Control
{
    public Outing? O;

    static double Seg(double t, double a, double b) => Math.Clamp((t - a) / (b - a), 0, 1);
    static double Ease(double x) => x * x * (3 - 2 * x);
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    void Inside(DrawingContext c, Art a, double x, double floor, double off, double t)
    {
        using (c.PushClip(new Rect(0, 0, Bounds.Width, floor)))
            MascotArt.Draw(c, a, x, floor - 10 + off, t, 0, 1, 1, false);
    }

    public override void Render(DrawingContext c)
    {
        var o = O;
        if (o == null) return;
        var a = o.Art;
        double t = o.T, g = Pet.FeetY;

        double bx = o.Cx, fl = g;
        if (t < Outing.SlideIn)
        {
            double u = Ease(t / Outing.SlideIn);
            bx = Lerp(o.StartX, o.Cx, u); fl = Lerp(o.StartFloor, g, u) - 16 * Math.Sin(Math.PI * u);
        }
        else if (t >= Outing.LidShut)
        {
            double u = Ease(Seg(t, Outing.LidShut, Outing.Total));
            bx = Lerp(o.Cx, o.StartX, u); fl = Lerp(g, o.StartFloor, u) - 16 * Math.Sin(Math.PI * u);
        }
        double open = 0.85 * Ease(Seg(t, Outing.SlideIn, Outing.LidOpen)) * (1 - Ease(Seg(t, Outing.Sunk, Outing.LidShut)));
        double outX = o.Cx + 40;

        Sprites.MiniBox(c, bx, fl, o.Label, 0, false);
        if (t >= Outing.LidOpen && t < Outing.Risen) Inside(c, a, bx, fl, 40 * (1 - Ease(Seg(t, Outing.LidOpen, Outing.Risen))), t);
        else if (t >= Outing.HopIn && t < Outing.Sunk) Inside(c, a, bx, fl, 40 * Ease(Seg(t, Outing.HopIn, Outing.Sunk)), t);
        Sprites.MiniBox(c, bx, fl, o.Label, open, true);

        double mx = outX, tipY = g - a.Height - a.Lift - 14;
        if (t >= Outing.Risen && t < Outing.HopOut)          // hops out of the box
        {
            double u = Seg(t, Outing.Risen, Outing.HopOut);
            mx = Lerp(bx, outX, u);
            MascotArt.Draw(c, a, mx, Lerp(fl - 10, g, Ease(u)) - 24 * Math.Sin(Math.PI * u), t);
        }
        else if (t >= Outing.HopOut && t < Outing.ActEnd)    // acts on the taskbar
        {
            MascotAct.Draw(c, a, outX, g, t - Outing.HopOut);
            mx = MascotAct.XAt(a, outX, t - Outing.HopOut);
        }
        else if (t >= Outing.ActEnd && t < Outing.WalkBack)  // walks back to the box
        {
            double u = Ease(Seg(t, Outing.ActEnd, Outing.WalkBack)), xEnd = MascotAct.XAt(a, outX, Outing.ActEnd - Outing.HopOut);
            mx = Lerp(xEnd, outX, u);
            MascotArt.Draw(c, a, mx, g - Math.Abs(Math.Sin(t * 11)) * 2, t, 0, xEnd > outX ? -1 : 1);
        }
        else if (t >= Outing.WalkBack && t < Outing.HopIn)   // jumps into the box
        {
            double u = Seg(t, Outing.WalkBack, Outing.HopIn);
            mx = Lerp(outX, bx, u);
            MascotArt.Draw(c, a, mx, Lerp(g, fl - 10, Ease(u)) - 24 * Math.Sin(Math.PI * u), t);
        }
        if (t >= Outing.HopOut && t < Outing.HopOut + 2.4) Sprites.Bubble(c, mx, tipY, "Я — " + a.Name + "!");
    }
}

/// <summary>Transparent click-through window over the whole taskbar where the outing plays.</summary>
class StageWindow : Window
{
    readonly StageView view = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    DateTime last = DateTime.UtcNow, lastRaise = DateTime.MinValue;
    IntPtr hwnd = IntPtr.Zero;
    double leftPx, rightPx, groundPx;

    public StageWindow()
    {
        Content = view;
        Width = 800; Height = Pet.WinH;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        World.Outing = Start;
        timer.Tick += (_, _) => OnTick();
        timer.Start();
    }

    void Geometry()
    {
        var scr = Screens.Primary;
        if (scr == null) return;
        var b = scr.Bounds;
        if (Win.TryGetTaskbar(out var tb) && tb.Width > tb.Height && tb.Y > b.Y + b.Height / 2)
        { groundPx = tb.Y; leftPx = tb.X; rightPx = tb.Right; }
        else
        { groundPx = scr.WorkingArea.Bottom; leftPx = b.X; rightPx = b.Right; }
        double s = RenderScaling;
        Width = Math.Clamp((rightPx - leftPx) / s, 400, 4000);
        Position = new PixelPoint((int)leftPx, (int)(groundPx - (Pet.FeetY - Pet.Sink) * s));
    }

    void Start(int slotIdx, double boxScreenX, double startFloor)
    {
        var slot = Mascots.Slots[slotIdx];
        if (slot.Art == null || view.O != null) return;
        Geometry();
        view.O = new Outing
        {
            Art = slot.Art, Label = slot.Label, T = 0, StartFloor = startFloor,
            Cx = Width / 2, StartX = (boxScreenX - leftPx) / RenderScaling,
        };
        World.OutIdx = slotIdx;
    }

    void OnTick()
    {
        var utc = DateTime.UtcNow;
        double dt = Math.Min((utc - last).TotalSeconds, 0.1);
        last = utc;
        var o = view.O;
        if (o == null) { if (IsVisible) Hide(); return; }
        if (!IsVisible)
        {
            Show();
            hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            Win.MakeToolWindow(hwnd);
            Win.SetClickThrough(hwnd, true);   // never steals a click
        }
        o.T += dt;
        if ((utc - lastRaise).TotalSeconds >= 1) { lastRaise = utc; Win.Raise(hwnd); }
        if (o.T >= Outing.Total) { view.O = null; World.OutIdx = -1; Hide(); return; }
        view.InvalidateVisual();
    }
}
