using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;




public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Elements")]
    public GameObject gameOverPanel;
    public Text coverageText;
    public Text scoreText;
    public Camera mainCamera;

    [Header("Game Variables")]
    public bool gameIsOver = false;
    public float txCouvertureMax = 0.6f;
    private int score = 0;
    // Ce champ stocke le taux de couverture calcul� (par rapport � la zone confin�e)
    private float coveragePercentage = 0f;

    [Header("Confinement")]
    // Marge � appliquer sur les bords (10% par d�faut)
    public float confinementMargin = 0.1f;

    [Header("Spawner Reference")]
    public BubbleSpawner bubbleSpawner;

    // Ces bool�ens pourront �tre utilis�s par d'autres scripts pour adapter le gameplay
    public bool swipeModeActive = false;
    public bool freezeModeActive = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
    }


    void Start()
    {
        Time.timeScale = 1f; // Le jeu d�marre actif
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        gameIsOver = false;
        score = 0;
        coveragePercentage = 0f;
        UpdateUI();
    }

    void FixedUpdate()
    {
        // Appelle CheckBubbleCoverage ici, au rythme de la physique
        if (!gameIsOver)
        {
            CheckBubbleCoverage();
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

    void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }

        if (coverageText != null)
        {
            // Convertir le taux de couverture en pourcentage
            // Ici, on consid�re que lorsque coveragePercentage atteint txCouvertureMax, c'est 100% affich�
            float displayedPercentage = Mathf.Clamp((coveragePercentage) * 100f, 0f, 100f);
            coverageText.text = "Couverture: " + displayedPercentage.ToString("F1") + "%";
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
        Debug.Log($"Found {bubbles.Length} active bubbles. Aire totale des bulles : {totalBubbleArea:F2}");

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

        Debug.Log($"Total Bubble Area: {totalBubbleArea:F2}, Total Playable Area: {totalPlayableArea:F2}, Coverage: {coveragePercentage * 100:F2}%");

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
            Time.timeScale = 0f;
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        gameIsOver = false;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        score = 0;
        coveragePercentage = 0f;
        UpdateUI();

        // D�truire toutes les bulles pr�sentes en sc�ne (assurez-vous qu'elles portent le tag "Bubble")
        GameObject[] bubbles = GameObject.FindGameObjectsWithTag("Bubble");
        foreach (GameObject bubble in bubbles)
        {
            Destroy(bubble);
        }

        // R�initialiser le spawner
        if (bubbleSpawner != null)
        {
            bubbleSpawner.RestartSpawner();
        }
    }

    public void ActivateSwipeMode(float duration)
    {
        Debug.Log("Activation du mode Swipe pour " + duration + " secondes.");
        StartCoroutine(SwipeModeCoroutine(duration));
    }

    private IEnumerator SwipeModeCoroutine(float duration)
    {
        swipeModeActive = true;
        // Ici, on peut ajouter le code pour activer les effets visuels ou logiques du mode Swipe.
        yield return new WaitForSeconds(duration);
        swipeModeActive = false;
        Debug.Log("Mode Swipe d�sactiv�.");
    }

    // M�thode d'activation du mode Freeze
    public void ActivateFreezeMode(float duration)
    {
        Debug.Log("Activation du mode Freeze pour " + duration + " secondes.");
        StartCoroutine(FreezeModeCoroutine(duration));
    }

    private IEnumerator FreezeModeCoroutine(float duration)
    {
        freezeModeActive = true;
        // Par exemple, vous pouvez ralentir le temps de jeu pour les bulles (mais attention Time.timeScale affecte tout)
        // Ou bien, appliquer un multiplicateur sur la vitesse de d�placement des bulles
        // Ici on simule simplement l'effet par un Debug.Log
        yield return new WaitForSeconds(duration);
        freezeModeActive = false;
        Debug.Log("Mode Freeze d�sactiv�.");
    }

}
