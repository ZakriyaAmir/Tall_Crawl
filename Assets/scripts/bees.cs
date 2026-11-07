using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bees : MonoBehaviour
{
    AudioSource audioSrc;
    static player cachedPlayer;

    const float NearDistance = 1.8f;   // Loud / normal volume when spider is near
    const float FadeDistance = 8.0f;   // Completely faded away when spider is far
    const float MaxVolume = 0.95f;     // Full normal volume

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc)
        {
            audioSrc.loop = true;
            audioSrc.volume = 0f;
        }
    }

    void OnEnable()
    {
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc)
        {
            audioSrc.loop = true;
            audioSrc.volume = 0f;
        }
    }

    void OnDisable()
    {
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();
    }

    void Update()
    {
        if (!audioSrc) return;

        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        if (cachedPlayer == null) return;

        float dist = Vector3.Distance(transform.position, cachedPlayer.transform.position);

        if (dist >= FadeDistance + 2.0f)
        {
            if (audioSrc.isPlaying)
            {
                audioSrc.volume = 0f;
                audioSrc.Stop();
            }
            return;
        }

        // Smooth proximity fade: 1.0 when near (<= NearDistance), fading smoothly to 0.0 at FadeDistance
        float t = Mathf.Clamp01(1f - (dist - NearDistance) / (FadeDistance - NearDistance));
        float smooth = t * t * (3f - 2f * t); // Smooth Hermite curve
        float targetVol = MaxVolume * smooth;

        audioSrc.volume = targetVol;

        if (targetVol > 0.001f)
        {
            if (!audioSrc.isPlaying) audioSrc.Play();
        }
        else if (audioSrc.isPlaying && dist >= FadeDistance)
        {
            audioSrc.Stop();
        }
    }
}
