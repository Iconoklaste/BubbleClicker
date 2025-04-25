using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AudioType
{
    Pop,
    Swipe,
    Dead,
    BubbleSpawn
    // Ajoute ici d'autres types si nécessaire
}

public enum AudioSourceType
{
    Bubble, // Peut-être pas utilisé si tu as Game et Player ? À vérifier.
    Game,
    Player
}

// [RequireComponent(typeof(AudioSource))] // Tu as des sources spécifiques, donc pas forcément besoin ici
public class AudioManager : MonoBehaviour
{
    // --- Singleton ---
    // Utiliser une propriété est légèrement plus robuste
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Essayer de trouver une instance existante
                _instance = FindObjectOfType<AudioManager>();
                if (_instance == null)
                {
                    // Si aucune instance n'existe, log une erreur car elle devrait être dans la scène
                    Debug.LogError("AudioManager instance not found in the scene!");
                }
            }
            return _instance;
        }
    }
    // --- Fin Singleton ---


    [Header("Volume Control")]
    [Range(0f, 1f)] // Pratique pour régler dans l'inspecteur
    public float volume = 1f;

    [Header("Audio Sources")]
    [Tooltip("Source pour les sons généraux du jeu (musique d'ambiance, effets UI globaux...)")]
    public AudioSource gameSource;
    [Tooltip("Source pour les sons directement liés aux actions du joueur (clics, pop...)")]
    public AudioSource playerSource;

    
    [System.Serializable]
    public struct AudioData
    {
        public AudioClip clip;
        public AudioType type;
    }

    [Header("Audio Clips")]
    public AudioData[] audioData;

    // Dictionnaire pour un accès plus rapide aux clips une fois chargés
    private Dictionary<AudioType, AudioClip> audioClipMap;
    private bool isInitialized = false; // Pour éviter les initialisations multiples

    void Awake()
    {
        // --- Gestion Singleton Améliorée ---
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject); // Si tu veux qu'il persiste entre les scènes
        }
        else if (_instance != this)
        {
            Debug.LogWarning("Une autre instance de AudioManager existe déjà. Destruction de celle-ci.");
            Destroy(gameObject);
            return; // Important pour arrêter l'exécution de Awake/Start sur l'objet détruit
        }
        // --- Fin Gestion Singleton ---

        // Initialisation de base qui doit se faire avant tout le reste
        InitializeAudioSources();
        BuildAudioClipMap(); // Préparer le dictionnaire
    }

    void Start()
    {
        // Appliquer le volume initial (peut aussi être fait dans InitializeAudioSources)
        SetGlobalVolume(volume);
    }

    // --- NOUVELLE MÉTHODE ASYNCHRONE ---
    /// <summary>
    /// Effectue les initialisations asynchrones nécessaires pour l'AudioManager.
    /// Peut être étendue pour charger des banques de sons, etc.
    /// </summary>
    /// <returns>IEnumerator pour être utilisé dans une coroutine.</returns>
    public IEnumerator InitializeAsync()
    {
        if (isInitialized)
        {
            Debug.Log("AudioManager déjà initialisé.");
            yield break; // Sortir si déjà fait
        }

        Debug.Log("AudioManager: Initialisation asynchrone démarrée...");

        // Étape 1: Vérifier si les sources audio sont prêtes (généralement instantané)
        if (gameSource == null || playerSource == null)
        {
            Debug.LogError("AudioManager: Sources audio non assignées ! Initialisation échouée.");
            yield break; // Arrêter si les sources manquent
        }
        yield return null; // Laisser une frame pour s'assurer que tout est stable

        // Étape 2: Précharger des données audio si nécessaire (Exemple simulé)
        // Ici, tu pourrais charger des Addressables ou des données depuis le disque.
        // Pour l'instant, on simule juste un petit délai.
        Debug.Log("AudioManager: Préchargement des données audio (simulé)...");
        yield return new WaitForSecondsRealtime(0.1f); // Utilise Realtime si tu veux que ça fonctionne même si Time.timeScale = 0

        // Étape 3: Autres vérifications ou préparations
        // ...

        isInitialized = true;
        Debug.Log("AudioManager: Initialisation asynchrone terminée.");
    }
    // --- FIN NOUVELLE MÉTHODE ---


    // Méthode pour initialiser les sources (appelée dans Awake)
    private void InitializeAudioSources()
    {
        if (gameSource == null) Debug.LogError("AudioManager: gameSource non assignée !");
        if (playerSource == null) Debug.LogError("AudioManager: playerSource non assignée !");

        // Configurer les sources si nécessaire (optionnel)
        // if (gameSource != null) gameSource.playOnAwake = false;
        // if (playerSource != null) playerSource.playOnAwake = false;
    }

    // Méthode pour construire le dictionnaire (appelée dans Awake)
    private void BuildAudioClipMap()
    {
        audioClipMap = new Dictionary<AudioType, AudioClip>();
        foreach (AudioData data in audioData)
        {
            if (data.clip != null)
            {
                if (!audioClipMap.ContainsKey(data.type))
                {
                    audioClipMap.Add(data.type, data.clip);
                }
                else
                {
                    Debug.LogWarning($"AudioManager: Type audio '{data.type}' défini plusieurs fois. Utilisation du premier clip trouvé.");
                }
            }
            else
            {
                Debug.LogWarning($"AudioManager: Aucun AudioClip assigné pour le type '{data.type}'.");
            }
        }
    }


    public void PlaySound(AudioType type, AudioSourceType sourceType)
    {
        // Utiliser le dictionnaire pour plus d'efficacité
        if (audioClipMap.TryGetValue(type, out AudioClip clip))
        {
            AudioSource sourceToUse = null;
            switch (sourceType)
            {
                case AudioSourceType.Game:
                    sourceToUse = gameSource;
                    break;
                case AudioSourceType.Player:
                    sourceToUse = playerSource;
                    break;
                // case AudioSourceType.Bubble: // Gérer ce cas si nécessaire
                //     sourceToUse = ... ; // Peut-être une source spécifique pour les bulles ? Ou utiliser playerSource ?
                //     break;
                default:
                    Debug.LogWarning($"AudioManager: AudioSourceType non géré : {sourceType}");
                    break;
            }

            if (sourceToUse != null)
            {
                sourceToUse.PlayOneShot(clip);
            }
            else
            {
                 Debug.LogError($"AudioManager: Aucune source audio valide trouvée pour le sourceType {sourceType}.");
            }
        }
        else
        {
            Debug.LogError($"AudioManager: Aucun clip trouvé pour le type {type} dans le dictionnaire.");
        }
    }

    // Ancienne méthode getClip (peut être supprimée ou gardée comme fallback)
    /*
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
    */

    public void StopAllSounds()
    {
        if (gameSource != null && gameSource.isPlaying)
        {
            gameSource.Stop();
        }
        if (playerSource != null && playerSource.isPlaying)
        {
            playerSource.Stop();
        }
        Debug.Log("AudioManager: Toutes les sources audio gérées (Game, Player) ont été arrêtées.");
    }

    // Méthode pour changer le volume global
    public void SetGlobalVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume); // Assurer que le volume reste entre 0 et 1
        if (gameSource != null) gameSource.volume = volume;
        if (playerSource != null) playerSource.volume = volume;
        Debug.Log($"AudioManager: Volume global réglé à {volume}");
    }
}
