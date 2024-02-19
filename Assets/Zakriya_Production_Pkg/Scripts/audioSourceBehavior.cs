using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class audioSourceBehavior : MonoBehaviour
{
    public bool destroyOnComplete;
    public Vector3 spawnTarget;
    public AudioSource audioSource;
    public AudioClip clip;

    // Start is called before the first frame update
    void Start()
    {
        if (spawnTarget != null) 
        {
            transform.position = spawnTarget;
        }

        audioSource.clip = clip;

        if (destroyOnComplete)
        {
            //Sound Type
            StartCoroutine(DestroyGameObjectAfterAudioPlayback());
            audioSource.volume = PlayerPrefs.GetFloat("soundVolume", 1f);
            audioSource.loop = false;
        }
        else 
        {
            //BGM Type
            audioSource.volume = PlayerPrefs.GetFloat("musicVolume", 1f);
            audioSource.loop = true;
        }

        audioSource.Play();
    }

    private IEnumerator DestroyGameObjectAfterAudioPlayback()
    {
        // Wait for the audio playback to complete
        yield return new WaitForSeconds(audioSource.clip.length);

        // Destroy the GameObject
        Destroy(gameObject);
    }
}
