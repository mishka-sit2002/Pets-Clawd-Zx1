using Avalonia;
using Avalonia.Media;

namespace Clawd;

public enum GameModeState { Arriving, WatchingPopcorn, RefillingPopcorn, GettingSoda, RoomTour, Leaving, RepairingTv, WipingWindow, Sleeping }

enum Held { None, Bucket, Soda, Module }
enum BucketAt { Table, Hand, Counter }

/// <summary>Text drawn on top of the (possibly mirrored) room: a speech cloud or a plain label.</summary>
sealed record Ov(double X, double Y, string Text, bool Cloud);

sealed class Step
{
    public readonly double Dur;
    public readonly Action<double>? Run;
    public readonly Action? Begin, End;
    public Step(double dur, Action<double>? run, Action? begin, Action? end) { Dur = dur; Run = run; Begin = begin; End = end; }
}

/// <summary>
/// Clawd's room in cross-section (440x250) + the Game-mode state machine (GameModeState).
/// Everything is drawn from rectangles / pixel matrices; scripted sequences are queues of timed Steps.
/// </summary>
sealed partial class GameRoom
{
    public const double W = 440, H = 250, Floor = 232, SeatLift = 14;

    // ---- layout (room coordinates) ----
    const double FridgeL = 36, FridgeW = 32, FridgeT = 172;
    const double CounterL = 78, CounterR = 146, CounterTop = 206;
    const double MachineL = 82, MachineW = 32, MachineTop = 156, BucketSlotX = 132;
    const double ShelfL = 158, ShelfR = 242, ShelfY = 118, DoorX = 214;
    const double ChairX = 292;
    const double BedX = 210, MattX = 44, MattLift = 8;   // bed in the flat, mattress in the server room
    const double TableL = 322, TableR = 354, TableTop = 206, BucketTableX = 332, CanTableX = 347;
    const double ScrL = 362, ScrT = 140, ScrW = 74, ScrH = 56, PuckX = 399;
    const double FridgeStand = 18, MachineStand = 162, ShelfStand = 178, DoorStand = 190, ArriveX = 256;
    const double DrainPerSec = 1.0 / 210;   // an unrefilled bucket empties in about 3.5 minutes

    static IBrush B(byte r, byte g, byte b, byte a = 255) => Px.B(r, g, b, a);
    static void R(DrawingContext c, IBrush b, double x, double y, double w, double h) => Sprites.R(c, b, x, y, w, h);
    static void Outline(DrawingContext c, IBrush b, double x, double y, double w, double h)
    { R(c, b, x, y, w, 1); R(c, b, x, y + h - 1, w, 1); R(c, b, x, y, 1, h); R(c, b, x + w - 1, y, 1, h); }
    static double Seg(double t, double a, double b) => Math.Clamp((t - a) / (b - a), 0, 1);
    static double Ease(double x) => x * x * (3 - 2 * x);
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    static readonly IBrush Wood = B(139, 94, 60), WoodDark = B(110, 72, 44), Red = B(200, 40, 48), Amber = B(255, 170, 50),
        BulbOff = B(120, 90, 50), Cyan = B(80, 220, 255), Metal = B(70, 76, 92), ChairBack = B(160, 64, 78),
        ChairArm = B(130, 50, 64), ChairSeat = B(150, 58, 72), ChairTop = B(184, 88, 104), FridgeBody = B(196, 204, 220),
        FridgeDoor = B(214, 221, 236), FridgeIn = B(255, 252, 225), ShelfLine = B(190, 196, 208), Handle = B(120, 126, 140),
        Glass = B(255, 248, 220, 70), WebC = B(215, 222, 240, 150);
    static readonly IBrush[] Bricks = { B(255, 110, 150), B(255, 170, 50), B(120, 220, 140) };
    static readonly (double X, double Y)[] WebEnd = { (50, 14), (44, 30), (32, 44), (14, 54) };

    // ---- public state ----
    public GameModeState Mode = GameModeState.Arriving;
    public bool LeaveDone;
    public readonly List<Ov> Overlays = new();

    // ---- actor ----
    double ax = ArriveX, sit, hop, squash = 1, size = 1, gaze, wave = -1, anim, puff;
    int dir = 1;
    bool walking, seated;
    Eyes eyes = Eyes.Open;
    string? say;
    Held held = Held.None;
    BucketAt bucketAt = BucketAt.Table;

    // ---- props ----
    double popcorn = 1;                       // 0..1 how full the bucket is
    bool sodaOnTable;
    double fridgeOpen, doorOpen, bugDrop, figBob, screenFlash, kernelT, sipT, cookT;
    bool cooking;

    // ---- scripting ----
    readonly Queue<Step> q = new();
    Step? cur;
    double curT, planX, eatT = 3, sodaT = 80, reactT = 8, blinkT = 3, blinking;
    bool pendingTour;
    readonly Random rnd = new();

    // ---- night ----
    double bedA, bedLift, blanket, loudT, vol0, complainT, srvShake;
    bool bedInServer, complaining, deepSleep;   // deepSleep: stormed off, nothing but the morning wakes him
    static bool Night => GameMode.Night;

