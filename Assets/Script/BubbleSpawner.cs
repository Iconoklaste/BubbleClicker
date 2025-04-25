using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Configuration du Spawner")]
    [Tooltip("Prefab de la bulle � faire appara�tre.")]
    public GameObject bubblePrefab;

    [Tooltip("Espace horizontal de spawn (en unit�s Unity).")]
    public float spawnWidth = 8f;


    [Header("Ajustement de difficult�")]
    [Tooltip("Intervalle de temps entre chaque spawn.")]
    public float spawnInterval = 1.5f;
    [Tooltip("R�duction de l'intervalle de spawn au fil du temps.")]
    public float spawnAcceleration = 0.01f;
    [Tooltip("Intervalle minimal entre les spawns.")]
    public float minSpawnInterval = 0.5f;

    private float timer;
    private bool isSpawning = true;
    private AudioManager audioManagerInstance;

    public enum BubbleType { Normal, Swipe, Explosive, Freeze }

    void Awake()
    {
        // Récupère l'instance de l'AudioManager (grâce au Singleton)
        audioManagerInstance = AudioManager.Instance;

        // Optionnel : Vérification pour s'assurer que l'AudioManager a été trouvé
        if (audioManagerInstance == null)
        {
            Debug.LogError("BubbleSpawner: AudioManager non trouvé dans la scène ! Assurez-vous qu'un GameObject avec le script AudioManager existe.");
        }
    }

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
            Debug.LogWarning("BubblePrefab n'est pas assignée dans le BubbleSpawner.");
            return;
        }

        // --- 1. Calculer la position de spawn ---
        float randomX = Random.Range(-spawnWidth / 2, spawnWidth / 2);
        Vector2 spawnPosition = new Vector2(transform.position.x + randomX, transform.position.y); // Ajuste selon où est ton spawner

        // --- 2. Instancier la bulle ---
        GameObject newBubbleGO = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);
        if (audioManagerInstance != null)
        {
            // Utilise l'AudioType.BubbleSpawn et choisis la source (Game semble approprié)
            audioManagerInstance.PlaySound(AudioType.BubbleSpawn, AudioSourceType.Game);
        }
        

        // --- 3. Récupérer le script BulleShaderController ---
        //    IMPORTANT : Assure-toi que ton prefab utilise bien BulleShaderController
        //    et non BullePhysique si tu utilises Shader Graph.
        BulleShaderController bubbleController = newBubbleGO.GetComponent<BulleShaderController>();

        // --- 4. Vérifier si le script a été trouvé ---
        if (bubbleController != null)
        {
            // 5. Déterminer le type de la bulle
            BubbleType bubbleType = GetRandomBubbleType(); // Utilise ta fonction existante

            // 6. Appeler SetBubbleType sur le script de la NOUVELLE bulle
            //    Ceci mettra à jour le type ET appliquera la couleur/texture via ApplyBubbleTypeColor()
            bubbleController.SetBubbleType(bubbleType);

            // Optionnel : Tu peux initialiser d'autres valeurs ici si besoin
            // bubbleController.generation = 0; // (Bien que 0 soit souvent la valeur par défaut)
        }
        else
        {
            // Si le script n'est pas trouvé, c'est probablement que le mauvais prefab est assigné
            Debug.LogError("Le prefab de bulle assigné au Spawner n'a pas le script BulleShaderController!", newBubbleGO);
        }
    }

    // Assure-toi que cette fonction est bien dans ta classe BubbleSpawner
    // et que l'enum BubbleType est accessible (définie ici ou dans un autre fichier)
    public BubbleType GetRandomBubbleType()
    {
        float chance = Random.value; // Génère un nombre aléatoire entre 0.0 et 1.0

        // 50% de chance d'avoir une bulle spéciale (si chance < 0.15)
        if (chance < 0.15f)
        {
            float specialChance = Random.value; // Nouveau nombre aléatoire pour choisir le type spécial

            // Répartition équitable entre les 3 types spéciaux
            if (specialChance < 0.33f)
            { // Début du bloc pour Swipe
                Debug.Log("GetRandomBubbleType: Chosen Type -> Swipe"); // Utilisation de Debug.Log
                return BubbleType.Swipe;
            } // Fin du bloc pour Swipe
            else if (specialChance < 0.67f) // Entre 0.33 et 0.66
            { // Début du bloc pour Explosive
                Debug.Log("GetRandomBubbleType: Chosen Type -> Explosive");
                return BubbleType.Explosive;
            } // Fin du bloc pour Explosive
            else // >= 0.67
            { // Début du bloc pour Freeze
                Debug.Log("GetRandomBubbleType: Chosen Type -> Freeze");
                return BubbleType.Freeze;
            } // Fin du bloc pour Freeze
        }
        else // 50% de chance d'avoir une bulle normale (si chance >= 0.50)
        {
            Debug.Log("GetRandomBubbleType: Chosen Type -> Normal");
            return BubbleType.Normal;
        }
    }


    public void RestartSpawner()
    {
        isSpawning = true;
        timer = 0f;
        spawnInterval = minSpawnInterval;
    }
}
