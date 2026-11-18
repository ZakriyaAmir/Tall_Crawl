using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bees : MonoBehaviour
{
    [Header("Swarm Rotation")]
    public float spinSpeed = 240f; // Constant rotation speed around the tree trunk (degrees per second)

    AudioSource audioSrc;
    static player cachedPlayer;

    const float NearDistance = 1.8f;   // Loud / normal volume when spider is near
    const float FadeDistance = 7.5f;   // Completely faded away when spider is far
    const float MaxVolume = 0.55f;     // Gentle, soothing ambient volume

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
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i]) cols[i].enabled = true;
        }

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
        // Constant circling around the tree trunk (World Y axis)
        if (spinSpeed != 0f)
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                transform.Rotate(0f, spinSpeed * dt, 0f, Space.World);
            }
        }

        if (!audioSrc) return;

        bool isTutorial = TCGameUI.Instance != null && TCGameUI.Instance.IsTutorialRun;
        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        if (cachedPlayer == null) return;
        if (!isTutorial)
        {
            if (cachedPlayer.paused || cachedPlayer.currentScore < 200f)
            {
                if (audioSrc.isPlaying) { audioSrc.volume = 0f; audioSrc.Stop(); }
                return;
            }
            if (TCGameUI.Instance != null && !TCGameUI.Instance.Started)
            {
                if (audioSrc.isPlaying) { audioSrc.volume = 0f; audioSrc.Stop(); }
                return;
            }
        }

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