    double FeetY => Floor - sit * SeatLift - hop - bedLift + ay;
    double BallX => 0.5 + 0.5 * Math.Sin(anim * 1.9);
    double BallY => 0.5 + 0.5 * Math.Sin(anim * 2.7);

    void Add(double dur, Action<double>? run = null, Action? begin = null, Action? end = null) =>
        q.Enqueue(new Step(dur, run, begin, end));

    void Walk(double to, double speed = 70)
    {
        double from = planX; planX = to;
        double d = Math.Abs(to - from);
        if (d < 1) return;
        Add(d / speed, p => ax = Lerp(from, to, p), () => { dir = to > from ? 1 : -1; walking = true; }, () => { walking = false; dir = 1; });
    }

    void Stand() => Add(0.5, p => { sit = 1 - Ease(p); hop = Math.Sin(p * Math.PI) * 3; }, null, () => { sit = 0; seated = false; });

    void Sit(Action? end = null) =>
        Add(0.6, p => { sit = Ease(p); hop = Math.Sin(p * Math.PI) * 5; }, () => { seated = true; dir = 1; }, () => { sit = 1; end?.Invoke(); });

    void Talk(double dur, string text, Action<double>? run = null, Action? end = null, Action? begin = null) =>
        Add(dur, run, () => { say = text; begin?.Invoke(); }, () => { say = null; end?.Invoke(); });

    // =====================================================================================
    //  State machine
    // =====================================================================================
    public void Arrive()
    {
        Abort(); Mode = GameModeState.Arriving; LeaveDone = false;
        ax = ArriveX; sit = 0; seated = false; size = 0; popcorn = 1; sodaOnTable = false; planX = ax;
        eventT = 90 + rnd.NextDouble() * 90;
        bedA = bedLift = blanket = loudT = 0; bedInServer = complaining = deepSleep = false;
        Add(0.5, p => { size = Math.Max(0.05, Ease(p)); eyes = Eyes.Wide; hop = Math.Sin(p * Math.PI) * 6; }, () => puff = 0.6, () => size = 1);
        Add(2.6, p => { wave = anim; eyes = Eyes.Happy; }, () => say = "Привет!", () => say = null);
        Walk(ChairX);
        Sit(() => { Mode = GameModeState.WatchingPopcorn; eatT = 4; sodaT = 70 + rnd.NextDouble() * 60; });
    }

    public void Leave()
    {
        pendingTour = false;
        bool wasSeated = seated && sit > 0.9;
        Abort();
        bedA = bedLift = blanket = 0; bedInServer = complaining = deepSleep = false;   // wakes up and folds the bed away
        Mode = GameModeState.Leaving; LeaveDone = false;
        if (wasSeated) { planX = ax; Stand(); }
        else { ax = ArriveX; sit = 0; seated = false; planX = ax; }
        Add(2.4, p => { wave = anim; eyes = Eyes.Happy; }, () => say = "Пока!", () => say = null);
        Add(0.5, p => size = Math.Max(0.05, 1 - Ease(p)), () => puff = 0.6, () => { size = 0; LeaveDone = true; });
    }

    public void RequestTour() { if (Mode != GameModeState.Leaving) pendingTour = true; }

    public void CancelTour()
    {
        pendingTour = false;
        if (Mode != GameModeState.RoomTour) return;
        Abort();
        ax = ChairX; sit = 1; seated = true; Mode = GameModeState.WatchingPopcorn;
    }

    void Abort()
    {
        q.Clear(); cur = null;
        held = Held.None; bucketAt = BucketAt.Table;
        fridgeOpen = doorOpen = bugDrop = figBob = screenFlash = 0;
        cooking = false; say = null; walking = false; dir = 1;
        rackOpen = stairDoor = srvDoor = fog = srvShake = 0; ay = 0; tv = 0; slotEmpty = false;
    }

    public void Update(double dt)
    {
        anim += dt;
        if (puff > 0) puff = Math.Max(0, puff - dt);
        if (cooking) cookT += dt;

        // transient pose, steps override it below
        eyes = Eyes.Open; squash = 1; wave = -1; hop = 0; kernelT = 0; sipT = 0; screenFlash = 0;
        gaze = seated ? LookGaze() : 0;

        // bedtime while he is down in the server room: lie down right there on a mattress
        if (Night && Mode == GameModeState.RepairingTv && ax > 4 && ay >= ServerFloor - Floor - 0.5) BeginSleep(true);

        if (cur == null && q.Count > 0) { cur = q.Dequeue(); curT = 0; cur.Begin?.Invoke(); }
        if (cur != null)
        {
            curT += dt;
            double p = cur.Dur <= 0 ? 1 : Math.Min(1, curT / cur.Dur);
            cur.Run?.Invoke(p);
            if (p >= 1) { var done = cur; cur = null; done.End?.Invoke(); }
        }
        else Idle(dt);

        blinkT -= dt;
        if (blinkT <= 0) { blinkT = 2.5 + rnd.NextDouble() * 4; blinking = 0.12; }
        if (blinking > 0) { blinking -= dt; if (eyes == Eyes.Open) eyes = Eyes.Closed; }
    }

