using Avalonia;
using Avalonia.Media;

namespace Clawd;

/// <summary>A scripted sequence driven by time T (seconds). Scenes pose the crab and draw props/humans.</summary>
public abstract class Scene
{
    public double T;
    public abstract double Duration { get; }
    public abstract double WinW { get; }
    public virtual State After => State.Idle;
    public bool Done => T >= Duration;

    public abstract void Pose(Pet p);
    public virtual void RenderBack(DrawingContext c, Pet p, double cx, double fy) { }
    public virtual void RenderFront(DrawingContext c, Pet p, double cx, double fy) { }
    public virtual void OnEnd(Pet p) { }
    public virtual Rect? CrabClip(Pet p, double cx) => null;

    protected static double Seg(double t, double a, double b) => Math.Clamp((t - a) / (b - a), 0, 1);
    protected static double Ease(double x) => x * x * (3 - 2 * x);
    protected static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

/// <summary>Click on hungry Clawd: humans run in, build a data center, Clawd eats tokens, humans leave.</summary>
public class FeedScene : Scene
{
    public override double Duration => 16;
    public override double WinW => 360;

    static readonly Color[] Shirts = { Color.FromRgb(66, 133, 244), Color.FromRgb(52, 168, 83), Color.FromRgb(234, 67, 53) };
    static readonly double[] Target = { 45, 112, -62 };   // x relative to crab center
    static readonly double[] Delay = { 0, 0.4, 0.2 };
    static readonly int[] StillDir = { 1, -1, 1 };

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.CrabDx = 0; p.CrabDy = 0;
        p.Eyes = T < 2.5 ? Eyes.Sad : T < 9 ? Eyes.Open : T < 13.5 ? Eyes.Happy : Eyes.Open;
        p.Squash = T > 9.5 && T < 13.5 ? 1 + 0.05 * Math.Sin(T * 14) : 1;
    }

    public override void OnEnd(Pet p) { p.Hunger = 0; Stats.Fed(); }

    (double x, bool moving, int dir) Human(int i, double cx, double w)
    {
        double target = cx + Target[i], from = i == 2 ? -20 : w + 20;
        double a = Seg(T, Delay[i], Delay[i] + 2.3);
        double b = Seg(T, 13.5 + Delay[i] * 0.5, 15.8);
        bool moving = (a > 0 && a < 1) || (b > 0 && b < 1);
        double x = b > 0 ? Lerp(target, from, Ease(b)) : Lerp(from, target, Ease(a));
        int dir = !moving ? StillDir[i]
                : b > 0 ? (from > target ? 1 : -1)
                : (from > target ? -1 : 1);
        return (x, moving, dir);
    }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        double prog = Seg(T, 2.8, 9);
        double op = 1 - Seg(T, 14.6, 16);
        using (c.PushOpacity(op))
            Sprites.DataCenter(c, cx + 62, fy, prog, T);
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        for (int i = 0; i < 3; i++)
        {
            var (x, moving, dir) = Human(i, cx, p.WinW);
            bool working = i < 2 && T > 3.0 && T < 9.2 && !moving;
            Sprites.Human(c, x, fy, Shirts[i], dir, moving, T, working, i * 0.7);
        }
        // tokens flying from the data center into Clawd's mouth
        for (int k = 0; k < 12; k++)
        {
            double u = (T - (9 + 0.35 * k)) / 1.1;
            if (u < 0 || u > 1) continue;
            double x = Lerp(cx + 83, cx + 10, u);
            double y = Lerp(fy - 44, fy - 14, u) - 22 * Math.Sin(Math.PI * u);
            Sprites.Token(c, x, y);
        }
    }
}

/// <summary>22:00: Clawd yawns, humans bring a bed and a nightstand and tuck him in.</summary>
public class BedScene : Scene
{
    public override double Duration => 10;
    public override double WinW => 360;
    public override State After => State.Sleeping;

