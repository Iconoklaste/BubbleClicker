using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using TMPro;




public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Elements")]
    public Text scoreText;
    public Text bubbleText;
    public Slider coverageSlider;
    public Camera mainCamera;
    public GameObject freezeIndicator;
    public Slider freezeSlider;
    public GameObject swipeIndicator;
    public Slider swipeSlider;
    public GameObject gameOverPanel;


    [Header("Game Variables")]
    public bool gameIsOver = false;
    public float txCouvertureMax = 0.6f;
    private int score = 0;
    private int nb_bulles = 0;
    // Ce champ stocke le taux de couverture calcul� (par rapport � la zone confin�e)
    private float coveragePercentage = 0f;
    // La valeur utilisée pour l'affichage dans la barre de vie, qui sera lissée
    private float displayedCoveragePercentage;
    // Vitesse à laquelle la barre se met à jour visuellement
    public float coverageSmoothingSpeed = 5.0f;

    private AudioManager audioManagerInstance;

    [Header("Spawner Reference")]
    public BubbleSpawner bubbleSpawner;



    // --- NOUVELLES VARIABLES POUR LES MODES GLOBAUX ---
    private bool _isSwipeModeActive = false;
    public bool IsSwipeModeActive => _isSwipeModeActive; // Propriété publique en lecture seule

    private bool _isFreezeModeActive = false;
    public bool IsFreezeModeActive => _isFreezeModeActive; // Propriété publique en lecture seule

    // --- Optionnel : Timers pour les modes ---
    private Coroutine swipeModeTimerCoroutine;
    private float swipeTimer = 0f;           // <<--- AJOUT : Temps restant pour le Swipe
    private float currentSwipeDuration = 0f; // <<--- AJOUT : Durée totale du Swmipe actuel


    private Coroutine freezeModeTimerCoroutine;
    private float freezeTimer = 0f;           // <<--- AJOUT : Temps restant pour le Freeze
    private float currentFreezeDuration = 0f; // <<--- AJOUT : Durée totale du Freeze actuel

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
            // DontDestroyOnLoad(gameObject); // Si nécessaire
        }
        else
        {
            Destroy(gameObject);
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("GameManager: Aucune caméra principale trouvée ou assignée !");
            }
        }
         // Assure-toi que la caméra est orthographique pour cette implémentation de GetGameArea
        if (mainCamera != null && !mainCamera.orthographic)
        {
             Debug.LogWarning("GameManager: La caméra principale n'est pas orthographique. GetGameArea suppose une caméra orthographique.");
        }
        // Initialiser la valeur affichée dans la barre de vie à la valeur réelle au démarrage
        displayedCoveragePercentage = coveragePercentage;
         // Validation importante pour éviter les divisions par zéro ou logiques étranges
         if (txCouvertureMax <= 0f)
         {
             Debug.LogError("txCouvertureMax doit être supérieur à 0 ! Réglage à 1.0f par défaut.");
             txCouvertureMax = 1.0f;
         }
    }


    void Start()
    {
        Time.timeScale = 1f; // Le jeu d�marre actif

        // Désactiver les indicateurs de mode au démarrage
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (freezeIndicator != null) freezeIndicator.SetActive(false);
        if (swipeIndicator != null) swipeIndicator.SetActive(false);

        // Réinitialiser les états et timers
        gameIsOver = false;
        score = 0;
        nb_bulles = 0;
        coveragePercentage = 0f;

        _isFreezeModeActive = false;
        freezeTimer = 0f;
        currentFreezeDuration = 0f;

        _isSwipeModeActive = false;
        swipeTimer = 0f;
        currentSwipeDuration = 0f;

        UpdateUI();
    }

    void FixedUpdate()
    {
        // Appelle CheckBubbleCoverage ici, au rythme de la physique
        if (!gameIsOver)
        {
            CheckBubbleCoverage();
        }

        // --- Lissage de la valeur de couverture ---
        // Fait tendre progressivement la valeur affichée vers la valeur réelle
        // Utilise Lerp (Linear Interpolation) pour un effet smooth
        displayedCoveragePercentage = Mathf.Lerp(
            displayedCoveragePercentage,
            coveragePercentage,
            Time.deltaTime * coverageSmoothingSpeed
        );

        // --- Mise à jour continue de l'UI ---
        // Appelle UpdateUI à chaque frame pour refléter le lissage
        UpdateUI();
    }

    void Update()
    {
        // --- Autre logique de Update (score, game over, input global...) ---


        // --- MISE A JOUR DU SLIDER FREEZE ---
        if (_isFreezeModeActive) // Vérifier si le mode est actif
        {
            // Vérifier si le slider est assigné et si la durée est valide
            if (freezeSlider != null && currentFreezeDuration > 0)
            {
                // Calculer la proportion de temps restant (valeur entre 0 et 1)
                float sliderFreezeValue = freezeTimer / currentFreezeDuration;
                // Appliquer la valeur au slider, en s'assurant qu'elle reste entre 0 et 1
                freezeSlider.value = Mathf.Clamp01(sliderFreezeValue);
            }
        }

        // --- MISE A JOUR DU SLIDER SWIPE ---
        if (_isSwipeModeActive) // Vérifier si le mode est actif
        {
            // Vérifier si le slider est assigné et si la durée est valide
            if (swipeSlider != null && currentSwipeDuration > 0)
            {
                // Calculer la proportion de temps restant (valeur entre 0 et 1)
                float swipeSwipeValue = swipeTimer / currentSwipeDuration;
                // Appliquer la valeur au slider, en s'assurant qu'elle reste entre 0 et 1
                swipeSlider.value = Mathf.Clamp01(swipeSwipeValue);
            }
        }
    }

    public void AddScore(int pointsToAdd)
    {
        if (!gameIsOver)
        {
            score += pointsToAdd;
            UpdateUI();
        }
    }

    public void NbBubblePoped(int bubbleToAdd){
        if (!gameIsOver)
        {
            nb_bulles += bubbleToAdd;
            UpdateUI();
        }
    }

    void UpdateUI()
    {
        float displayedPercentage = Mathf.Clamp((coveragePercentage) * 100f, 0f, 100f);
        

        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }

        if (bubbleText != null)
        {
            bubbleText.text = "Popped: " + nb_bulles;
        }

