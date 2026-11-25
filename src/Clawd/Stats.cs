using System.Text.Json;

namespace Clawd;

/// <summary>Clawd's persistent "feelings" (0..100) plus the live AFK timer.</summary>
static class Stats
{
    public static double Attachment = 35;   // how attached he is to you
    public static double Together;          // how much you've been spending time with him (pokes, feeding)
    public static double Jealousy;          // grows when you keep clicking the corner boxes
    public static double Hunger;            // 0..1, mirrored from Pet for the panel
    public static double AfkSeconds;        // seconds without any keyboard / mouse input
    public static bool PanelVisible = true;
    public static bool JealousyReact;       // one-shot flag for Pet

    static double saveT;
    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clawd", "stats.json");

    public record Saved(double Attachment, double Together, double Jealousy, bool PanelVisible, DateTime At);

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var s = JsonSerializer.Deserialize<Saved>(File.ReadAllText(FilePath));
            if (s == null) return;
            double mins = Math.Clamp((DateTime.Now - s.At).TotalMinutes, 0, 1440);
            Attachment = Math.Clamp(s.Attachment, 0, 100);
            Together = Math.Max(0, s.Together - 0.5 * mins);
            Jealousy = Math.Max(0, s.Jealousy - 0.3 * mins);
            PanelVisible = s.PanelVisible;
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new Saved(Attachment, Together, Jealousy, PanelVisible, DateTime.Now)));
        }
        catch { }
    }

    public static void Tick(double dt, double idleSec, bool asleep)
    {
        AfkSeconds = idleSec;
        double m = dt / 60;
        Together = Math.Max(0, Together - 0.5 * m);
        Jealousy = Math.Max(0, Jealousy - 0.3 * m);
        if (Together > 40) Attachment += 0.4 * m;                 // time together bonds you
        if (!asleep && idleSec > 600) Attachment -= 0.3 * m;       // you've been away for a long time
        if (Jealousy > 70) Attachment -= 0.2 * m;                  // jealousy eats at the bond
        Attachment = Math.Clamp(Attachment, 0, 100);
        saveT += dt;
        if (saveT >= 30) { saveT = 0; Save(); }
    }

    public static void Poked()
    {
        Together = Math.Min(100, Together + 6);
        Jealousy = Math.Max(0, Jealousy - 3);
        Attachment = Math.Min(100, Attachment + 0.5);
    }

    public static void Fed()
    {
        Attachment = Math.Min(100, Attachment + 4);
        Together = Math.Min(100, Together + 10);
    }

    public static void Stroked(double dt)   // being petted: you spend time with him, he feels loved
    {
        Together = Math.Min(100, Together + 3 * dt);
        Jealousy = Math.Max(0, Jealousy - 1 * dt);
        Attachment = Math.Min(100, Attachment + 0.05 * dt);
    }

    public static void BoxClicked()
    {
        Jealousy = Math.Min(100, Jealousy + 7);
        if (Jealousy >= 60) JealousyReact = true;
    }

    public static void Betrayed() => Attachment = Math.Max(0, Attachment - 15);   // used by the music/sleep scene (next step)
}
