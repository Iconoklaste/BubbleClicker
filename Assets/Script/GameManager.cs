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
            Instance = this;
        else
            Destroy(gameObject);
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

    void Update()
    {
        // Mettre � jour en continu le taux de couverture si le jeu n'est pas termin�
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
            float displayedPercentage = Mathf.Clamp((coveragePercentage / txCouvertureMax) * 100f, 0f, 100f);
            coverageText.text = "Couverture: " + displayedPercentage.ToString("F1") + "%";
        }
    }

    void CheckBubbleCoverage()
    {
        // Si le jeu est termin�, ne pas recalculer
        if (gameIsOver)
            return;

        // Calculer l'aire totale occup�e par les bulles
        Bubble[] bubbles = FindObjectsOfType<Bubble>();
        float totalBubbleArea = 0f;
        foreach (Bubble b in bubbles)
        {
            float diameter = b.transform.localScale.x;
            float radius = diameter / 2f;
            totalBubbleArea += Mathf.PI * radius * radius;
        }

        // Calculer la taille de l'�cran en unit�s monde (pour une cam�ra orthographique)
        float screenHeight = Camera.main.orthographicSize * 2f;
        float screenWidth = screenHeight * Camera.main.aspect;

        // D�finir la zone confin�e en appliquant la marge sur chaque c�t�
        float confinedWidth = screenWidth * (1 - 2 * confinementMargin);
        float confinedHeight = screenHeight * (1 - 2 * confinementMargin);
        float confinedArea = confinedWidth * confinedHeight;

        // Calcul du taux de couverture par rapport � la zone confin�e
        coveragePercentage = totalBubbleArea / confinedArea;

        // Mise � jour de l'UI
        UpdateUI();

        // Si le taux de couverture d�passe txCouvertureMax (ce qui correspond � 100% affich�), terminer la partie
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
