using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AudioType
{
    Pop,
    Swipe,
    Dead,
    BubbleSpawn
}

public enum AudioSourceType
{
    Bubble,
    Game,
    Player
}

public class AudioManager : MonoBehaviour
{
    static public AudioManager Instance;

    public float volume = 1f;

    public AudioSource gameSource;
    public AudioSource playerSource;

    [System.Serializable]
    public struct AudioData
    {
        public AudioClip clip;
        public AudioType type;
    }

    public AudioData[] audioData;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        gameSource.volume = volume;
        playerSource.volume = volume;
    }

    public void PlaySound(AudioType type, AudioSourceType sourceType)
    {
        AudioClip clip = getClip(type);

        if (sourceType == AudioSourceType.Game)
        {
            gameSource.PlayOneShot(clip);
        }
        else if (sourceType == AudioSourceType.Player)
        {
            playerSource.PlayOneShot(clip);
        }
    }

    AudioClip getClip(AudioType type)
    {
        foreach (AudioData data in audioData)
        {
            if (data.type == type)
            {
                return data.clip;
            }
        }
        Debug.LogError("AudioManager: No clip found for type " + type);
        return null;
    }

    public void StopAllSounds()
    {
        if (gameSource != null)
        {
            gameSource.Stop();
        }
        if (playerSource != null)
        {
            playerSource.Stop();
        }
         Debug.Log("AudioManager: Toutes les sources audio gérées ont été arrêtées.");
    }
}

