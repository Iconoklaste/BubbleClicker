using UnityEngine;

public class BubbleEnhanced : MonoBehaviour
{
    [Header("Param�tres de d�placement et de points")]
    [Tooltip("Vitesse de mont�e de la bulle.")]
    public float speed = 2.0f;
    [Tooltip("Points attribu�s lors du clic sur la bulle.")]
    public int points = 10;

    [Header("Gestion des G�n�rations")]
    [Tooltip("G�n�ration actuelle de la bulle (0 pour la g�n�ration initiale).")]
    public int generation = 0;
    [Tooltip("Nombre maximal de g�n�rations autoris�es.")]
    public int maxGenerations = 4;

    [Header("Bulles enfants")]
    [Tooltip("Prefab de la bulle � instancier lors de l'�clatement.")]
    public GameObject bubblePrefab;
    [Tooltip("Nombre de bulles enfants � cr�er lors de l'�clatement.")]
    public int numberOfChildBubbles = 3;
    [Tooltip("Facteur de r�duction d'�chelle pour les bulles enfants.")]
    [Range(0.1f, 1f)]
    public float childScaleFactor = 0.5f;
    [Tooltip("Force appliqu�e pour disperser les bulles enfants.")]
    public float explosionForce = 100f;
    [Tooltip("Rayon d'offset al�atoire pour le spawn des bulles enfants.")]
    public float spawnOffsetRadius = 0.2f;

    [Header("Effets visuels et audio")]
    [Tooltip("Syst�me de particules pour simuler une explosion organique.")]
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

        // R�cup�ration de l'Animator pour la d�formation
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (!isPopped)
        {
            // D�placement de la bulle vers le haut
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        // D�truire la bulle si elle sort de l'�cran (en supposant une cam�ra orthographique)
        if (transform.position.y > Camera.main.orthographicSize + 1f)
        {
            Destroy(gameObject);
        }
    }

    void OnMouseDown()
    {
        if (isPopped) return; // Emp�cher un double clic
        isPopped = true;

        // Debug : affichage de la position
        Debug.Log("Bulle cliqu�e � : " + transform.position);

        // Jouer le son du pop
        if (audioSource != null && popSound != null)
        {
            audioSource.PlayOneShot(popSound);
        }

        // D�clencher l'animation de pop/deformation
        if (animator != null)
        {
            animator.SetTrigger("Pop");
        }

        // Laisser le temps � l'animation de jouer avant d'ex�cuter l'explosion organique
        Invoke("PopBubble", 0.5f); // Le d�lai doit correspondre � la dur�e de l'animation de pop
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

        // G�n�rer les bulles enfants si la g�n�ration courante est inf�rieure au maximum autoris�
        if (bubblePrefab != null && numberOfChildBubbles > 0 && generation < maxGenerations)
        {
            for (int i = 0; i < numberOfChildBubbles; i++)
            {
                // Appliquer un l�ger offset pour �viter que les bulles ne se superposent exactement
                Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
                Vector2 spawnPos = (Vector2)transform.position + randomOffset;
                GameObject childBubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);
                childBubble.transform.localScale = transform.localScale * childScaleFactor;

                // Transmettre la g�n�ration incr�ment�e � la bulle enfant
                BubbleEnhanced childScript = childBubble.GetComponent<BubbleEnhanced>();
                if (childScript != null)
                {
                    childScript.generation = generation + 1;
                }

                // Si un Rigidbody2D est pr�sent, appliquer une impulsion al�atoire
                Rigidbody2D childRb = childBubble.GetComponent<Rigidbody2D>();
                if (childRb != null)
                {
                    childRb.gravityScale = 0;
                    Vector2 randomDirection = Random.insideUnitCircle.normalized;
                    childRb.AddForce(randomDirection * explosionForce);
                }
            }
        }

        // D�truire la bulle une fois l'animation termin�e
        Destroy(gameObject);
    }
}
