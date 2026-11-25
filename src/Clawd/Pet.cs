using Avalonia;
using Avalonia.Media;

namespace Clawd;

public enum State { Idle, Walk, Hungry, Scene, Sleeping, Away, InGame }
public enum Form { Clawd, Shlepa }
public enum Icon { None, Heart, Exclaim, Question, Anger, Note }

/// <summary>All of Clawd's logic: state machine (walk / hungry / sleep) + scene playback.</summary>
public class Pet
{
    public const double WinH = 110, FeetY = 104, Sink = 6;
    public const double NormalW = 140, SleepW = 180, AwayW = 260, DoorDx = -100;

    // --- settings (move to a config file later) ---
    public int SleepHour = 22, WakeHour = 8;
    public double HungerSeconds = 20 * 60;   // how long until he asks for tokens
    public double FlowerRollSeconds = 1800;   // easter egg: every 30 min of daytime...
    public double FlowerChance = 0.10;        // ...a 10% roll => roughly once per ~5 hours
    public bool RequestFlower;                // test hook (context menu)
    public bool PlayIntro;                    // first launch: Clawd climbs out of a box
    public bool RequestBox;                   // test hook (context menu)
    public bool RequestStorm;                 // test hook (context menu)
    public bool RequestZx;                    // test hook (context menu)
    public double MouseIdle;                  // seconds without mouse movement, set by the window
    public bool? ForceNight;                  // test override from the context menu

    // --- state ---
    public State State = State.Idle;
    public double X;                          // crab center, screen pixels
    public int Dir = 1;
    public double WinW = NormalW;
    public double Hunger;                     // 0..1
    public Eyes Eyes = Eyes.Open;
    public double CrabDx, CrabDy, Squash = 1; // pose offsets in DIPs, scenes drive these
    public bool Walking;
    public double Anim, Scale = 1;
    public Scene? Scene;
    public Icon Icon;
    public bool IsAsleep => State == State.Sleeping || State == State.Away;
    public Form Form = Form.Clawd;           // name him Шлёпа / Шлепа and he turns into a cat
    public bool Dragging, Petting, ShlepaAsleep;
    public double Lift, Twitch;               // Lift = height above the taskbar while carried / falling (DIPs)
    public bool Listening;   // music is playing: headphones on
    public double Wave = -1, Gaze, Size = 1;   // scene-driven crab pose: waving claw (-1 = off), eye shift, scale
    public bool Hidden;                         // crab not drawn (he is behind the tray door)

    bool placed, first = true;
    double timer = 2, target, hop, lastMin, lastMax;
    double blinkT = 3, blinking, emoteT, moodT = 60, flowerT = 1800;
    Eyes emoteEyes;
    double nameT;
    public string Name = "";
    DateTime lastZx = DateTime.MinValue;
    // sleeping vs. loud music
    bool complaining;
    double loudT, complainT, vol0, leaveVol, quietT;
    double fallV, strokeAmt, twitchT = 5;
    readonly Queue<double> clicks = new();
    readonly Random rnd = new();

    bool IsNight(DateTime n) => SleepHour > WakeHour
        ? n.Hour >= SleepHour || n.Hour < WakeHour
        : n.Hour >= SleepHour && n.Hour < WakeHour;