    // ---- WatchingPopcorn ----
    void Idle(double dt)
    {
        if (Mode == GameModeState.Sleeping) { SleepTick(dt); return; }
        if (Mode != GameModeState.WatchingPopcorn) return;
        popcorn = Math.Max(0, popcorn - dt * DrainPerSec);
        eatT -= dt; sodaT -= dt; reactT -= dt;
        if (pendingTour) { pendingTour = false; BeginTour(); return; }
        if (Night) { BeginSleep(false); return; }
        if (popcorn <= 0.03) { BeginRefill(); return; }
        if (sodaT <= 0) { BeginSoda(); return; }
        eventT -= dt;
        if (eventT <= 0) { eventT = 150 + rnd.NextDouble() * 150; if (Kind == HouseKind.Cinema) BeginRepair(); else BeginWipe(); return; }
        if (eatT <= 0 && popcorn > 0.1) { eatT = 5 + rnd.NextDouble() * 5; Eat(); return; }
        if (reactT <= 0)   // something happens on the screen
        {
            reactT = 6 + rnd.NextDouble() * 8;
            Add(1.2, p => { eyes = Eyes.Wide; screenFlash = Math.Sin(p * Math.PI); hop = Math.Abs(Math.Sin(p * Math.PI * 2)) * 3; });
        }
    }

    void Eat() => Add(1.6, p =>
    {
        kernelT = Seg(p, 0.1, 0.55);
        if (p > 0.55) { eyes = Eyes.Happy; squash = 1 + 0.05 * Math.Sin((p - 0.55) * 40); }
    }, null, () => popcorn = Math.Max(0, popcorn - 0.03));

    // ---- RefillingPopcorn ----
    void BeginRefill()
    {
        Mode = GameModeState.RefillingPopcorn; planX = ax;
        Add(1.1, p => eyes = Eyes.Sad, () => say = "Попкорн кончился!", () => say = null);
        Stand();
        Add(0.7, null, null, () => { held = Held.Bucket; bucketAt = BucketAt.Hand; });
        Walk(MachineStand);
        Add(0.7, null, null, () => { held = Held.None; bucketAt = BucketAt.Counter; popcorn = 0; });
        Add(4.5, p => { popcorn = Ease(p); eyes = p > 0.7 ? Eyes.Happy : Eyes.Open; squash = 1 + 0.03 * Math.Sin(anim * 14); },
            () => { cooking = true; cookT = 0; }, () => { cooking = false; popcorn = 1; });
        Add(1.0, p => eyes = Eyes.Happy, () => say = "Готово!", () => say = null);
        Add(0.6, null, null, () => { held = Held.Bucket; bucketAt = BucketAt.Hand; });
        Walk(ChairX);
        Add(0.6, null, null, () => { held = Held.None; bucketAt = BucketAt.Table; });
        Sit(() => { Mode = GameModeState.WatchingPopcorn; eatT = 4; });
    }

    // ---- GettingSoda ----
    void BeginSoda()
    {
        Mode = GameModeState.GettingSoda; planX = ax;
        sodaT = 80 + rnd.NextDouble() * 70;
        Add(0.9, p => eyes = Eyes.Wide, () => say = "Хочу газировки!", () => say = null);
        Stand();
        Walk(FridgeStand);
        Add(0.8, p => fridgeOpen = Ease(p));
        Add(0.8, p => eyes = Eyes.Happy, null, () => { held = Held.Soda; sodaOnTable = false; });
        Add(0.7, p => fridgeOpen = 1 - Ease(p), null, () => fridgeOpen = 0);
        Walk(ChairX);
        Sit();
        Add(2.2, p =>
        {
            sipT = Math.Sin(Seg(p, 0.1, 0.9) * Math.PI);
            eyes = sipT > 0.5 ? Eyes.Happy : Eyes.Open;
            squash = 1 + 0.03 * Math.Sin(Seg(p, 0.4, 0.8) * 25);   // gulp
        }, null, () => { held = Held.None; sodaOnTable = true; Mode = GameModeState.WatchingPopcorn; eatT = 4; });
    }

    // ---- Sleeping ----
    void BeginSleep(bool server)
    {
        double keepAy = ay;
        Abort();
        if (server) ay = keepAy;   // mid-repair in the server room: stay down there
        Mode = GameModeState.Sleeping; bedInServer = server; planX = ax;
        complaining = deepSleep = false; loudT = 0;
        double lift = server ? MattLift : Sprites.BedTop, to = server ? MattX : BedX, side = to + 56;

        if (server)
        {
            Add(1.4, p => eyes = Eyes.Sad, () => say = "Поздно уже...\nлягу прямо тут", () => say = null);
            Walk(side);
            Add(1.0, p => { bedA = Ease(p); eyes = Eyes.Happy; }, () => say = "Раскладываю матрас", () => say = null);
        }
        else
        {
            Add(1.4, p => { eyes = Eyes.Sleepy; squash = 1 + 0.08 * Math.Sin(Math.PI * p); }, () => say = "Уже ночь...\nпора спать", () => say = null);
            Stand();
            Add(1.0, p => { bedA = Ease(p); eyes = Eyes.Happy; }, () => say = "Достаю кровать", () => say = null);
            Walk(side);
        }
        Add(0.9, p => { double e = Ease(p); ax = Lerp(side, to, e); bedLift = lift * e; hop = Math.Sin(p * Math.PI) * 8; },
            () => dir = -1, () => { ax = to; bedLift = lift; dir = 1; planX = to; });
        Add(1.0, p => { blanket = Ease(p); eyes = Eyes.Closed; }, null, () => blanket = 1);
    }