    static readonly Color ShirtA = Color.FromRgb(66, 133, 244), ShirtB = Color.FromRgb(251, 188, 5);

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.CrabDx = 0;
        p.Eyes = T < 1.0 ? Eyes.Open : Eyes.Closed;
        double j = Seg(T, 3.4, 4.0);
        p.CrabDy = -Sprites.BedTop * Ease(j) - 8 * Math.Sin(Math.PI * j);   // hop onto the bed
        p.Squash = 1 + 0.12 * Math.Sin(Math.PI * Seg(T, 0.2, 1.2));       // yawn
    }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        double bedX = Lerp(p.WinW + 60, cx, Ease(Seg(T, 0.2, 3.2)));
        double nsX = Lerp(-30, cx + 75, Ease(Seg(T, 0.4, 3.0)));
        Sprites.Bed(c, bedX, fy);
        Sprites.Nightstand(c, nsX, fy, true);
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        double w = p.WinW;
        double bedX = Lerp(w + 60, cx, Ease(Seg(T, 0.2, 3.2)));
        double nsX = Lerp(-30, cx + 75, Ease(Seg(T, 0.4, 3.0)));

        Sprites.Blanket(c, cx, fy, Seg(T, 4.4, 6.0));

        // human A pushes the bed from the right, tucks Clawd in, leaves right
        double ax = T < 6.5 ? bedX + 52 : Lerp(cx + 52, w + 20, Ease(Seg(T, 6.5, 8.6)));
        bool amove = T < 3.2 || (T >= 6.5 && T < 8.6);
        int adir = T < 3.2 ? -1 : T < 6.5 ? -1 : 1;
        Sprites.Human(c, ax, fy, ShirtA, adir, amove, T, T > 4.4 && T < 6.0, 0);

        // human B pushes the nightstand from the left, steps aside, leaves left
        double bx = T < 3.0 ? nsX - 18
                  : T < 3.8 ? Lerp(cx + 57, cx - 60, Ease(Seg(T, 3.0, 3.8)))
                  : T < 7.0 ? cx - 60
                  : Lerp(cx - 60, -20, Ease(Seg(T, 7.0, 9.0)));
        bool bmove = T < 3.8 || T >= 7.0;
        int bdir = T < 3.0 ? 1 : -1;
        Sprites.Human(c, bx, fy, ShirtB, bdir, bmove, T, false, 0.5);

        if (T > 6.5) Sprites.Zzz(c, cx + 14, fy - Sprites.BedTop - 34, T);
    }
}

/// <summary>08:00: Clawd stretches, hops out of bed, walks to the nightstand and drinks coffee.</summary>
public class WakeScene : Scene
{
    public override double Duration => 7.6;
    public override double WinW => 260;

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.CrabDx = 0; p.Squash = 1;
        p.Eyes = T < 1.0 ? Eyes.Closed : Eyes.Open;
        p.CrabDy = -Sprites.BedTop;

        if (T >= 1.0 && T < 2.2) p.Squash = 1 + 0.15 * Math.Sin(Math.PI * Seg(T, 1.0, 2.2));   // stretch
        if (T >= 2.2)
        {
            double h = Seg(T, 2.2, 2.8);
            p.CrabDy = -Sprites.BedTop * (1 - Ease(h)) - 6 * Math.Sin(Math.PI * h);             // hop down
        }
        double w = Seg(T, 2.8, 4.4);
        p.CrabDx = 40 * Ease(w);
        p.Walking = w > 0 && w < 1;
        if (T > 5.0 && T < 6.2) p.Eyes = Eyes.Happy;                                            // good coffee
    }

    public override void OnEnd(Pet p)
    {
        p.X += p.CrabDx * p.Scale;   // he ended up next to the nightstand: keep him there
        p.CrabDx = 0;
    }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        using (c.PushOpacity(1 - Seg(T, 6.4, 7.6)))
        {
            Sprites.Bed(c, cx, fy);
            Sprites.Nightstand(c, cx + 75, fy, false);
        }
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        using (c.PushOpacity(1 - Seg(T, 6.4, 7.6)))
            Sprites.Blanket(c, cx, fy, 1 - Seg(T, 1.6, 2.2));

        double lift = Ease(Seg(T, 4.4, 5.2)) - Ease(Seg(T, 6.0, 6.6));
        double x = Lerp(cx + 71, cx + 58, lift), y = Lerp(fy - 26, fy - 16, lift);
        Sprites.Cup(c, x, y, T, lift > 0.1);
    }
}

