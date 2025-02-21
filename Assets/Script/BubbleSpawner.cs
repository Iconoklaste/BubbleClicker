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

    public enum BubbleType { Normal, Swipe, Explosive, Freeze }

    private void Start()
    {
        SpawnBubble();
    }

    void Update()
    {
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

        float randomX = Random.Range(-spawnWidth / 2, spawnWidth / 2);
        Vector2 spawnPosition = new Vector2(randomX, spawnY);

        GameObject newBubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);

        BubbleType bubbleType = GetRandomBubbleType(); // Nouvelle logique pour obtenir un type aléatoire

        BullePhysique bubblePhysique = newBubble.GetComponent<BullePhysique>();
        if (bubblePhysique != null)
        {
            bubblePhysique.SetBubbleType(bubbleType);
        }
    }

    public BubbleType GetRandomBubbleType()
    {
        float chance = Random.value;

        if (chance < 0.15f) // 15% de chance d'avoir une bulle spéciale
        {
            float specialChance = Random.value;

            if (specialChance < 0.33f)
                return BubbleType.Swipe;
            else if (specialChance < 0.67f)
                return BubbleType.Explosive;
            else
                return BubbleType.Freeze;
        }

        return BubbleType.Normal;
    }

    public void RestartSpawner()
    {
        isSpawning = true;
        timer = 0f;
        spawnInterval = 1.5f;
    }
}
