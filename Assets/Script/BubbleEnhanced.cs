using UnityEngine;

public class BubbleEnhanced : MonoBehaviour
{
    [Header("Paramètres de déplacement et de points")]
    [Tooltip("Vitesse de montée de la bulle.")]
    public float speed = 2.0f;
    [Tooltip("Points attribués lors du clic sur la bulle.")]
    public int points = 10;

    [Header("Gestion des Générations")]
    [Tooltip("Génération actuelle de la bulle (0 pour la génération initiale).")]
    public int generation = 0;
    [Tooltip("Nombre maximal de générations autorisées.")]
    public int maxGenerations = 4;

    [Header("Bulles enfants")]
    [Tooltip("Prefab de la bulle à instancier lors de l'éclatement.")]
    public GameObject bubblePrefab;
    [Tooltip("Nombre de bulles enfants à créer lors de l'éclatement.")]
    public int numberOfChildBubbles = 3;
    [Tooltip("Facteur de réduction d'échelle pour les bulles enfants.")]
    [Range(0.1f, 1f)]
    public float childScaleFactor = 0.5f;
    [Tooltip("Force appliquée pour disperser les bulles enfants.")]
    public float explosionForce = 100f;
    [Tooltip("Rayon d'offset aléatoire pour le spawn des bulles enfants.")]
    public float spawnOffsetRadius = 0.2f;

    [Header("Effets visuels et audio")]
    [Tooltip("Système de particules pour simuler une explosion organique.")]
    public ParticleSystem organicPopEffect;
    [Tooltip("Effet sonore lors du pop.")]
    public AudioClip popSound;

    private AudioSource audioSource;
    private Animator animator;
    private bool isPopped = false;

    void Awake()
    {
        // Configuration de l'AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && popSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Récupération de l'Animator pour la déformation
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (!isPopped)
        {
            // Déplacement de la bulle vers le haut
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        // Détruire la bulle si elle sort de l'écran (en supposant une caméra orthographique)
        if (transform.position.y > Camera.main.orthographicSize + 1f)
        {
            Destroy(gameObject);
        }
    }

    void OnMouseDown()
    {
        if (isPopped) return; // Empêcher un double clic
        isPopped = true;

        // Debug : affichage de la position
        Debug.Log("Bulle cliquée à : " + transform.position);

        // Jouer le son du pop
        if (audioSource != null && popSound != null)
        {
            audioSource.PlayOneShot(popSound);
        }

        // Déclencher l'animation de pop/deformation
        if (animator != null)
        {
            animator.SetTrigger("Pop");
        }

        // Laisser le temps à l'animation de jouer avant d'exécuter l'explosion organique
        Invoke("PopBubble", 0.5f); // Le délai doit correspondre à la durée de l'animation de pop
    }

    void PopBubble()
    {
        // Lancer l'effet organique d'explosion
        if (organicPopEffect != null)
        {
            ParticleSystem effect = Instantiate(organicPopEffect, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration);
        }

        // Ajouter les points via le GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(points);
        }

        // Générer les bulles enfants si la génération courante est inférieure au maximum autorisé
        if (bubblePrefab != null && numberOfChildBubbles > 0 && generation < maxGenerations)
        {
            for (int i = 0; i < numberOfChildBubbles; i++)
            {
                // Appliquer un léger offset pour éviter que les bulles ne se superposent exactement
                Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
                Vector2 spawnPos = (Vector2)transform.position + randomOffset;
                GameObject childBubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);
                childBubble.transform.localScale = transform.localScale * childScaleFactor;

                // Transmettre la génération incrémentée à la bulle enfant
                BubbleEnhanced childScript = childBubble.GetComponent<BubbleEnhanced>();
                if (childScript != null)
                {
                    childScript.generation = generation + 1;
                }

                // Si un Rigidbody2D est présent, appliquer une impulsion aléatoire
                Rigidbody2D childRb = childBubble.GetComponent<Rigidbody2D>();
                if (childRb != null)
                {
                    childRb.gravityScale = 0;
                    Vector2 randomDirection = Random.insideUnitCircle.normalized;
                    childRb.AddForce(randomDirection * explosionForce);
                }
            }
        }

        // Détruire la bulle une fois l'animation terminée
        Destroy(gameObject);
    }
}