/// <summary>Easter egg (very rare): like an iron golem giving a poppy to a villager, Clawd gives a flower to a tiny human.</summary>
public class FlowerScene : Scene
{
    public override double Duration => 11;
    public override double WinW => 260;

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.CrabDx = 0; p.CrabDy = 0; p.Squash = 1;
        p.Icon = T >= 6.4 && T < 9.0 ? Icon.Heart : Icon.None;
        p.Eyes = T < 2.6 ? Eyes.Open : T < 3.4 ? Eyes.Wide : T < 6.2 ? Eyes.Open : T < 9.3 ? Eyes.Happy : Eyes.Open;
    }

    double Vx(double cx, double w) =>
        T < 2.6 ? Lerp(w + 20, cx + 60, Ease(Seg(T, 0.3, 2.6)))
      : T < 8.8 ? cx + 60
      : Lerp(cx + 60, w + 20, Ease(Seg(T, 8.8, 10.8)));

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        double vx = Vx(cx, p.WinW);
        bool moving = (T > 0.3 && T < 2.6) || T > 8.8;
        Sprites.Villager(c, vx, fy, T < 8.8 ? -1 : 1, moving, T);

        if (T >= 3.2)
        {
            double px, py;
            if (T < 6.2)
            {
                double lift = Ease(Seg(T, 3.2, 4.6));
                px = Lerp(cx + 28, cx + 44, lift);
                py = Lerp(fy - 18, fy - 32, lift) + (T > 4.6 ? Math.Sin(T * 3) : 0);
            }
            else
            {
                double t2 = Ease(Seg(T, 6.2, 7.0));   // the villager takes the flower
                px = Lerp(cx + 44, vx - 9, t2);
                py = Lerp(fy - 32, fy - 22, t2);
            }
            Sprites.Poppy(c, px, py);
        }
        if (T > 7.0 && T < 9.2)
            Sprites.Heart(c, vx, fy - 40 + Math.Sin(T * 6) * 2, 3);
    }
}

/// <summary>
/// Easter egg: July 1 (7.01, the Zx1 birthday), mouse untouched for 3 minutes. Clawd plants a Zx1 flag (Z axis),
/// walks ALONE into a portal marked x (the unknown) and comes back with a green CUDA chip (1 = just him).
/// </summary>
public class ZxScene : Scene
{
    public override double Duration => 15;
    public override double WinW => 300;

    const double PortalDx = 85, FlagDx = -70;

    // crab (and his hat/chip) are hidden once they cross the portal's left edge
    public override Rect? CrabClip(Pet p, double cx) => new Rect(0, 0, cx + PortalDx - 3, Pet.WinH);

    public override void Pose(Pet p)
    {
        p.Walking = false; p.CrabDy = 0; p.Squash = 1; p.Icon = Icon.None;
        p.Dir = 1; p.CrabDx = 0; p.Eyes = Eyes.Open;

        if (T >= 1.0 && T < 2.6) p.Dir = -1;                                                  // looks at the flag
        if (T >= 2.4 && T < 4.0) p.Eyes = Eyes.Happy;
        if (T >= 2.4 && T < 2.9) p.CrabDy = -6 * Math.Sin(Math.PI * Seg(T, 2.4, 2.9));        // little hop
        if (T >= 5.4 && T < 6.2) { p.Eyes = Eyes.Wide; p.Dir = T < 5.8 ? -1 : 1; }             // looks around

        double walkIn = Seg(T, 6.2, 8.6), walkOut = Seg(T, 10.2, 11.8);
        if (T >= 6.2 && T < 10.2)
        {
            p.CrabDx = 112 * Ease(walkIn);
            p.Walking = walkIn > 0 && walkIn < 1;
        }
        else if (T >= 10.2)
        {
            p.CrabDx = Lerp(112, 40, Ease(walkOut));
            p.Walking = walkOut > 0 && walkOut < 1;
            p.Dir = -1; p.Eyes = Eyes.Happy;
        }
        if (T >= 11.8 && T < 13.6) { p.Eyes = Eyes.Happy; p.Squash = 1 + 0.04 * Math.Sin(T * 12); }
        if (T >= 13.6) p.Eyes = Eyes.Open;
    }

