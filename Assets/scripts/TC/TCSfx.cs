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
}