/*         if (coverageText != null)
        {
            // Convertir le taux de couverture en pourcentage
            // Ici, on consid�re que lorsque coveragePercentage atteint txCouvertureMax, c'est 100% affich�
            coverageText.text = "Couverture: " + displayedPercentage.ToString("F1") + "%";
        } */

        // --- Mise à jour du Slider (affiche le % de progression VERS txCouvertureMax) ---
        if (coverageSlider != null)
        {
            // 1. Calculer la progression actuelle par rapport au maximum autorisé.
            //    Si txCouvertureMax est 0.8 et displayedCoveragePercentage est 0.4,
            //    le ratio est 0.4 / 0.8 = 0.5 (soit 50%).
            float progressRatio = 0f;
            if (txCouvertureMax > 0f) // Éviter la division par zéro
            {
                // On s'assure que la valeur affichée ne dépasse pas le max pour ce calcul
                float clampedDisplayed = Mathf.Min(displayedCoveragePercentage, txCouvertureMax);
                progressRatio = clampedDisplayed / txCouvertureMax;
            }

            // 2. Convertir ce ratio (0 à 1) en pourcentage (0 à 100) pour le slider.
            float sliderValue = Mathf.Clamp01(progressRatio) * 100f;


            // 4. Appliquer au slider (qui doit avoir Min=0, Max=100 dans l'inspecteur).
            coverageSlider.value = sliderValue;
        }

    }

    public Rect GetTotalGameArea()
    {
        if (mainCamera == null || !mainCamera.orthographic)
        {
            Debug.LogError("GetTotalGameArea: Nécessite une caméra principale orthographique assignée.");
            return new Rect(0, 0, 0, 0); // Retourne un Rect vide en cas d'erreur
        }

        // Pour une caméra orthographique :
        float screenHeightWorld = mainCamera.orthographicSize * 2f;
        float screenWidthWorld = screenHeightWorld * mainCamera.aspect;

        // Trouve le coin inférieur gauche en coordonnées monde
        // (Suppose que la caméra est centrée ou utilise sa position)
        Vector3 cameraBottomLeft = mainCamera.transform.position -
                                   new Vector3(screenWidthWorld / 2f, screenHeightWorld / 2f, 0);

        // Crée le Rect (x, y, largeur, hauteur)
        return new Rect(cameraBottomLeft.x, cameraBottomLeft.y, screenWidthWorld, screenHeightWorld);
    }



    void CheckBubbleCoverage()
    {
        // gameIsOver est déjà vérifié dans Update, mais une double vérif ne fait pas de mal
        if (gameIsOver)
            return;

        // Trouve toutes les bulles actives
        BulleShaderController[] bubbles = FindObjectsOfType<BulleShaderController>();
        float totalBubbleArea = 0f;

        // Calcule l'aire de chaque bulle
        foreach (BulleShaderController b in bubbles)
        {
            // --- MODIFICATION ICI ---
            Collider2D bubbleCollider = b.GetComponent<Collider2D>();
            if (bubbleCollider != null)
            {
                // bounds.size donne les dimensions du "bounding box" en unités du monde
                // Pour un cercle/une sphère, x et y devraient être (presque) égaux au diamètre
                float worldDiameter = bubbleCollider.bounds.size.x;
                float worldRadius = worldDiameter / 2f;
                float area = Mathf.PI * worldRadius * worldRadius;
                // Debug.Log($"Bubble: {b.gameObject.name}, Bounds.size.x: {worldDiameter:F2}, Area: {area:F2}"); // Log mis à jour
                totalBubbleArea += area;
            }
            else
            {
                // Optionnel : Gérer le cas où une bulle n'a pas de collider
                Debug.LogWarning($"Bubble {b.gameObject.name} has no Collider2D, cannot calculate its area accurately.");
                // Vous pourriez essayer d'utiliser le Renderer, mais le Collider est souvent plus précis pour la physique/zone
                // Renderer renderer = b.GetComponent<Renderer>();
                // if (renderer != null) { ... utiliser renderer.bounds.size.x ... }
            }
            // --- FIN MODIFICATION ---
        }
        //Debug.Log($"Found {bubbles.Length} active bubbles. Aire totale des bulles : {totalBubbleArea:F2}");

        // --- Utilise l'aire TOTALE de jeu ---
        Rect totalGameAreaRect = GetTotalGameArea();
        float totalPlayableArea = totalGameAreaRect.width * totalGameAreaRect.height;
        // --- Fin de la partie importante ---

        // Calcule le pourcentage (avec sécurité pour éviter division par zéro)
        if (totalPlayableArea > Mathf.Epsilon) // Utilise Epsilon pour comparer les flottants
        {
            coveragePercentage = totalBubbleArea / totalPlayableArea;
        }
        else
        {
            coveragePercentage = 0f;
            // Optionnel: Log si l'aire est nulle/trop petite
            // Debug.LogWarning("CheckBubbleCoverage: totalPlayableArea is zero or negative!");
        }

        // Limite le pourcentage entre 0 et 1 (au cas où, bien que > 1 soit le but de GameOver)
        coveragePercentage = Mathf.Clamp01(coveragePercentage);

        //Debug.Log($"Total Bubble Area: {totalBubbleArea:F2}, Total Playable Area: {totalPlayableArea:F2}, Coverage: {coveragePercentage * 100:F2}%");

        UpdateUI(); // Met à jour l'interface utilisateur si nécessaire

        // Vérifie si la limite est dépassée
        if (coveragePercentage >= txCouvertureMax)
        {
            GameOver();
        }
    }


    public void GameOver()
    {
        if (!gameIsOver)
        {
            gameIsOver = true;
            Debug.Log("Game Over !");
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
                freezeIndicator.SetActive(false);
                swipeIndicator.SetActive(false);
                
            Time.timeScale = 0f;

            if (AudioManager.Instance != null) // Utiliser le singleton directement
            {
                AudioManager.Instance.StopAllSounds();
            }
            else
            {
                Debug.LogWarning("GameOver: AudioManager.Instance non trouvé, impossible d'arrêter les sons.");
            }
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        // Recharger la scène actuelle est le moyen le plus simple de tout réinitialiser
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


    // --- NOUVELLES MÉTHODES POUR GÉRER LES MODES ---

    public void SetSwipeMode(bool activate, float duration = 0f)
    {
        // --- Gestion des états redondants ---
        if (activate && _isSwipeModeActive) // Déjà actif, on veut peut-être juste redémarrer/étendre ?
        {
            if (duration > 0) // Si une nouvelle durée est fournie, on redémarre
            {
                Debug.Log($"Swipe Mode déjà actif. Redémarrage du timer pour {duration}s.");
                if (swipeModeTimerCoroutine != null) StopCoroutine(swipeModeTimerCoroutine);

                currentSwipeDuration = duration; // Mettre à jour la durée
                swipeTimer = duration;           // Réinitialiser le temps restant
                swipeModeTimerCoroutine = StartCoroutine(SwipeTimerCoroutine()); // Relancer la coroutine

                // S'assurer que le slider est visuellement à fond
                if (swipeSlider != null) swipeSlider.value = 1f;
            }
            else
            {
                 Debug.Log("Swipe Mode déjà actif. Aucune nouvelle durée fournie.");
            }
            return; // Sortir car l'état de base ne change pas
        }
        if (!activate && !_isSwipeModeActive) // Déjà inactif
        {
            Debug.Log("Swipe Mode déjà inactif.");
            return; // Sortir
        }

        // --- Mise à jour de l'état principal ---
        _isSwipeModeActive = activate;
        Debug.Log($"GameManager: Swipe Mode global mis à {activate}");

        // --- Gestion de l'indicateur visuel (Panel) ---
        if (swipeIndicator != null)
        {
            swipeIndicator.SetActive(activate);
            Debug.Log($"GameManager: SwipeIndicator visibility set to {activate}");
        }
        else if (activate)
        {
            Debug.LogWarning("GameManager: swipeIndicator non assigné.");
        }

        // --- Arrêt de la coroutine précédente (si elle existe) ---
        if (activate)
        {
            if (duration > 0)
            {
                currentSwipeDuration = duration; // Stocker la durée totale
                swipeTimer = duration;           // Initialiser le temps restant
                swipeModeTimerCoroutine = StartCoroutine(SwipeTimerCoroutine()); // Démarrer le décompte

                // Initialiser le slider à 100%
                if (swipeSlider != null)
                {
                    swipeSlider.value = 1.0f;
                }
                else
                {
                    Debug.LogWarning("GameManager: swipeSlider non assigné. Le slider ne sera pas mis à jour.");
                }
            }
            else
            {
                Debug.LogError($"Tentative d'activation du Swipe Mode sans durée valide ({duration}s). Annulation.");
                _isSwipeModeActive = false; // Revenir à l'état inactif
                if (swipeIndicator != null) swipeIndicator.SetActive(false); // Cacher l'indicateur
            }
        }
        // --- Logique de Désactivation ---
        else
        {
            swipeTimer = 0f;           // Réinitialiser le temps
            currentSwipeDuration = 0f; // Réinitialiser la durée
            // Optionnel: Mettre le slider à 0% (même s'il est caché)
            if (swipeSlider != null)
            {
                swipeSlider.value = 0f;
            }
            // IMPORTANT : Notifier les bulles que le freeze est terminé !
            //NotifyBubblesOfSwipeEnd(); // Assure-toi que cette méthode existe
        }
    }


 
    public void SetFreezeMode(bool activate, float duration = 0f)
    {
        // --- Gestion des états redondants ---

        // CAS 1: On essaie d'activer un mode déjà actif
        if (activate && _isFreezeModeActive)
        {
            if (duration > 0) // Si une nouvelle durée est fournie, on redémarre/étend
            {
                Debug.Log($"Freeze Mode déjà actif. Redémarrage du timer pour {duration}s.");
                if (freezeModeTimerCoroutine != null) StopCoroutine(freezeModeTimerCoroutine);

                currentFreezeDuration = duration; // Mettre à jour la durée
                freezeTimer = duration;           // Réinitialiser le temps restant
                freezeModeTimerCoroutine = StartCoroutine(FreezeTimerCoroutine()); // Relancer la coroutine

                // S'assurer que le slider est visuellement à fond
                if (freezeSlider != null) freezeSlider.value = 1f;
            }
            else
            {
                 Debug.Log("Freeze Mode déjà actif. Aucune nouvelle durée fournie.");
            }
            return; // Sortir car l'état de base (_isFreezeModeActive) ne change pas
        }

        // CAS 2: On essaie de désactiver un mode déjà inactif
        if (!activate && !_isFreezeModeActive)
        {
            Debug.Log("Freeze Mode déjà inactif.");
            return; // Sortir car l'état de base (_isFreezeModeActive) ne change pas
        }

        // --- Si on arrive ici, c'est qu'on change réellement d'état (Actif -> Inactif ou Inactif -> Actif) ---


        _isFreezeModeActive = activate;
        Debug.Log($"GameManager: Freeze Mode global mis à {activate}");


        // --- Gestion de l'indicateur visuel (Panel) ---
        if (freezeIndicator != null)
        {
            freezeIndicator.SetActive(activate);
            Debug.Log($"GameManager: FreezeIndicator visibility set to {activate}");
        }
        else if (activate)
        {
            Debug.LogWarning("GameManager: freezeIndicator non assigné.");
        }

        // --- Arrêt de la coroutine précédente (si elle existe) ---
        if (freezeModeTimerCoroutine != null)
        {
            StopCoroutine(freezeModeTimerCoroutine);
            freezeModeTimerCoroutine = null;
        }

        // --- Logique d'Activation ---
        if (activate)
        {
            if (duration > 0)
            {
                currentFreezeDuration = duration; // Stocker la durée totale
                freezeTimer = duration;           // Initialiser le temps restant
                freezeModeTimerCoroutine = StartCoroutine(FreezeTimerCoroutine()); // Démarrer le décompte

                // Initialiser le slider à 100%
                if (freezeSlider != null)
                {
                    freezeSlider.value = 1.0f;
                }
                else
                {
                    Debug.LogWarning("GameManager: freezeSlider non assigné. Le slider ne sera pas mis à jour.");
                }
            }
            else
            {
                Debug.LogError($"Tentative d'activation du Freeze Mode sans durée valide ({duration}s). Annulation.");
                _isFreezeModeActive = false; // Revenir à l'état inactif
                if (freezeIndicator != null) freezeIndicator.SetActive(false); // Cacher l'indicateur
            }
        }
        // --- Logique de Désactivation ---
        else
        {
            freezeTimer = 0f;           // Réinitialiser le temps
            currentFreezeDuration = 0f; // Réinitialiser la durée
            // Optionnel: Mettre le slider à 0% (même s'il est caché)
            if (freezeSlider != null)
            {
                freezeSlider.value = 0f;
            }
            // IMPORTANT : Notifier les bulles que le freeze est terminé !
            //NotifyBubblesOfFreezeEnd(); // Assure-toi que cette méthode existe
        }
    }

    private IEnumerator FreezeTimerCoroutine()
    {
        Debug.Log($"Freeze Coroutine démarrée. Durée: {currentFreezeDuration}s");
        // Boucle tant qu'il reste du temps
        while (freezeTimer > 0)
        {
            freezeTimer -= Time.deltaTime;
            yield return null; // Attendre la prochaine frame
        }

        // Le temps est écoulé
        freezeTimer = 0; // Assurer la valeur exacte
        Debug.Log("Freeze Coroutine terminée (temps écoulé).");

        // Appeler SetFreezeMode(false) pour désactiver proprement
        SetFreezeMode(false);

        freezeModeTimerCoroutine = null; // Marquer la coroutine comme terminée
    }

    private IEnumerator SwipeTimerCoroutine()
    {
        Debug.Log($"Swipe Coroutine démarrée. Durée: {currentSwipeDuration}s");
        // Boucle tant qu'il reste du temps
        while (swipeTimer > 0)
        {
            swipeTimer -= Time.deltaTime;
            yield return null; // Attendre la prochaine frame
        }

        // Le temps est écoulé
        swipeTimer = 0; // Assurer la valeur exacte
        Debug.Log("Swipe Coroutine terminée (temps écoulé).");

        // Appeler SetFreezeMode(false) pour désactiver proprement
        SetSwipeMode(false);

        swipeModeTimerCoroutine = null; // Marquer la coroutine comme terminée
    }

    /// <summary>
    /// Coroutine générique pour attendre une durée puis exécuter une action.
    /// </summary>
    /// <param name="delay">Temps d'attente en secondes.</param>
    /// <param name="onComplete">Action à exécuter à la fin du délai.</param>
    private IEnumerator ModeTimerCoroutine(float delay, System.Action onComplete)
    {
        yield return new WaitForSeconds(delay);
        onComplete?.Invoke(); // Exécute l'action fournie (ex: SetSwipeMode(false))
    }



}
