using DG.Tweening;
using UnityEngine;

public class Bubble : MonoBehaviour
{
    [Header("Paramètres de déplacement et de points")]
    [Tooltip("Vitesse de montée de la bulle.")]
    public float speed = 2.0f;
    [Tooltip("Points de base attribués lors du clic sur la bulle.")]
    public int basePoints = 10;

    [Header("Croissance de la bulle")]
    [Tooltip("Vitesse de croissance de la bulle au fil du temps.")]
    public float growthRate = 0.1f;

    [Header("Génération de Bulles")]
    [Tooltip("Génération actuelle de la bulle (0 pour la bulle initiale).")]
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
    [Tooltip("Force appliquée pour disperser les bulles enfants (si Rigidbody2D est utilisé).")]
    public float explosionForce = 100f;
    [Tooltip("Rayon d'offset aléatoire pour le spawn des bulles enfants.")]
    public float spawnOffsetRadius = 0.2f;

    [Header("Effets visuels et audio")]
    [Tooltip("Système de particules pour simuler l'éclatement (optionnel).")]
    public ParticleSystem popEffect;
    // Note : on ne garde plus l'AudioClip ici car on passe par l'AudioManager.

    private Rigidbody2D rb;

    // animation DOTween
    public float shrinkDuration = 1f; // Durée de contraction pour chaque axe
    public float growDuration = 0.5f;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0; // Désactiver la gravité pour un déplacement contrôlé.
        }
    }

    void Start()
    {
        AnimateBubble();
    }

    void Update()
    {
        // Croissance et déplacement de la bulle (déjà existants)
        // transform.localScale += Vector3.one * growthRate * Time.deltaTime;

        if (rb != null)
        {
            rb.velocity = Vector2.up * speed;
        }
        else
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        // Contraindre le centroïde de la bulle à rester dans l'écran
        ClampPositionToScreen();

        // Détruire la bulle si elle dépasse le haut de l'écran (facultatif si la contrainte est appliquée)
        if (transform.position.y > Camera.main.orthographicSize + 1f)
        {
            Destroy(gameObject);
        }
    }

    void OnMouseDown()
    {
        // Vérifier si le jeu est terminé avant de procéder
        if (GameManager.Instance != null && GameManager.Instance.gameIsOver)
        {
            return;
        }

        Debug.Log("Bulle cliquée à : " + transform.position);

        // Calcul du score basé sur la génération.
        int scoreAward = basePoints * (generation + 1);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreAward);
        }

        // Choix du son à jouer selon la génération.
        if (generation == 0)
            AudioManager.Instance.PlaySound(AudioType.Pop, AudioSourceType.Player);
        else if (generation == 1)
            AudioManager.Instance.PlaySound(AudioType.Swipe, AudioSourceType.Player);
        else
            AudioManager.Instance.PlaySound(AudioType.Dead, AudioSourceType.Player);

        // Instancier et jouer l'effet de particules (explosion organique).
        if (popEffect != null)
        {
            ParticleSystem effect = Instantiate(popEffect, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration);
        }

        // Générer des bulles enfants si la génération est inférieure au maximum autorisé.
        if (bubblePrefab != null && numberOfChildBubbles > 0 && generation < maxGenerations)
        {
            for (int i = 0; i < numberOfChildBubbles; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
                Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
                GameObject childBubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);
                childBubble.transform.localScale = transform.localScale * childScaleFactor;

                Bubble childBubbleScript = childBubble.GetComponent<Bubble>();
                if (childBubbleScript != null)
                {
                    childBubbleScript.generation = generation + 1;
                    childBubbleScript.growthRate = growthRate;
                }

                Rigidbody2D childRb = childBubble.GetComponent<Rigidbody2D>();
                if (childRb != null)
                {
                    childRb.gravityScale = 0;
                    Vector2 randomDirection = Random.insideUnitCircle.normalized;
                    childRb.AddForce(randomDirection * explosionForce);
                }
            }
        }

        // Détruire la bulle originale.
        DOTween.Kill(transform, true);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        DOTween.Kill(transform, true);
        Debug.Log("OnDestroy appelé");

    }


    void ClampPositionToScreen()
    {
        // Convertir la position de la bulle en coordonnées Viewport (0 à 1)
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

        // Clamp x et y entre 0 et 1 pour que le centre reste à l'intérieur de l'écran
        viewportPos.x = Mathf.Clamp(viewportPos.x, 0f, 1f);
        viewportPos.y = Mathf.Clamp(viewportPos.y, 0f, 1f);

        // Convertir de nouveau en coordonnées Monde
        transform.position = Camera.main.ViewportToWorldPoint(viewportPos);
    }

    void AnimateBubble()
    {
        if (this == null || transform == null) return;
        Sequence seq = DOTween.Sequence();

        // Contraction sur X, expansion sur Y
        seq.Append(transform.DOScaleX(0.95f, shrinkDuration).SetEase(Ease.InOutQuad))
           .Join(transform.DOScaleY(1.05f, shrinkDuration).SetEase(Ease.InOutQuad));

        // Expansion sur X, contraction sur Y
        seq.Append(transform.DOScaleX(1.05f, growDuration).SetEase(Ease.InOutQuad))
           .Join(transform.DOScaleY(0.95f, growDuration).SetEase(Ease.InOutQuad));

        // Boucle infinie pour répéter l'effet
        seq.SetLoops(-1, LoopType.Yoyo);

        // Ajouter la croissance continue via DOTween
        DOTween.To(() => transform.localScale,
                   x => transform.localScale = x,
                   transform.localScale * growthRate, // Taille max sur la durée de vie
                   5f) // Durée totale de la montée
              .SetEase(Ease.Linear)
              .SetLoops(-1, LoopType.Incremental); // Boucle pour un agrandissement progressif
    }



}
