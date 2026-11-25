using Avalonia;
using Avalonia.Media;

namespace Clawd;

/// <summary>Game mode ON: Clawd turns to you, waves goodbye, a door appears, he walks in and the door closes.</summary>
public class EnterGameScene : Scene
{
    public override double Duration => 9;
    public override double WinW => 300;
    public override State After => State.InGame;

    const double DoorDx = 100;   // the door appears to the right of the crab
    double open, grow;
    string? say;

    public override void OnEnd(Pet p) { GameMode.PetInside = true; GameMode.PickHouse(); }

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.Squash = 1; p.CrabDx = 0; p.CrabDy = 0;
        p.Eyes = Eyes.Open; p.Wave = -1; p.Size = 1; p.Hidden = false;
        say = null;

        if (T < 0.8)   // turns to face you: a little hop
        {
            double k = Math.Sin(Seg(T, 0, 0.8) * Math.PI);
            p.Eyes = Eyes.Wide; p.Squash = 1 - 0.1 * k; p.CrabDy = -4 * k;
        }
        else if (T < 3.4)   // waves goodbye
        {
            p.Eyes = Eyes.Happy; p.Wave = T - 0.8;
            if (T > 1.0 && T < 3.1) say = "Пока! Я играть!";
        }

        grow = Ease(Seg(T, 3.2, 4.2)) * (1 - Ease(Seg(T, 8.0, 9.0)));
        if (T >= 4.2 && T < 5.4) { p.CrabDx = DoorDx * Ease(Seg(T, 4.2, 5.4)); p.Walking = true; }
        else if (T >= 5.4) p.CrabDx = DoorDx;

        open = Ease(Seg(T, 5.4, 6.2)) * (1 - Ease(Seg(T, 7.2, 8.0)));
        if (T >= 6.2 && T < 7.0)   // steps into the doorway and shrinks into the depth
        {
            double k = Ease(Seg(T, 6.2, 7.0));
            p.Size = 1 - 0.55 * k; p.CrabDy = -5 * k; p.Walking = true;
        }
        if (T >= 7.0) p.Hidden = true;
    }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy) =>
        TrayDoor.Draw(c, cx + DoorDx, fy, grow, open, false);

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        TrayDoor.Draw(c, cx + DoorDx, fy, grow, open, true);
        if (say != null) Sprites.Bubble(c, cx + p.CrabDx, fy - 46 + p.CrabDy, say);
    }
}

/// <summary>Game mode OFF: a door appears, opens, Clawd walks out of it back onto the taskbar.</summary>
public class ExitGameScene : Scene
{
    public override double Duration => 7;
    public override double WinW => 300;

    const double DoorDx = 100;
    double open, grow;
    string? say;

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.Squash = 1; p.CrabDx = DoorDx; p.CrabDy = 0;
        p.Eyes = Eyes.Open; p.Wave = -1; p.Size = 1; p.Hidden = true;
        say = null;

        grow = Ease(Seg(T, 0, 0.8)) * (1 - Ease(Seg(T, 3.6, 4.4)));
        open = Ease(Seg(T, 0.8, 1.6)) * (1 - Ease(Seg(T, 2.8, 3.6)));

        if (T < 1.6) return;
        p.Hidden = false;
        if (T < 2.4)   // comes out of the depth
        {
            double k = Ease(Seg(T, 1.6, 2.4));
            p.Size = 0.45 + 0.55 * k; p.CrabDy = -5 * (1 - k); p.Walking = true;
        }
        else if (T < 3.6)   // walks back to his spot
        {
            p.Dir = -1; p.CrabDx = DoorDx * (1 - Ease(Seg(T, 2.4, 3.6))); p.Walking = true;
        }
        else
        {
            p.CrabDx = 0; p.Eyes = Eyes.Happy;
            if (T < 6.4) { p.Wave = T - 3.6; say = "Я вернулся!"; }
        }
    }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy) =>
        TrayDoor.Draw(c, cx + DoorDx, fy, grow, open, false);

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        TrayDoor.Draw(c, cx + DoorDx, fy, grow, open, true);
        if (say != null) Sprites.Bubble(c, cx + p.CrabDx, fy - 46 + p.CrabDy, say);
    }
}
