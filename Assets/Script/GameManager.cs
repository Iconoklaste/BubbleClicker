using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    // Ce champ stocke le taux de couverture calculé (par rapport à la zone confinée)
    private float coveragePercentage = 0f;

    [Header("Confinement")]
    // Marge à appliquer sur les bords (10% par défaut)
    public float confinementMargin = 0.1f;

    [Header("Spawner Reference")]
    public BubbleSpawner bubbleSpawner;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        Time.timeScale = 1f; // Le jeu démarre actif
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        gameIsOver = false;
        score = 0;
        coveragePercentage = 0f;
        UpdateUI();
    }

    void Update()
    {
        // Mettre à jour en continu le taux de couverture si le jeu n'est pas terminé
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
            // Ici, on considère que lorsque coveragePercentage atteint txCouvertureMax, c'est 100% affiché
            float displayedPercentage = Mathf.Clamp((coveragePercentage / txCouvertureMax) * 100f, 0f, 100f);
            coverageText.text = "Couverture: " + displayedPercentage.ToString("F1") + "%";
        }
    }

    void CheckBubbleCoverage()
    {
        // Si le jeu est terminé, ne pas recalculer
        if (gameIsOver)
            return;

        // Calculer l'aire totale occupée par les bulles
        Bubble[] bubbles = FindObjectsOfType<Bubble>();
        float totalBubbleArea = 0f;
        foreach (Bubble b in bubbles)
        {
            float diameter = b.transform.localScale.x;
            float radius = diameter / 2f;
            totalBubbleArea += Mathf.PI * radius * radius;
        }

        // Calculer la taille de l'écran en unités monde (pour une caméra orthographique)
        float screenHeight = Camera.main.orthographicSize * 2f;
        float screenWidth = screenHeight * Camera.main.aspect;

        // Définir la zone confinée en appliquant la marge sur chaque côté
        float confinedWidth = screenWidth * (1 - 2 * confinementMargin);
        float confinedHeight = screenHeight * (1 - 2 * confinementMargin);
        float confinedArea = confinedWidth * confinedHeight;

        // Calcul du taux de couverture par rapport à la zone confinée
        coveragePercentage = totalBubbleArea / confinedArea;

        // Mise à jour de l'UI
        UpdateUI();

        // Si le taux de couverture dépasse txCouvertureMax (ce qui correspond à 100% affiché), terminer la partie
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

        // Détruire toutes les bulles présentes en scène (assurez-vous qu'elles portent le tag "Bubble")
        GameObject[] bubbles = GameObject.FindGameObjectsWithTag("Bubble");
        foreach (GameObject bubble in bubbles)
        {
            Destroy(bubble);
        }

        // Réinitialiser le spawner
        if (bubbleSpawner != null)
        {
            bubbleSpawner.RestartSpawner();
        }
    }
}
