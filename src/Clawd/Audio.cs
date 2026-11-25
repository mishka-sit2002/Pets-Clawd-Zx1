#if WINDOWS_AUDIO
using NAudio.CoreAudioApi;
using Windows.Media.Control;
#endif

namespace Clawd;

/// <summary>
/// What the PC is playing: whether any media session (Spotify, YouTube Music, browser, ...) is playing,
/// the master volume and the real output level. Windows only; on other systems nothing is ever "playing".
/// </summary>
static class Audio
{
    public static bool Playing;
    public static double Volume;   // 0..1 master volume
    public static double Peak;     // 0..1 current output level

    /// <summary>Master volume above which music counts as "loud" at night (0..1). Raise it to make him more tolerant.</summary>
    public const double LoudAt = 0.55;

    static double silentFor;       // seconds the output has been silent (paused tab / muted player still reports "playing")
    public static bool Audible => Playing && silentFor < 3;
    public static bool IsLoud => Audible && Volume >= LoudAt;
    /// <summary>Calm again: nothing audible, clearly below the loud threshold, or turned down by 10+ points since he complained.</summary>
    public static bool IsQuiet(double refVolume) => !Audible || Volume < LoudAt - 0.05 || Volume <= refVolume - 0.10;

#if WINDOWS_AUDIO
    static MMDeviceEnumerator? en;
    static GlobalSystemMediaTransportControlsSessionManager? mgr;
    static bool requested;
    static double t;

    static async Task InitSessions()
    {
        try { mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); } catch { }
    }
#endif

    public static void Poll(double dt)
    {
#if WINDOWS_AUDIO
        t -= dt;
        if (t > 0) return;
        t = 0.5;

        try
        {
            en ??= new MMDeviceEnumerator();
            using var dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            Volume = dev.AudioEndpointVolume.MasterVolumeLevelScalar;
            Peak = dev.AudioMeterInformation.MasterPeakValue;
        }
        catch { Volume = 0; Peak = 0; }
        silentFor = Peak > 0.01 ? 0 : silentFor + 0.5;

        if (!requested) { requested = true; _ = InitSessions(); }
        bool playing = false;
        try
        {
            if (mgr != null)
                foreach (var s in mgr.GetSessions())
                    if (s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        playing = true;
        }
        catch { }
        Playing = playing;
#endif
    }
}