    public override void OnEnd(Pet p) { p.X += p.CrabDx * p.Scale; p.CrabDx = 0; }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        using (c.PushOpacity(1 - Seg(T, 13.8, 14.8)))
            Sprites.Flag(c, cx + FlagDx, fy, Seg(T, 1.0, 2.2), Seg(T, 2.2, 2.9));

        double open = Ease(Seg(T, 4.0, 5.2)) * (1 - Ease(Seg(T, 13.6, 14.6)));
        if (open > 0) Sprites.Portal(c, cx + PortalDx, fy - 22, open, T, T > 8.6 && T < 10.2);
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        double x = cx + p.CrabDx, top = fy + p.CrabDy - 8 * Sprites.PX * p.Squash;

        double za = Seg(T, 2.9, 3.3) * (1 - Seg(T, 4.0, 4.5));      // Z axis arrow over his head
        if (za > 0)
        {
            using (c.PushOpacity(za)) Sprites.ZArrow(c, x, top);
        }

        using (c.PushClip(new Rect(0, 0, cx + PortalDx - 3, Pet.WinH)))
        {
            if (T >= 5.0 && T < 14.6) Sprites.Hat(c, x, top);
            if (T >= 10.4 && T < 14.0)
            {
                using (c.PushOpacity(1 - Seg(T, 13.2, 14.0)))
                    Sprites.Chip(c, x, top - 24 + Math.Sin(T * 5) * 2, T);
            }
        }
    }
}

/// <summary>First launch: a box drops onto the taskbar, wobbles, Clawd pops out, looks around, jumps out and says hi.</summary>
public class BoxScene : Scene
{
    public override double Duration => 9;
    public override double WinW => 200;

    // hide everything below the floor line while he is still "inside" the box
    public override Rect? CrabClip(Pet p, double cx) => new Rect(0, 0, p.WinW, Pet.FeetY);

    public override void Pose(Pet p)
    {
        p.Walking = false; p.Squash = 1; p.Icon = Icon.None; p.Dir = 1; p.CrabDx = 0; p.Eyes = Eyes.Open;

        double rise = Ease(Seg(T, 2.0, 3.2));
        p.CrabDy = 34 * (1 - rise) - 10 * rise;                       // pops up out of the box
        if (T >= 3.2 && T < 4.2) { p.Dir = T < 3.7 ? -1 : 1; p.Eyes = Eyes.Wide; }   // peeks around
        if (T >= 4.2)
        {
            double u = Seg(T, 4.2, 5.4);                              // jumps out to the right
            p.CrabDx = 60 * Ease(u);
            p.CrabDy = Lerp(-10, 0, u) - 30 * Math.Sin(Math.PI * u);
            p.Eyes = Eyes.Happy;
            if (T >= 5.4 && T < 6.0) p.Squash = 1 - 0.15 * Math.Sin(Math.PI * Seg(T, 5.4, 6.0));
        }
        if (T >= 8.0) p.Eyes = Eyes.Open;
    }

    public override void OnEnd(Pet p) { p.X += p.CrabDx * p.Scale; p.CrabDx = 0; }

    (double dx, double dy) BoxOff()
    {
        double dy = T < 0.8 ? -70 * (1 - Ease(Seg(T, 0, 0.8)))
                            : -6 * Math.Abs(Math.Sin((T - 0.8) * 9)) * (1 - Seg(T, 0.8, 1.6));
        double dx = T > 1.2 && T < 2.0 ? Math.Sin(T * 40) * 1.5 : 0;   // something is moving inside
        return (dx, dy);
    }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        var (dx, dy) = BoxOff();
        using (c.PushOpacity(1 - Seg(T, 7.6, 8.8)))
        using (c.PushTransform(Matrix.CreateTranslation(dx, dy)))
            Sprites.BoxBack(c, cx, fy);
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        var (dx, dy) = BoxOff();
        using (c.PushOpacity(1 - Seg(T, 7.6, 8.8)))
        using (c.PushTransform(Matrix.CreateTranslation(dx, dy)))
            Sprites.BoxFront(c, cx, fy, Ease(Seg(T, 1.8, 2.4)));

