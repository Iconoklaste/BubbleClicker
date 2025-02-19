using DG.Tweening;
using UnityEngine;

public enum BubbleType { Normal, Swipe, Explosive, Freeze }

public class Bubble : MonoBehaviour

{
    [Header("Type de bulles")]
    [Tooltip("Type de bulle (Normal, Swipe, Explosive, Freeze)")]
    public BubbleType bubbleType = BubbleType.Normal;

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

    [Header("Animation DOTween des bulles")]
    [Tooltip("Durée de contraction sur X")]
    public float shrinkDuration = 1f; 
    [Tooltip("Durée de contraction sur Y")]
    public float growDuration = 0.5f;


    private Rigidbody2D rb;

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
        if (Random.value < 0.15f) // 15% de chance d'avoir une bulle spéciale
        {
            int randomType = Random.Range(1, 4); // 1 = Swipe, 2 = Explosion, 3 = Freeze
            bubbleType = (BubbleType)randomType;
        }
        else
        {
            bubbleType = BubbleType.Normal;
        }

        SetBubbleAppearance();

        AnimateBubble();
    }

    void Update()
    {
        // Croissance et déplacement de la bulle (déjà existants)
        //transform.localScale += Vector3.one * growthRate * Time.deltaTime;


        // Déplacement vers le haut (via la physique ou la translation)
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
        // Ne rien faire si le jeu est terminé
        if (GameManager.Instance != null && GameManager.Instance.gameIsOver)
            return;

        Debug.Log("Bulle cliquée à : " + transform.position);

        // Selon le type de bulle, exécuter le pouvoir spécial
        switch (bubbleType)
        {
            case BubbleType.Normal:
                HandleNormalBubble();
                break;
            case BubbleType.Swipe:
                // Active le mode swipe pour 3 secondes dans le GameManager
                GameManager.Instance.ActivateSwipeMode(3f);
                break;
            case BubbleType.Explosive:
                HandleExplosiveBubble();
                break;
            case BubbleType.Freeze:
                // Active le mode freeze (ralentissement) pour 3 secondes
                GameManager.Instance.ActivateFreezeMode(3f);
                break;
        }

        // Jouer l'effet de particules (ex: explosion) s'il est défini
        if (popEffect != null)
        {
            ParticleSystem effect = Instantiate(popEffect, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration);
        }

        // Stopper toutes les animations DOTween sur ce transform
        DOTween.Kill(transform, true);

        // Pour les bulles normales, on génère les enfants (les bulles spéciales ne spawnent pas d'enfants)
        if (bubbleType == BubbleType.Normal && bubblePrefab != null && numberOfChildBubbles > 0 && generation < maxGenerations)
        {
            for (int i = 0; i < numberOfChildBubbles; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
                Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
                GameObject childBubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);
                // Appliquer une réduction d'échelle aux enfants
                childBubble.transform.localScale = transform.localScale * childScaleFactor;

                Bubble childBubbleScript = childBubble.GetComponent<Bubble>();
                if (childBubbleScript != null)
                {
                    childBubbleScript.generation = generation + 1;
                    childBubbleScript.growthRate = growthRate;
                    childBubbleScript.bubbleType = BubbleType.Normal; // Toujours une bulle normale
                    childBubbleScript.SetBubbleAppearance(); // Met à jour l'apparence en conséquence
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

        // Détruire la bulle (elle disparaît après activation du pouvoir)
        Destroy(gameObject);
    }

    // Gestion de la bulle normale : ajoute le score et joue le son approprié
    void HandleNormalBubble()
    {
        int scoreAward = basePoints * (generation + 1);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreAward);
        }

        // Jouer le son selon la génération
        if (generation == 0)
            AudioManager.Instance.PlaySound(AudioType.Pop, AudioSourceType.Player);
        else if (generation == 1)
            AudioManager.Instance.PlaySound(AudioType.Swipe, AudioSourceType.Player);
        else
            AudioManager.Instance.PlaySound(AudioType.Dead, AudioSourceType.Player);
    }

    // Gestion de la bulle explosive : détruit les bulles dans un rayon donné
    void HandleExplosiveBubble()
    {
        float explosionRadius = 2f; // Rayon d’explosion

        // Animation : Grossit rapidement avant d'exploser
        transform.DOScale(Vector3.one * explosionRadius, 0.3f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // Détruit les bulles autour après l'agrandissement
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                foreach (Collider2D col in colliders)
                {
                    Bubble bubble = col.GetComponent<Bubble>();
                    if (bubble != null && bubble != this)
                    {
                        Destroy(bubble.gameObject);
                    }
                }

                // Effet d'explosion (ajoute un effet visuel si disponible)
                if (popEffect != null)
                {
                    ParticleSystem effect = Instantiate(popEffect, transform.position, Quaternion.identity);
                    effect.Play();
                    Destroy(effect.gameObject, effect.main.duration);
                }

                // Jouer un son d'explosion
                AudioManager.Instance.PlaySound(AudioType.Dead, AudioSourceType.Player);

                // Détruit la bulle explosive après son effet
                Destroy(gameObject);
            });
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
        if (this == null || transform == null)
        {
            Debug.LogWarning("Échec de l'animation : l'objet ou son transform est null !");
            return;
        }
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

    void SetBubbleAppearance()
    {
        switch (bubbleType)
        {
            case BubbleType.Swipe:
                GetComponentInChildren<SpriteRenderer>().color = Color.green;
                break;
            case BubbleType.Explosive:
                GetComponentInChildren<SpriteRenderer>().color = Color.red;
                break;
            case BubbleType.Freeze:
                GetComponentInChildren<SpriteRenderer>().color = Color.blue;
                break;
            default:
                GetComponentInChildren<SpriteRenderer>().color = Color.white;
                break;
        }
    } 


    void OnDestroy()
    {
        DOTween.Kill(transform, true);
        Debug.Log("OnDestroy appelé");

    }

}