    public void Tick(double dt, DateTime now, double minX, double maxX, double scale)
    {
        Scale = scale; Anim += dt; lastMin = minX; lastMax = maxX;
        if (nameT > 0) nameT -= dt;
        strokeAmt = Math.Max(0, strokeAmt - 150 * dt);
        Petting = strokeAmt > 120 && Scene == null && State != State.Sleeping && State != State.Away && !Dragging;
        if (Petting) Stats.Stroked(dt);
        if (!placed) { X = minX + (maxX - minX) * (0.3 + 0.4 * rnd.NextDouble()); placed = true; }
        bool night = ForceNight ?? IsNight(now);
        GameMode.Night = night;   // the flat in Game mode goes to bed / wakes up with him

        if (Scene != null)
        {
            Scene.T += dt;
            Scene.Pose(this);
            if (Scene.Done) EndScene();
            ClampX();
            return;
        }

        if (Dragging || Lift > 0)   // picked up with the cursor, or falling back onto the taskbar
        {
            Icon = Icon.None; CrabDx = 0; CrabDy = 0; Squash = 1; Eyes = Eyes.Wide;
            Walking = Form == Form.Clawd;   // legs dangle
            if (Dragging) fallV = 0;
            else
            {
                fallV += 1400 * dt;
                Lift = Math.Max(0, Lift - fallV * dt);
                Walking = false;
                if (Lift <= 0) { fallV = 0; Emote(Eyes.Happy, Icon.None, 1.2); }   // landed
            }
            ClampX(); return;
        }

        if (State == State.InGame)   // inside the game room: the tray window sleeps, the viewer window plays him
        {
            Icon = Icon.None; Walking = false;
            if (!GameMode.Enabled && !GameMode.ViewerOpen) { GameMode.PetInside = false; StartScene(new ExitGameScene()); }
            ClampX(); return;
        }

        if (Form == Form.Shlepa) { TickShlepa(dt, night); ClampX(); return; }

        if (PlayIntro || RequestBox)
        {
            PlayIntro = RequestBox = false; first = false;
            StartScene(new BoxScene()); ClampX(); return;
        }
        if (first) { first = false; if (night) EnterSleep(); }   // app started at night: already asleep

        if (State == State.Sleeping)
        {
            if (!night) { StartScene(new WakeScene()); ClampX(); return; }
            if (RequestStorm)
            {
                RequestStorm = false; leaveVol = Audio.Volume; Stats.Betrayed();
                StartScene(new StormOffScene()); ClampX(); return;
            }
            SleepPose();

            bool loud = Audio.IsLoud;
            if (!complaining)
            {
                loudT = loud ? loudT + dt : 0;
                if (loudT >= 5) { complaining = true; complainT = 0; vol0 = Audio.Volume; loudT = 0; }   // asks nicely
            }
            else
            {
                complainT += dt;
                bool quieter = Audio.IsQuiet(vol0);
                if (quieter) complaining = false;
                else if (complainT >= 25)   // you ignored him: he storms off
                {
                    complaining = false; leaveVol = vol0; Stats.Betrayed();
                    StartScene(new StormOffScene()); ClampX(); return;
                }
                else Eyes = complainT > 12 ? Eyes.Angry : Eyes.Sad;
            }
            ClampX(); return;
        }
        if (State == State.Away)   // door closed; he comes back only when the music is quieter
        {
            CrabDx = 0; CrabDy = 0; Squash = 1; Walking = false;
            bool quiet = Audio.IsQuiet(leaveVol);
            quietT = quiet ? quietT + dt : 0;
            if (quietT >= 8) { quietT = 0; StartScene(new ReturnScene(night)); }
            ClampX(); return;
        }
        if (night) { StartScene(new BedScene()); ClampX(); return; }

        // ---- daytime ----
        Hunger = Math.Min(1, Hunger + dt / HungerSeconds);
        Stats.Hunger = Hunger;
        CrabDx = 0; CrabDy = 0; Squash = 1; Walking = false; Eyes = Eyes.Open;
        if (hop > 0)
        {
            hop -= dt;
            CrabDy = -Math.Sin((0.5 - Math.Max(hop, 0)) / 0.5 * Math.PI) * 12;
        }

        switch (State)
        {
            case State.Idle:
                if (Hunger >= 1) { State = State.Hungry; break; }
                timer -= dt;
                if (timer <= 0)
                {
                    double half = WinW * scale / 2, lo = minX + half, hi = maxX - half;
                    target = lo + rnd.NextDouble() * Math.Max(1, hi - lo);
                    if (Math.Abs(target - X) > 40 * scale) { Dir = target > X ? 1 : -1; State = State.Walk; }
                    else timer = 1;
                }
                break;
            case State.Walk:
                if (Hunger >= 1) { State = State.Hungry; break; }
                Walking = true;
                X += Dir * 45 * scale * dt;
                if ((target - X) * Dir <= 0) { State = State.Idle; timer = 2 + rnd.NextDouble() * 5; }
                break;
            case State.Hungry:
                Eyes = Eyes.Sad;
                if (Hunger < 1) State = State.Idle;
                break;
        }

        // Game mode: walk through the door into the viewer window
        if (GameMode.Enabled && !GameMode.PetInside && (State == State.Idle || State == State.Walk || State == State.Hungry))
        { StartScene(new EnterGameScene()); return; }

        // easter egg: a flower for a tiny human (rare)
        flowerT -= dt;
        bool flower = RequestFlower;
        if (flowerT <= 0)
        {
            flowerT = FlowerRollSeconds;
            if ((State == State.Idle || State == State.Walk) && rnd.NextDouble() < FlowerChance) flower = true;
        }
        if (flower) { RequestFlower = false; StartScene(new FlowerScene()); return; }

        // Zx1 easter egg: July 1 (7.01) + mouse untouched for 3 minutes, once per day
        bool zx = RequestZx;
        if (!zx && now.Month == 7 && now.Day == 1 && MouseIdle >= 180 && lastZx.Date != now.Date
            && (State == State.Idle || State == State.Walk)) zx = true;
        if (zx) { RequestZx = false; lastZx = now; StartScene(new ZxScene()); return; }

        // emotions: blinking, random moods, sleepy before bedtime
        blinkT -= dt;
        if (blinkT <= 0) { blinkT = 3 + rnd.NextDouble() * 4; blinking = 0.12; }
        if (blinking > 0) { blinking -= dt; if (Eyes == Eyes.Open) Eyes = Eyes.Closed; }

        if (Stats.JealousyReact)
        {
            Stats.JealousyReact = false;
            if (State != State.Hungry) Emote(Eyes.Angry, Icon.Anger, 2.5);   // jealous: you keep poking the boxes
        }
        if (Petting) Emote(Eyes.Love, Icon.Heart, 0.5);
        moodT -= dt;
        if (moodT <= 0 && State == State.Idle && emoteT <= 0)
        {
            moodT = (40 + rnd.NextDouble() * 80) * (1.6 - Stats.Attachment / 100);   // attached = more mood swings of joy
            switch (rnd.Next(3))
            {
                case 0: Emote(Eyes.Wide, Icon.Question, 2); break;
                case 1: Emote(Eyes.Happy, Icon.None, 2); break;
                default: Emote(Eyes.Wink, Icon.None, 1); break;
            }
        }
        if (emoteT > 0) { emoteT -= dt; Eyes = emoteEyes; }
        else
        {
            Icon = Listening && State != State.Hungry ? Icon.Note : Icon.None;
            if (Eyes == Eyes.Open && (NearBed(now) || Stats.AfkSeconds > 300)) Eyes = Eyes.Sleepy;   // bored when you're away
        }
        Listening = Audio.Playing;
        if (Listening && State == State.Idle) Squash = 1 + 0.03 * Math.Sin(Anim * 8);   // bobbing to the beat
        ClampX();
    }