    void SleepTick(double dt)
    {
        if (!Night || (pendingTour && !deepSleep)) { BeginWake(); return; }
        eyes = Eyes.Closed; squash = 1 + 0.015 * Math.Sin(anim * 2);   // breathing
        if (bedInServer) { say = null; return; }   // the server room is soundproof: nothing wakes him until morning

        // music too loud in the flat: asks nicely, then shouts, then storms off to sleep in the server room
        if (!complaining)
        {
            loudT = Audio.IsLoud ? loudT + dt : 0;
            if (loudT >= 5) { complaining = true; complainT = 0; vol0 = Audio.Volume; loudT = 0; }
        }
        else
        {
            complainT += dt;
            if (Audio.IsQuiet(vol0)) complaining = false;
            else if (complainT >= 25) { BeginStorm(); return; }
        }
        if (complaining)
        {
            bool angry = complainT > 12;
            eyes = angry ? Eyes.Angry : Eyes.Sad;
            say = angry ? "ПОТИШЕ!!" : "Потише,\nпожалуйста…";
        }
        else say = null;
    }

    /// <summary>You ignored him twice: he gets up, goes down to the server room, slams the door and sleeps there.</summary>
    void BeginStorm()
    {
        complaining = false; loudT = 0; planX = ax; deepSleep = true;
        double from = ax, off = BedX + 56, side = MattX + 56, lift = MattLift;
        Add(1.5, p => { eyes = Eyes.Angry; blanket = 1 - Ease(Seg(p, 0, 0.4)); }, () => say = "Всё! Ухожу спать\nв серверную!", () => { say = null; blanket = 0; });
        Add(0.8, p => { double e = Ease(p); ax = Lerp(from, off, e); bedLift = Sprites.BedTop * (1 - e); hop = Math.Sin(p * Math.PI) * 8; eyes = Eyes.Angry; },
            () => dir = 1, () => { ax = off; bedLift = 0; });
        Add(0.9, p => { bedA = 1 - Ease(p); eyes = Eyes.Angry; }, null, () => bedA = 0);
        planX = off;
        Walk(8);
        Add(0.6, p => stairDoor = Ease(p));
        Walk(-12);
        Add(0.4, p => stairDoor = 1 - Ease(p));
        Stairs(true);
        Add(0.6, p => srvDoor = Ease(p));
        Walk(6);
        Add(0.2, p => srvDoor = 1 - Ease(p), null, () => bedInServer = true);          // slams it behind him
        Add(0.7, p => { srvShake = Math.Sin(p * 60) * 2 * (1 - p); eyes = Eyes.Angry; }, () => say = "*БАХ!*", () => { say = null; srvShake = 0; });
        Walk(side);
        Add(1.0, p => { bedA = Ease(p); eyes = Eyes.Happy; }, () => say = "Раскладываю матрас", () => say = null);
        Add(0.9, p => { double e = Ease(p); ax = Lerp(side, MattX, e); bedLift = lift * e; hop = Math.Sin(p * Math.PI) * 8; },
            () => dir = -1, () => { ax = MattX; bedLift = lift; dir = 1; planX = MattX; });
        Add(1.0, p => { blanket = Ease(p); eyes = Eyes.Closed; }, null, () => blanket = 1);
    }

    void BeginWake()
    {
        complaining = false; say = null; planX = ax;
        bool server = bedInServer;
        double from = ax, lift = bedLift, off = (server ? MattX : BedX) + 56;

        Add(1.4, p => { eyes = p < 0.5 ? Eyes.Closed : Eyes.Open; squash = 1 + 0.1 * Math.Sin(Math.PI * Seg(p, 0.2, 0.8)); blanket = 1 - Ease(Seg(p, 0.5, 1)); },
            () => say = "Доброе утро!", () => { say = null; blanket = 0; });
        Add(0.8, p => { double e = Ease(p); ax = Lerp(from, off, e); bedLift = lift * (1 - e); hop = Math.Sin(p * Math.PI) * 8; },
            () => dir = 1, () => { ax = off; bedLift = 0; });
        Add(0.9, p => bedA = 1 - Ease(p), null, () => bedA = 0);   // the bed is folded away
        planX = off;
        if (server)   // back up through the server door and the stairwell
        {
            Add(0.6, p => srvDoor = Ease(p));
            Walk(-12);
            Add(0.4, p => srvDoor = 1 - Ease(p));
            Stairs(false);
            Add(0.6, p => stairDoor = Ease(p));
            Walk(8);
            Add(0.5, p => stairDoor = 1 - Ease(p));
        }
        Walk(ChairX);
        Sit(() => { Mode = GameModeState.WatchingPopcorn; eatT = 4; sodaT = 70 + rnd.NextDouble() * 60; });
    }

