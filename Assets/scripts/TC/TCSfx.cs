using System.Collections.Generic;
using UnityEngine;

/// Cute procedural spider voice SFX (Assets/TC/Resources/Sfx, made by Tools/gen_spider_sfx.py).
/// Muting is honoured globally through AudioListener.volume (TCSave.Sound).
public static class TCSfx
{
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    public static AudioClip Get(string n)
    {
        if (cache.TryGetValue(n, out var c)) return c;
        c = Resources.Load<AudioClip>("Sfx/" + n); cache[n] = c; return c;
    }
    public static void Play(string n, float v = 0.8f) => TCAudio.Play(Get(n), v);
    public static void Play(string n, float v, float pitch) => TCAudio.Play(Get(n), v, pitch);

    /// Voice pool for the cute spider:
    ///   wave  — the "hi-hiii!" clip played when it waves on the main menu (happy chatter)
    ///   death — the in-game death voice, used only for dizzy / flop gags, never for happy actions
    /// Layered under tap-action SFX with a slight per-action + random pitch so it chatters, not loops.
    public static AudioClip VoiceHappy => Get("wave");
    public static AudioClip VoiceDizzy => Get("death");
    public static void PlayVoice(bool dizzy, float v = 0.55f, float pitch = 1f)
        => TCAudio.Play(dizzy ? VoiceDizzy : VoiceHappy, v, pitch);
    public static void PlayVoice(float v = 0.55f, float pitch = 1f) => PlayVoice(false, v, pitch);

    /// Action stinger + talking voice together. <paramref name="dizzy"/> selects the death voice
    /// (only for flop / dizzy gags). Pitch is a per-action base plus a small random jitter.
    public static void PlayAction(string sfx, float sfxVol, float voicePitch, bool dizzy = false, float voiceVol = 0.52f)
    {
        if (!string.IsNullOrEmpty(sfx)) Play(sfx, sfxVol);
        PlayVoice(dizzy, voiceVol, voicePitch + Random.Range(-0.07f, 0.07f));
    }
}