    public void OnClick()
    {
        if (Scene != null || State == State.Sleeping) return;
        if (State == State.Hungry) { StartScene(new FeedScene()); return; }

        ShowName();
        Stats.Poked();
        clicks.Enqueue(Anim);
        while (clicks.Count > 0 && Anim - clicks.Peek() > 3) clicks.Dequeue();
        if (State == State.Walk) { State = State.Idle; timer = 2.5; }

        if (clicks.Count >= 5)   // poked too much: annoyed
        {
            clicks.Clear();
            Emote(Eyes.Angry, Icon.Anger, 3);
            return;
        }
        hop = 0.5;
        double r = rnd.NextDouble(), love = 0.15 + 0.6 * Stats.Attachment / 100;   // more attached = more hearts
        if (r < love) Emote(Eyes.Love, Icon.Heart, 1.8);
        else if (r < love + 0.3) Emote(Eyes.Happy, Icon.None, 1.6);
        else Emote(Eyes.Wide, Icon.Exclaim, 1.2);
    }

    void Emote(Eyes e, Icon i, double secs) { emoteEyes = e; Icon = i; emoteT = secs; }

    public void ShowName() { if (Name.Length > 0) nameT = 3; }

    bool NearBed(DateTime n)   // last 30 minutes before bedtime
    {
        double diff = (new TimeSpan(SleepHour, 0, 0) - n.TimeOfDay).TotalMinutes;
        if (diff < 0) diff += 1440;
        return diff <= 30;
    }