    // ---- RoomTour ----
    void BeginTour()
    {
        Mode = GameModeState.RoomTour; planX = ax;
        Stand();

        // 1. armchair
        Talk(3.2, "Моё кресло.\nМесто №1 в зале!", p =>
        {
            sit = Ease(Seg(p, 0.1, 0.3)) - Ease(Seg(p, 0.85, 1));
            seated = sit > 0.5;
            if (p > 0.3 && p < 0.8) { squash = 1 + 0.05 * Math.Sin((p - 0.3) * 50); eyes = Eyes.Happy; }
        }, () => { sit = 0; seated = false; });

        // 2. table
        Walk(306);
        Talk(3.4, "Столик: слева попкорн,\nсправа газировка", p =>
        {
            kernelT = Seg(p, 0.15, 0.5);
            if (p > 0.5) { eyes = Eyes.Happy; squash = 1 + 0.05 * Math.Sin((p - 0.5) * 40); }
        });

        // 3. virtual screen
        Walk(356);
        Talk(3.6, Kind == HouseKind.Window ? "Окно во всю стену.\nВижу твою игру!" : "Виртуальный экран.\nСмотрю, как ты играешь!", p =>
        {
            gaze = 1; eyes = p < 0.5 ? Eyes.Wide : Eyes.Happy;
            screenFlash = 0.5 + 0.5 * Math.Sin(p * 18);
            hop = Math.Abs(Math.Sin(p * Math.PI * 2)) * 3;
        });

        // 4. popcorn machine
        Walk(MachineStand);
        Talk(3.8, "Попкорн-машина.\nГотовит свежий попкорн!", p => eyes = Eyes.Happy,
            () => cooking = false, () => { cooking = true; cookT = 0; });

        // 5. mini fridge
        Walk(FridgeStand);
        Talk(3.6, "Мини-холодильник\nс газировкой", p =>
        {
            fridgeOpen = Ease(Seg(p, 0.15, 0.35)) * (1 - Ease(Seg(p, 0.8, 0.95)));
            held = p > 0.4 && p < 0.8 ? Held.Soda : Held.None;
            eyes = Eyes.Happy;
        }, () => { fridgeOpen = 0; held = Held.None; });

        // 6. cobweb with Zex Bug Jr.
        Talk(3.8, "Паутинка в углу —\nдом Зекс Бага Младшего", p =>
        {
            bugDrop = Math.Sin(Seg(p, 0.1, 0.9) * Math.PI);
            eyes = p < 0.3 ? Eyes.Wide : Eyes.Happy;
            if (p > 0.3 && p < 0.9) wave = anim;
        }, () => bugDrop = 0);

        // 7. shelf of mascots
        Walk(ShelfStand);
        Talk(3.8, "Полка талисманов.\nМини-я в новогоднем колпаке!", p =>
        {
            figBob = p; eyes = Eyes.Happy;
            hop = Math.Abs(Math.Sin(p * Math.PI * 3)) * 4;
        }, () => figBob = 0);

        // 8. door to the tray
        Walk(DoorStand);
        Talk(3.6, "Эта дверь ведёт\nпрямо в системный трей", p =>
        {
            doorOpen = Ease(Seg(p, 0.15, 0.35)) * (1 - Ease(Seg(p, 0.8, 0.95)));
            eyes = Eyes.Wide;
        }, () => doorOpen = 0);

        // back to the armchair
        Walk(ChairX);
        Sit();
        Talk(2.8, "Тур окончен!\nПриятной игры!", p => { wave = anim; eyes = Eyes.Happy; }, () =>
        {
            if (Kind == HouseKind.Window) GameConfig.Current.HasSeenWindowTour = true; else GameConfig.Current.HasSeenRoomTour = true;
            GameConfig.Save();
            Mode = GameModeState.WatchingPopcorn; eatT = 4;
        });
    }

    // =====================================================================================
    //  Drawing
    // =====================================================================================
    void DrawApartment(DrawingContext c, byte wallA)
    {
        DrawShell(c, wallA);
        DrawWeb(c);
        DrawShelf(c);
        DrawDoor(c);
        DrawFridge(c);
        DrawKitchen(c);
        DrawViewing(c);
        DrawBed(c);
        DrawActor(c);
        DrawForeground(c);
    }

