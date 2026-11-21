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