    public bool CanDrag => Scene == null && (State == State.Idle || State == State.Walk || State == State.Hungry);

    public void BeginDrag()
    {
        if (State == State.Walk) { State = State.Idle; timer = 3; }
        Dragging = true; fallV = 0;
    }

    public void EndDrag() => Dragging = false;

    public void Stroke(double distDip)   // cursor moving over him without a button: petting
    {
        if (Scene == null && State != State.Sleeping && State != State.Away && !Dragging) strokeAmt += distDip;
    }

    public void SetName(string n, bool animate)
    {
        Name = n;
        var f = IsShlepaName(n) ? Form.Shlepa : Form.Clawd;
        if (f == Form) return;
        if (animate && Scene == null && (State == State.Idle || State == State.Walk || State == State.Hungry))
            StartScene(new TransformScene(f));
        else
        {
            Form = f;
            if (State == State.Sleeping || State == State.Away) { State = State.Idle; WinW = NormalW; complaining = false; }
        }
    }

    static bool IsShlepaName(string n)
    {
        var s = n.Trim().ToLowerInvariant().Replace('ё', 'е');
        return s == "шлепа" || s == "floppa" || s == "big floppa";
    }

    double HeadTop(double fy) =>
        Form == Form.Shlepa ? FeetY - Sink - 45 + CrabDy : fy + CrabDy - 8 * Sprites.PX * Squash;

    double HeadX(double cx) => cx + CrabDx + (Form == Form.Shlepa ? 25 : 0);   // Shlepa's head is on the right

    void DrawBody(DrawingContext c, double cx, double fy)
    {
        if (Form == Form.Shlepa)
            Sprites.Shlepa(c, cx + CrabDx, FeetY - Sink + CrabDy, Eyes, Anim, Dragging || Lift > 0, Petting, Twitch > 0);
        else
            Sprites.Crab(c, cx + CrabDx, fy + CrabDy, Dir, Squash, Eyes, Walking, Anim, Wave, Gaze, Size);
    }

    // Shlepa is lazy: he lies on the edge of the taskbar, blinks, twitches an ear, purrs when petted, sleeps at night
    void TickShlepa(double dt, bool night)
    {
        State = State.Idle; WinW = NormalW; Walking = false; Dir = 1;
        CrabDx = 0; CrabDy = 0; Squash = 1; Eyes = Eyes.Open;
        Hunger = 0; Stats.Hunger = 0;
        ShlepaAsleep = night;
        Listening = !night && Audio.Playing;

        blinkT -= dt;
        if (blinkT <= 0) { blinkT = 2.5 + rnd.NextDouble() * 4; blinking = 0.14; }
        if (blinking > 0) { blinking -= dt; Eyes = Eyes.Closed; }
        twitchT -= dt;
        if (twitchT <= 0) { twitchT = 4 + rnd.NextDouble() * 6; Twitch = 0.25; }
        if (Twitch > 0) Twitch -= dt;
        if (hop > 0) { hop -= dt; CrabDy = -Math.Sin((0.5 - Math.Max(hop, 0)) / 0.5 * Math.PI) * 10; }

        if (Stats.JealousyReact) { Stats.JealousyReact = false; Emote(Eyes.Angry, Icon.Anger, 2.5); }
        if (Petting) Emote(Eyes.Love, Icon.Heart, 0.5);
        if (emoteT > 0) { emoteT -= dt; Eyes = emoteEyes; }
        else
        {
            Icon = Listening ? Icon.Note : Icon.None;
            if (night) Eyes = Eyes.Closed;
        }
    }