    void DrawShell(DrawingContext c, byte a)
    {
        DrawWall(c, a);
        var stripe = B(46, 52, 76, a);
        for (double sx = 12; sx < W; sx += 24)
        {
            if (Kind == HouseKind.Window && sx + 9 > HoleL && sx < HoleL + HoleW)   // keep the pane clear
            {
                R(c, stripe, sx, 14, 9, HoleT - 14);
                R(c, stripe, sx, HoleT + HoleH, 9, Floor - 6 - HoleT - HoleH);
                continue;
            }
            R(c, stripe, sx, 14, 9, Floor - 20);
        }
        R(c, B(78, 56, 40), 0, 0, W, 12);
        R(c, B(52, 38, 28), 0, 12, W, 2);
        byte a2 = (byte)Math.Min(255, a + 40);
        R(c, B(70, 50, 36, a2), 0, Floor - 6, W, 6);
        R(c, B(112, 76, 48, a2), 0, Floor, W, H - Floor);
        var plank = B(80, 54, 34, a2);
        for (double fx = 36; fx < W; fx += 52) R(c, plank, fx, Floor, 2, H - Floor);
        R(c, plank, 0, Floor + 9, W, 1);
    }

    void DrawWeb(DrawingContext c)
    {
        const double ox = 0, oy = 14;
        foreach (var e in WebEnd) Px.Line(c, WebC, ox, oy, e.X, e.Y, 1.2);
        foreach (double f in new[] { 0.4, 0.7, 1.0 })
            for (int i = 0; i + 1 < WebEnd.Length; i++)
            {
                var a = WebEnd[i]; var b = WebEnd[i + 1];
                Px.Line(c, WebC, ox + (a.X - ox) * f, oy + (a.Y - oy) * f, ox + (b.X - ox) * f, oy + (b.Y - oy) * f, 1.2);
            }
        // Zex Bug Jr. hangs on a thread and bobs; he climbs down when Clawd talks about him
        double by = 28 + 5 * Math.Sin(anim * 1.5) + bugDrop * 100;
        Px.Line(c, WebC, 22, oy, 22, by, 1.2);
        Px.Bug(c, 22, by, 2);
    }

    double BobOffset(int i)
    {
        if (figBob <= 0) return 0;
        int idx = 3 - Math.Min(3, (int)(figBob * 4));
        return idx == i ? Math.Sin(figBob * 4 % 1 * Math.PI) * 5 : 0;
    }

    void Scaled(DrawingContext c, double px, double py, double k, Action draw)
    {
        var m = Matrix.CreateTranslation(-px, -py) * Matrix.CreateScale(k, k) * Matrix.CreateTranslation(px, py);
        using (c.PushTransform(m)) draw();
    }

    void DrawShelf(DrawingContext c)
    {
        R(c, Wood, ShelfL, ShelfY, ShelfR - ShelfL, 4);
        R(c, WoodDark, ShelfL, ShelfY + 4, ShelfR - ShelfL, 1);
        R(c, WoodDark, ShelfL + 8, ShelfY + 5, 3, 9);
        R(c, WoodDark, ShelfR - 12, ShelfY + 5, 3, 9);
        double[] fx = { 172, 192, 212, 232 };
        for (int i = 0; i < 4; i++)
        {
            double x = fx[i], by = ShelfY - BobOffset(i);
            if (i == 0)   // mini Clawd in a New Year hat with a pompom
            {
                Sprites.Crab(c, x, by, 1, 1, Eyes.Happy, false, anim, -1, 0, 0.55);
                Px.Hat(c, x, by - 32 * 0.55 + 2, 1.3);
            }
            else
            {
                int kind = i - 1;
                Scaled(c, x, by, 0.7, () => Sprites.Mascot(c, kind, x, by, 0, 0, false, anim));
            }
        }
    }

    void DrawDoor(DrawingContext c)
    {
        Sprites.DoorBack(c, DoorX, Floor);
        if (doorOpen > 0.25)   // the system tray glows behind the door
        {
            byte a = (byte)(255 * Math.Clamp((doorOpen - 0.25) / 0.5, 0, 1));
            R(c, B(90, 190, 255, a), DoorX - 8, Floor - 30, 5, 5);
            R(c, B(80, 220, 120, a), DoorX - 1, Floor - 30, 5, 5);
            R(c, B(255, 170, 50, a), DoorX + 6, Floor - 30, 4, 5);
            R(c, B(250, 250, 252, a), DoorX - 8, Floor - 20, 18, 3);
        }
        Sprites.DoorFront(c, DoorX, Floor, doorOpen, 0);
        R(c, Px.Orange, DoorX - 17, Floor - 64, 34, 11);
        Outline(c, Px.Ink, DoorX - 17, Floor - 64, 34, 11);
        Overlays.Add(new Ov(DoorX, Floor - 64, "ТРЕЙ", false));
    }

