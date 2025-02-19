using UnityEngine;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Configuration du Spawner")]
    [Tooltip("Prefab de la bulle à faire apparaître.")]
    public GameObject bubblePrefab;
    [Tooltip("Intervalle de temps entre chaque spawn.")]
    public float spawnInterval = 1.5f;
    [Tooltip("Espace horizontal de spawn (en unités Unity).")]
    public float spawnWidth = 8f;
    [Tooltip("Position Y fixe de spawn (typiquement en bas de l'écran).")]
    public float spawnY = -5f;

    [Header("Ajustement de difficulté")]
    [Tooltip("Réduction de l'intervalle de spawn au fil du temps.")]
    public float spawnAcceleration = 0.01f;
    [Tooltip("Intervalle minimal entre les spawns.")]
    public float minSpawnInterval = 0.5f;

    private float timer;
    private bool isSpawning = true;

    void Update()
    {
        // Vérifier si le jeu est terminé avant de continuer
        if (GameManager.Instance != null && GameManager.Instance.gameIsOver)
        {
            isSpawning = false;
            return;
        }
        else
        {
            isSpawning = true;
        }

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnBubble();
            timer = 0f;
            spawnInterval = Mathf.Max(spawnInterval - spawnAcceleration, minSpawnInterval);
        }
    }


    void SpawnBubble()
    {
        if (bubblePrefab == null)
        {
            Debug.LogWarning("BubblePrefab n'est pas assigné dans le spawner.");
            return;
        }

        // Calcul d'une position X aléatoire dans la zone de spawn
        float randomX = Random.Range(-spawnWidth / 2, spawnWidth / 2);
        Vector2 spawnPosition = new Vector2(randomX, spawnY);

        // Instanciation de la bulle
        Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);
    }

    public void RestartSpawner()
    {
        isSpawning = true;
        timer = 0f;
        spawnInterval = 1.5f; // Réinitialiser à l'intervalle de départ
    }
}