    void StartScene(Scene s)
    {
        Scene = s; State = State.Scene; WinW = s.WinW;
        Icon = Icon.None; emoteT = 0; clicks.Clear(); complaining = false; loudT = 0;
        Walking = false; CrabDx = 0; CrabDy = 0;
        ClampX();
    }

    void EndScene()
    {
        var s = Scene!; Scene = null;
        s.OnEnd(this);
        Wave = -1; Gaze = 0; Size = 1; Hidden = false;
        State = s.After;
        WinW = State == State.Sleeping ? SleepW : State == State.Away ? AwayW : NormalW;
        quietT = 0;
        timer = 2; Icon = Icon.None;
        if (State == State.Sleeping) SleepPose();
        else { CrabDx = 0; CrabDy = 0; Squash = 1; Eyes = Eyes.Open; Walking = false; }
    }

    void EnterSleep() { State = State.Sleeping; WinW = SleepW; SleepPose(); }

    void SleepPose()
    {
        Eyes = Eyes.Closed; Dir = 1; Walking = false; CrabDx = 0;
        CrabDy = -Sprites.BedTop;
        Squash = 1 + 0.015 * Math.Sin(Anim * 2);   // breathing
    }

    void ClampX()
    {
        double half = WinW * Scale / 2, lo = lastMin + half, hi = lastMax - half;
        X = hi > lo ? Math.Clamp(X, lo, hi) : (lo + hi) / 2;
    }

    public void Render(DrawingContext c)
    {
        double cx = WinW / 2, fy = FeetY;
        if (State == State.Away)   // empty bed, closed door with a sign, no crab
        {
            Sprites.Bed(c, cx, fy);
            Sprites.Nightstand(c, cx + 75, fy, true);
            Sprites.DoorBack(c, cx + DoorDx, fy);
            Sprites.DoorFront(c, cx + DoorDx, fy, 0, 0);
            Sprites.DoorSign(c, cx + DoorDx, fy, 0);
            return;
        }
        if (Scene != null) Scene.RenderBack(c, this, cx, fy);
        else if (State == State.Sleeping)
        {
            Sprites.Bed(c, cx, fy);
            Sprites.Nightstand(c, cx + 75, fy, true);
        }

        Rect? clip = Scene?.CrabClip(this, cx);
        if (Hidden) { }
        else if (clip is Rect cr)
        {
            using (c.PushClip(cr)) DrawBody(c, cx, fy);
        }
        else DrawBody(c, cx, fy);

        if (Form == Form.Shlepa && Scene == null)
        {
            if (ShlepaAsleep) Sprites.Zzz(c, HeadX(cx) + 4, HeadTop(fy) - 2, Anim);
            else if (Petting) Sprites.Bubble(c, HeadX(cx), HeadTop(fy) - 28, "мррр…");
        }

        if (Listening && Scene == null && (State == State.Idle || State == State.Walk || State == State.Hungry))
            Sprites.Headphones(c, HeadX(cx), HeadTop(fy) + (Form == Form.Shlepa ? 23 : 0));

        if (Scene != null) Scene.RenderFront(c, this, cx, fy);
        else if (State == State.Sleeping)
        {
            Sprites.Blanket(c, cx, fy, 1);
            if (complaining) Sprites.Bubble(c, cx, fy - 52, complainT > 12 ? "ПОТИШЕ!!" : "Потише, пожалуйста…");
            else Sprites.Zzz(c, cx + 14, fy - Sprites.BedTop - 34, Anim);
        }
        else if (State == State.Hungry && Icon == Icon.None)
            Sprites.Bubble(c, cx + CrabDx, fy - 38, "Хочу токены!");

        if (Icon != Icon.None && State != State.Sleeping)
            Sprites.IconAbove(c, HeadX(cx), HeadTop(fy) - 6, Icon, Anim);

        if (nameT > 0 && Name.Length > 0 && State != State.Sleeping)
            Sprites.NameTag(c, HeadX(cx), HeadTop(fy) - 44, Name, Math.Min(1, nameT));
    }
}