    void DrawFridge(DrawingContext c)
    {
        double l = FridgeL, t = FridgeT, w = FridgeW, h = Floor - FridgeT;
        R(c, FridgeBody, l, t, w, h);
        if (fridgeOpen > 0.02)
        {
            R(c, FridgeIn, l + 2, t + 20, w - 4, h - 22);
            R(c, ShelfLine, l + 2, t + 36, w - 4, 2);
            R(c, ShelfLine, l + 2, t + 52, w - 4, 2);
            for (int i = 0; i < 3; i++)
            {
                Px.Can(c, l + 8 + i * 8.5, t + 36, 1.1);
                Px.Can(c, l + 8 + i * 8.5, t + 52, 1.1);
            }
        }
        double lw = (w - 4) * (1 - fridgeOpen), lx = l + w - 2 - lw;   // hinged on the right edge
        if (lw > 0.5) R(c, FridgeDoor, lx, t + 20, lw, h - 22);
        if (lw > 8) R(c, Handle, lx + 3, t + 28, 2, 12);
        R(c, FridgeDoor, l + 2, t + 2, w - 4, 17);
        R(c, Handle, l + w - 7, t + 6, 2, 8);
        R(c, Px.Ink, l + 2, t + 19, w - 4, 1);
        Outline(c, Px.Ink, l, t, w, h);
    }

    void DrawKitchen(DrawingContext c)
    {
        // counter
        R(c, Wood, CounterL, CounterTop, CounterR - CounterL, Floor - CounterTop);
        R(c, WoodDark, CounterL - 1, CounterTop, CounterR - CounterL + 2, 3);
        R(c, WoodDark, CounterL + 6, CounterTop + 8, 26, 16);
        R(c, WoodDark, CounterL + 36, CounterTop + 8, 26, 16);
        R(c, Px.Gold, CounterL + 30, CounterTop + 14, 2, 4);
        R(c, Px.Gold, CounterL + 36, CounterTop + 14, 2, 4);

        // popcorn machine: red cabinet, glass chamber, striped roof with bulbs
        R(c, Red, MachineL, 190, MachineW, 16);
        R(c, Px.Gold, MachineL, 196, MachineW, 3);
        R(c, Glass, MachineL + 2, 162, MachineW - 4, 28);
        Outline(c, Metal, MachineL + 1, 161, MachineW - 2, 30);
        R(c, Red, MachineL - 3, MachineTop, MachineW + 6, 6);
        for (int i = 0; i < 5; i++) R(c, Px.White, MachineL - 3 + i * 8 + 3, MachineTop, 4, 6);
        for (int i = 0; i < 5; i++)
            R(c, cooking && ((int)(anim * 6) + i) % 2 == 0 ? Amber : BulbOff, MachineL + i * 7 + 1, MachineTop - 3, 3, 3);
        if (cooking)
        {
            for (int i = 0; i < 9; i++)   // kernels popping in the glass
            {
                double ph = (cookT * 2.2 + i * 0.29) % 1;
                Px.Kernel(c, MachineL + 4 + (i * 7) % 22 + Math.Sin(cookT * 9 + i) * 1.5, 186 - Math.Sin(ph * Math.PI) * 22, i);
            }
            for (int i = 0; i < 3; i++)   // falling into the bucket
            {
                double ph = (cookT * 3 + i / 3.0) % 1;
                Px.Kernel(c, BucketSlotX - 6, 196 + ph * 10, i);
            }
            for (int i = 0; i < 2; i++)   // steam
                R(c, B(255, 255, 255, 110), MachineL + 10 + i * 10 + Math.Sin(anim * 4 + i) * 2, 148 - (anim * 14 + i * 6) % 14, 2, 4);
        }
        else
            for (int i = 0; i < 5; i++) Px.Kernel(c, MachineL + 5 + i * 5, 185, i);   // kernels waiting
        Px.Line(c, Px.Gold, MachineL + MachineW, 194, BucketSlotX - 6, 198, 3);        // chute

        if (bucketAt == BucketAt.Counter) Px.Bucket(c, BucketSlotX, CounterTop, popcorn);
    }

    void DrawViewing(DrawingContext c)
    {
        // armchair (the front of the seat is drawn after Clawd)
        R(c, ChairBack, ChairX - 22, 176, 44, 46);
        R(c, ChairTop, ChairX - 22, 176, 44, 5);
        R(c, ChairArm, ChairX - 28, 198, 7, 34);
        R(c, ChairArm, ChairX + 21, 198, 7, 34);
        R(c, ChairTop, ChairX - 28, 198, 7, 3);
        R(c, ChairTop, ChairX + 21, 198, 7, 3);

        // table with popcorn and soda
        R(c, Wood, TableL, TableTop, TableR - TableL, 4);
        R(c, WoodDark, TableL + 3, TableTop + 4, 3, Floor - TableTop - 4);
        R(c, WoodDark, TableR - 6, TableTop + 4, 3, Floor - TableTop - 4);
        if (bucketAt == BucketAt.Table) Px.Bucket(c, BucketTableX, TableTop, popcorn);
        if (sodaOnTable && held != Held.Soda) Px.Can(c, CanTableX, TableTop, 1.5);

        if (Kind == HouseKind.Window) DrawWindow(c); else DrawScreen(c);
    }