        if (T >= 5.6 && T < 6.9)
            Sprites.Bubble(c, cx + 30, fy - 38, $"Привет, я {(p.Name.Length > 0 ? p.Name : "Clawd")}!");
        else if (T >= 6.9 && T < 8.6 && p.Name.Length == 0)
            Sprites.Bubble(c, cx + 30, fy - 38, "Дай мне имя (ПКМ)");
    }
}

/// <summary>
/// Music stayed loud while he slept and you ignored his request: he climbs down angrily, stomps to a door,
/// slams it and hangs a "ТИХО" sign. Afterwards he is Away until the music gets quieter.
/// </summary>
public class StormOffScene : Scene
{
    public override double Duration => 8.2;
    public override double WinW => Pet.AwayW;
    public override State After => State.Away;
    const double DoorDx = Pet.DoorDx;

    // the crab disappears into the doorway: hidden left of the door's right edge
    public override Rect? CrabClip(Pet p, double cx) => new Rect(cx + DoorDx + 11, 0, p.WinW, Pet.WinH);

    double DoorOpacity => Seg(T, 1.8, 2.4);
    double DoorOpen => T < 5.4 ? Ease(Seg(T, 2.4, 3.0)) : 1 - Ease(Seg(T, 5.4, 5.9));
    double Shake => T > 5.9 && T < 6.5 ? Math.Sin(T * 60) * 2 * (1 - Seg(T, 5.9, 6.5)) : 0;

    public override void Pose(Pet p)
    {
        p.Dir = -1; p.Walking = false; p.Squash = 1; p.Icon = Icon.None; p.CrabDx = 0;
        p.Eyes = Eyes.Angry;
        p.CrabDy = -Sprites.BedTop;
        if (T < 1.5)
        {
            p.Icon = Icon.Anger;
            p.CrabDx = Math.Sin(T * 50);                                   // shaking with anger
        }
        else if (T < 2.1)
        {
            double u = Seg(T, 1.5, 2.1);                                   // hops off the bed
            p.CrabDy = -Sprites.BedTop * (1 - Ease(u)) - 8 * Math.Sin(Math.PI * u);
        }
        else
        {
            double u = Seg(T, 2.1, 5.4);                                   // stomps to the door
            p.CrabDy = 0;
            p.CrabDx = -118 * Ease(u);
            p.Walking = u > 0 && u < 1;
            p.Squash = 1 - 0.05 * Math.Abs(Math.Sin(T * 16));
            if (T < 5.4) p.Icon = Icon.Anger;
        }
    }

    public override void OnEnd(Pet p) { p.CrabDx = 0; p.CrabDy = 0; p.Squash = 1; }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        Sprites.Bed(c, cx, fy);
        Sprites.Nightstand(c, cx + 75, fy, true);
        using (c.PushOpacity(DoorOpacity)) Sprites.DoorBack(c, cx + DoorDx, fy);
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        Sprites.Blanket(c, cx, fy, 1 - Seg(T, 1.5, 2.0));                  // thrown off
        double x = cx + DoorDx;
        using (c.PushOpacity(DoorOpacity)) Sprites.DoorFront(c, x, fy, DoorOpen, Shake);
        if (T >= 6.4) Sprites.DoorSign(c, x, fy, -10 * (1 - Ease(Seg(T, 6.4, 6.9))));
        if (T >= 5.9 && T < 7.2)
            using (c.PushOpacity(1 - Seg(T, 6.6, 7.2)))
                c.DrawText(Sprites.Txt("БАМ!", 16, new SolidColorBrush(Color.FromRgb(255, 214, 80))),
                           new Point(x - 22, fy - 70 - Seg(T, 5.9, 7.2) * 6));
    }
}

/// <summary>The music is quieter: the door opens, Clawd comes back (a bit sulky) and goes back to bed.</summary>
public class ReturnScene : Scene
{
    readonly bool night;
    public ReturnScene(bool night) { this.night = night; }

