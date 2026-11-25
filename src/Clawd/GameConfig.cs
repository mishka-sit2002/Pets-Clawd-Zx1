using System.Text.Json;

namespace Clawd;

public enum ViewerCorner { BottomRight, BottomLeft, TopLeft, TopRight }

/// <summary>Small persistent config for the Game mode (LocalAppData/Clawd/config.json).</summary>
public sealed class GameConfig
{
    public bool HasSeenRoomTour { get; set; }
    public ViewerCorner Corner { get; set; } = ViewerCorner.BottomRight;          // cinema flat
    public ViewerCorner WindowCorner { get; set; } = ViewerCorner.TopRight;      // window flat
    public bool HasSeenWindowTour { get; set; }

    static GameConfig? cur;
    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clawd", "config.json");

    public static GameConfig Current => cur ??= Load();

    static GameConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(FilePath)) ?? new GameConfig();
        }
        catch { }
        return new GameConfig();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current));
        }
        catch { }
    }
}

/// <summary>Shared Game-mode switches between the tray pet, the viewer window and the menu.</summary>
static class GameMode
{
    public static bool Enabled;      // the "Игровой режим" switch
    public static bool PetInside;    // Clawd has walked through the tray door
    public static bool Night;        // it is his bedtime (22:00-08:00 or the test override)
    public static bool ViewerOpen;   // the viewer window is on screen
    public static HouseKind House;   // which flat he moved into this time
    public static int ForceHouse = -1;   // test hook: -1 = random, 0 = cinema, 1 = window

    public static void PickHouse() =>
        House = ForceHouse >= 0 ? (HouseKind)ForceHouse : Random.Shared.Next(2) == 0 ? HouseKind.Cinema : HouseKind.Window;
}