    void DrawScreen(DrawingContext c)
    {
        var beam = B(80, 220, 255, 45);
        foreach (double bx in new[] { PuckX - 14, PuckX, PuckX + 14 }) R(c, beam, bx - 1, ScrT + ScrH, 2, Floor - 6 - ScrT - ScrH);
        R(c, Metal, PuckX - 20, Floor - 6, 40, 6);
        R(c, Cyan, PuckX - 3, Floor - 5, 6, 2);
        R(c, B(80, 220, 255, 40), ScrL - 3, ScrT - 3, ScrW + 6, ScrH + 6);
        R(c, B(10, 18, 34, 215), ScrL, ScrT, ScrW, ScrH);
        if (tv == 2 || (tv == 1 && (int)(anim * 12) % 3 == 0))   // broken: static noise
        {
            int fr = (int)(anim * 20);
            for (int i = 0; i < 34; i++)
                R(c, i % 3 == 0 ? Px.White : B(150, 160, 180), ScrL + (i * 37 + fr * 13) % (int)(ScrW - 6), ScrT + (i * 23 + fr * 7) % (int)(ScrH - 4), 5, 2);
            Outline(c, Red, ScrL - 1, ScrT - 1, ScrW + 2, ScrH + 2);
            if (tv == 2) Overlays.Add(new Ov(ScrL + ScrW / 2, ScrT + ScrH / 2 - 4, "НЕТ СИГНАЛА", false));
            return;
        }
        var scan = B(80, 220, 255, 18);
        for (double sy = ScrT + 2; sy < ScrT + ScrH; sy += 4) R(c, scan, ScrL, sy, ScrW, 1);
        for (int i = 0; i < 6; i++) R(c, Bricks[i % Bricks.Length], ScrL + 4 + i * 11.5, ScrT + 5, 9, 4);
        double ballX = ScrL + 8 + BallX * (ScrW - 20), ballY = ScrT + 14 + BallY * (ScrH - 26);
        R(c, Px.White, ballX, ballY, 4, 4);
        R(c, Cyan, ScrL + 3, Math.Clamp(ballY - 6, ScrT + 12, ScrT + ScrH - 16), 3, 12);
        R(c, Amber, ScrL + ScrW - 6, Math.Clamp(ballY - 6 + Math.Sin(anim) * 5, ScrT + 12, ScrT + ScrH - 16), 3, 12);
        if (screenFlash > 0.01) R(c, B(255, 255, 255, (byte)(90 * Math.Clamp(screenFlash, 0, 1))), ScrL, ScrT, ScrW, ScrH);
        Outline(c, Cyan, ScrL - 1, ScrT - 1, ScrW + 2, ScrH + 2);
    }

    void DrawBed(DrawingContext c)
    {
        if (bedA < 0.01 || bedInServer) return;
        using (c.PushOpacity(bedA)) Sprites.Bed(c, BedX, Floor);
    }

    void DrawActor(DrawingContext c)
    {
        if (puff > 0) Px.Puff(c, ax, FeetY - 14, 1 - puff / 0.6);
        if (size < 0.03) return;
        Sprites.Crab(c, ax, FeetY, dir, squash, eyes, walking, anim, wave, gaze, size);
        if (say != null) Overlays.Add(new Ov(ax, FeetY - 32 * size - 6, say, true));
        if (Mode == GameModeState.Sleeping && blanket > 0.9 && !complaining)   // zzz (as overlays, so they are never mirrored)
            for (int i = 0; i < 3; i++)
            {
                double t = (anim * 0.5 + i * 0.33) % 1;
                Overlays.Add(new Ov(ax + 14 + i * 6 + t * 10, FeetY - 34 - t * 26, "z", false));
            }
    }

    void DrawForeground(DrawingContext c)
    {
        double fy = FeetY;
        if (sit > 0.5)   // the front of the seat hides his legs
        {
            R(c, ChairSeat, ChairX - 22, Floor - 22, 44, 22);
            R(c, ChairTop, ChairX - 22, Floor - 22, 44, 4);
        }
        if (size < 0.03) return;

        if (blanket > 0.01)
        {
            if (!bedInServer) Sprites.Blanket(c, BedX, Floor, blanket);
            else
            {
                double top = ServerFloor - MattLift - 8 * blanket;
                R(c, B(66, 120, 200), MattX - 24, top, 64, ServerFloor - 3 - top);
                R(c, B(110, 160, 230), MattX - 24, top, 64, 2);
            }
        }

        if (held == Held.Bucket) Px.Bucket(c, ax, fy - 3, popcorn);
        if (held == Held.Module) Px.Module(c, ax, fy - 3, modBroken);
        if (held == Held.Soda)
        {
            double cx = Lerp(ax + 21, ax + 9, sipT), cy = Lerp(fy - 14, fy - 27, sipT);
            var m = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(-0.45 * sipT) * Matrix.CreateTranslation(cx, cy);
            using (c.PushTransform(m)) Px.Can(c, cx, cy, 1.5);
        }
        if (kernelT > 0 && kernelT < 1)   // a kernel flies from the bucket to his mouth
        {
            double kx = Lerp(BucketTableX, ax + 2, kernelT), ky = Lerp(TableTop - 18, fy - 14, kernelT) - Math.Sin(kernelT * Math.PI) * 12;
            Px.Kernel(c, kx, ky, 0);
        }
    }
}