    public override double Duration => 5.2;
    public override double WinW => Pet.AwayW;
    public override State After => night ? State.Sleeping : State.Idle;
    const double DoorDx = Pet.DoorDx;

    public override Rect? CrabClip(Pet p, double cx) => new Rect(cx + DoorDx + 11, 0, p.WinW, Pet.WinH);

    double DoorOpacity => 1 - Seg(T, 3.9, 4.6);
    double DoorOpen => T < 3.3 ? Ease(Seg(T, 0.3, 1.1)) : 1 - Ease(Seg(T, 3.3, 3.8));

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.Squash = 1; p.Icon = Icon.None;
        p.Eyes = T < 3.6 ? Eyes.Sad : night ? Eyes.Closed : Eyes.Open;
        double u = Seg(T, 1.1, 3.6);
        p.CrabDx = -118 * (1 - Ease(u));                                   // steps out of the doorway
        p.Walking = u > 0 && u < 1;
        p.CrabDy = 0;
        if (night && T >= 3.6)
        {
            double j = Seg(T, 3.6, 4.2);                                   // back onto the bed
            p.CrabDy = -Sprites.BedTop * Ease(j) - 8 * Math.Sin(Math.PI * j);
        }
    }

    public override void OnEnd(Pet p) { p.CrabDx = 0; }

    public override void RenderBack(DrawingContext c, Pet p, double cx, double fy)
    {
        using (c.PushOpacity(night ? 1 : 1 - Seg(T, 4.0, 5.0)))
        {
            Sprites.Bed(c, cx, fy);
            Sprites.Nightstand(c, cx + 75, fy, true);
        }
        using (c.PushOpacity(DoorOpacity)) Sprites.DoorBack(c, cx + DoorDx, fy);
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        double x = cx + DoorDx;
        if (night) Sprites.Blanket(c, cx, fy, Seg(T, 4.2, 5.0));
        using (c.PushOpacity(DoorOpacity)) Sprites.DoorFront(c, x, fy, DoorOpen, 0);
        using (c.PushOpacity(1 - Seg(T, 0.3, 0.8))) Sprites.DoorSign(c, x, fy, 0);   // he takes the sign down
    }
}

/// <summary>You named him Шлёпа: a puff of smoke, sparkles, and the crab turns into a cat (or back).</summary>
public class TransformScene : Scene
{
    readonly Form target;
    public TransformScene(Form target) { this.target = target; }

    public override double Duration => 2.8;
    public override double WinW => 160;

    public override void Pose(Pet p)
    {
        p.Dir = 1; p.Walking = false; p.CrabDx = 0; p.CrabDy = 0; p.Icon = Icon.None;
        p.Squash = T < 0.9 ? 1 + 0.12 * Math.Sin(T * 45) : 1;
        p.Eyes = T < 0.9 ? Eyes.Wide : Eyes.Happy;
        if (T >= 0.9 && p.Form != target) p.Form = target;   // swapped while hidden by the smoke
    }

    public override void RenderFront(DrawingContext c, Pet p, double cx, double fy)
    {
        double grow = Ease(Seg(T, 0.2, 0.9)), fade = 1 - Seg(T, 1.3, 2.4);
        if (T < 0.2 || fade <= 0) return;
        double gy = Pet.FeetY - Pet.Sink - 14;
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4 + T, r = 24 * grow;
            c.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(220 * fade), 235, 235, 240)), null,
                          new Point(cx + Math.Cos(a) * r, gy + Math.Sin(a) * r * 0.7), 11 * grow + 4, 9 * grow + 3);
        }
        c.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(230 * fade), 245, 245, 250)), null,
                      new Point(cx, gy), 20 * grow + 4, 16 * grow + 3);
        if (T > 0.9 && T < 2.2)
            for (int i = 0; i < 6; i++)
            {
                double a = i * 1.047 + T * 2, r = 22 + (T - 0.9) * 14;
                c.DrawRectangle(new SolidColorBrush(Color.FromRgb(255, 224, 120)), null,
                                new Rect(cx + Math.Cos(a) * r - 1.5, gy + Math.Sin(a) * r * 0.7 - 1.5, 3, 3));
            }
    }
}
