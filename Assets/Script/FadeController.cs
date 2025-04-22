using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(AudioSource))] // Assurer qu'il y a un AudioSource
public class FadeController : MonoBehaviour
{
    public static FadeController Instance { get; private set; }

    [Header("Visual Fade Settings")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeOutDuration = 2.0f; // Durée du fondu visuel vers le noir
    public float fadeInDuration = 1.5f;  // Durée du fondu visuel depuis le noir

    [Header("Audio Transition Settings")]
    public AudioClip transitionClip; // Le fichier audio (vagues -> sous l'eau -> ambiance)
    public float visualFadeStartTime = 0.5f; // Moment où le fondu au noir commence (en secondes audio)
    public float sceneLoadTime = 6.5f;     // Moment où la nouvelle scène est chargée (en secondes audio)
    public float audioEndTime = 30.0f;     // Moment où l'audio doit finir de s'estomper (en secondes audio)
    public float audioFadeOutDuration = 5.0f; // Durée du fondu audio final

    private AudioSource transitionAudioSource;
    private bool isTransitioning = false; // Renommé pour plus de clarté
    private Coroutine audioFadeCoroutine; // Pour pouvoir arrêter le fondu audio si besoin

    void Awake()
    {
        // --- Singleton ---
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        // --- Fin Singleton ---

        // --- Récupération des composants ---
        transitionAudioSource = GetComponent<AudioSource>();
        transitionAudioSource.playOnAwake = false; // Important: on le contrôle manuellement
        transitionAudioSource.loop = false; // Le son ne doit jouer qu'une fois

        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
            if (fadeCanvasGroup == null)
            {
                Debug.LogError("FadeController: CanvasGroup non trouvé !");
                enabled = false;
                return;
            }
        }

        // Vérification initiale
        if (transitionClip == null)
        {
             Debug.LogWarning("FadeController: Aucun AudioClip de transition assigné dans l'inspecteur.");
        }
         // Assurer que les temps sont logiques
        if (sceneLoadTime <= visualFadeStartTime) {
            Debug.LogError("FadeController: sceneLoadTime doit être supérieur à visualFadeStartTime !");
        }
        if (audioEndTime <= sceneLoadTime) {
             Debug.LogWarning("FadeController: audioEndTime est très proche ou avant sceneLoadTime. Le fondu audio final sera court ou inexistant.");
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Méthode appelée par le bouton via SceneLoader
    public void StartSceneTransition(string sceneName)
    {
        if (!isTransitioning)
        {
            if (transitionClip == null) {
                Debug.LogError("Impossible de lancer la transition : aucun AudioClip assigné !");
                return;
            }
            // Lancer la coroutine principale qui gère tout le processus
            StartCoroutine(TimedTransitionCoroutine(sceneName));
        }
        else
        {
            Debug.LogWarning("FadeController: Transition déjà en cours.");
        }
    }



    /// <summary>
    /// Lance un fondu visuel sortant puis charge la scène, SANS l'audio de transition.
    /// </summary>
    public void FadeOutAndLoadSceneSimple(string sceneName)
    {
        if (!isTransitioning)
        {
            // Lancer la coroutine pour le fondu visuel simple
            StartCoroutine(FadeOutAndLoadSimpleCoroutine(sceneName));
        }
        else
        {
            Debug.LogWarning("FadeController: Transition déjà en cours.");
        }
    }

    // Coroutine pour le fondu visuel simple et le chargement
    private IEnumerator FadeOutAndLoadSimpleCoroutine(string sceneName)
    {
        isTransitioning = true; // Marquer qu'une transition (visuelle) est en cours
        Debug.Log($"Transition visuelle simple démarrée vers '{sceneName}'.");

        // 1. Lancer le fondu visuel au noir et attendre sa fin
        yield return StartCoroutine(FadeOutVisualCoroutine());

        // 2. Une fois l'écran noir, charger la scène
        Debug.Log($"Écran noir atteint. Chargement de la scène '{sceneName}'.");
        SceneManager.LoadScene(sceneName);

        // La suite (fondu visuel entrant) sera gérée par OnSceneLoaded
        // isTransitioning sera remis à false à la fin de FadeInVisualCoroutine
    }


    // --- Coroutine Principale de Transition ---
    private IEnumerator TimedTransitionCoroutine(string sceneName)
    {
        isTransitioning = true;
        Debug.Log("Transition démarrée.");

        // Optionnel : Arrêter les autres sons (ex: musique de la scène Landing)
        // FindObjectOfType<AudioManager>()?.StopAllSounds(); // Adapte selon ton AudioManager

        // 1. Démarrer l'audio de transition
        transitionAudioSource.clip = transitionClip;
        transitionAudioSource.volume = 1f; // Assurer le volume max au début
        transitionAudioSource.time = 0f;   // Rembobiner au début
        transitionAudioSource.Play();
        Debug.Log($"Audio '{transitionClip.name}' démarré.");

        // 2. Attendre jusqu'au début du fondu visuel (visualFadeStartTime)
        while (transitionAudioSource.isPlaying && transitionAudioSource.time < visualFadeStartTime)
        {
            yield return null; // Attendre la prochaine frame
        }

        // Vérifier si l'audio s'est arrêté prématurément
        if (!transitionAudioSource.isPlaying && transitionAudioSource.time < visualFadeStartTime) {
             Debug.LogError("L'audio s'est arrêté avant le début du fondu visuel !");
             isTransitioning = false;
             yield break; // Arrêter la coroutine
        }

        Debug.Log($"Temps audio {transitionAudioSource.time:F2}s atteint. Démarrage du fondu visuel au noir.");
        // 3. Lancer le fondu visuel au noir (FadeOutCoroutine)
        //    On le lance mais on n'attend pas sa fin ici, on continue de vérifier le temps audio
        StartCoroutine(FadeOutVisualCoroutine());

        // 4. Attendre jusqu'au moment de charger la scène (sceneLoadTime)
        while (transitionAudioSource.isPlaying && transitionAudioSource.time < sceneLoadTime)
        {
            yield return null;
        }

         if (!transitionAudioSource.isPlaying && transitionAudioSource.time < sceneLoadTime) {
             Debug.LogWarning("L'audio s'est arrêté avant le chargement de la scène ! Chargement quand même.");
             // On continue quand même, mais le timing sera peut-être décalé
         }

        Debug.Log($"Temps audio {transitionAudioSource.time:F2}s atteint. Chargement de la scène '{sceneName}'.");

        // Assurer que l'écran est bien noir avant de charger
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;

        // 5. Charger la nouvelle scène
        SceneManager.LoadScene(sceneName);

        // La suite (fondu visuel entrant, fondu audio sortant) sera gérée par OnSceneLoaded
        // isTransitioning sera remis à false à la fin du fondu visuel entrant (FadeInVisualCoroutine)
    }


    // --- Coroutines pour les fondus VISUELS ---

    private IEnumerator FadeInVisualCoroutine()
    {
        // isTransitioning est déjà true
        fadeCanvasGroup.blocksRaycasts = true;
        float timer = 0f;
        fadeCanvasGroup.alpha = 1f; // Partir du noir

        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = 1f - (timer / fadeInDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        isTransitioning = false; // La transition est terminée visuellement
        Debug.Log("Fondu visuel entrant terminé. Transition terminée.");
    }

    private IEnumerator FadeOutVisualCoroutine()
    {
        // isTransitioning est déjà true
        fadeCanvasGroup.blocksRaycasts = true;
        float timer = 0f;
        fadeCanvasGroup.alpha = 0f; // Partir du transparent

        while (timer < fadeOutDuration)
        {
            timer += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = timer / fadeOutDuration;
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
        // Ne pas mettre isTransitioning à false ici
        // Ne pas débloquer les raycasts ici
    }

    // --- Coroutine pour le fondu AUDIO final ---
    private IEnumerator FadeOutAudioCoroutine()
    {
        // Attendre que le temps audio atteigne le début du fondu final
        float audioFadeStartTime = audioEndTime - audioFadeOutDuration;
        if (audioFadeStartTime < transitionAudioSource.time) {
            Debug.LogWarning("Le fondu audio final commence immédiatement car le temps audio a déjà dépassé audioFadeStartTime.");
            audioFadeStartTime = transitionAudioSource.time; // Commencer maintenant
        } else {
             while (transitionAudioSource.isPlaying && transitionAudioSource.time < audioFadeStartTime)
             {
                 yield return null;
             }
        }


        if (!transitionAudioSource.isPlaying) yield break; // L'audio s'est arrêté avant

        Debug.Log($"Temps audio {transitionAudioSource.time:F2}s atteint. Démarrage du fondu audio final.");
        float startVolume = transitionAudioSource.volume; // Utiliser le volume actuel au cas où il aurait été changé
        float timer = 0f;
        float currentAudioTimeStart = transitionAudioSource.time; // Temps au début de ce fondu

        while (transitionAudioSource.isPlaying && timer < audioFadeOutDuration)
        {
            // Vérifier si le temps audio progresse toujours (au cas où le jeu serait en pause sans que l'audio le soit)
            if (transitionAudioSource.time < currentAudioTimeStart) {
                Debug.LogWarning("Le temps audio semble avoir reculé ou être bloqué. Arrêt du fondu audio.");
                yield break;
            }

            timer += Time.unscaledDeltaTime; // Utiliser unscaled pour être indépendant du Time.timeScale
            transitionAudioSource.volume = Mathf.Lerp(startVolume, 0f, timer / audioFadeOutDuration);
            yield return null;
        }

        if (transitionAudioSource != null) // Vérifier s'il existe toujours
        {
            transitionAudioSource.Stop();
            transitionAudioSource.volume = startVolume; // Remettre le volume par défaut pour la prochaine fois
            Debug.Log("Fondu audio final terminé.");
        }
         audioFadeCoroutine = null; // Indiquer que la coroutine est terminée
    }


    // --- Gestionnaire d'événement de chargement de scène ---
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. Démarrer le fondu visuel entrant (toujours)
        StartCoroutine(FadeInVisualCoroutine());

        // 2. Gérer le fondu audio final SEULEMENT si l'audio de transition est toujours en cours
        if (transitionAudioSource != null && transitionAudioSource.isPlaying && transitionAudioSource.clip == transitionClip)
        {
            // Arrêter toute coroutine de fondu audio précédente (sécurité)
            if (audioFadeCoroutine != null) {
                StopCoroutine(audioFadeCoroutine);
            }
            // Lancer la nouvelle coroutine pour gérer la fin de l'audio
            audioFadeCoroutine = StartCoroutine(FadeOutAudioCoroutine());
        }
    }


}
