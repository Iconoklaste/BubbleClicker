using DG.Tweening;
using UnityEngine;

public enum Bubble1Type { Normal, Swipe, Explosive, Freeze }

public class Bubble1 : MonoBehaviour

{
    [Header("Type de bulles")]
    [Tooltip("Type de bulle (Normal, Swipe, Explosive, Freeze)")]
    public Bubble1Type bubble1Type = Bubble1Type.Normal;

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



    private Rigidbody2D rb;

    public float amplitudeOscillation = 0.1f;
    public float frequenceOscillation = 1f;
    public float pressionInitiale = 1f;
    public float pressionMinimale = 0.1f;

    public float frequenceContraction = 2f;
    public float amplitudeContraction = 0.1f;

    private float pression;
    private float startTimeOffset; // Décalage unique par bulle

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
        float chance = Random.value; // Valeur entre 0 et 1

        if (chance < 0.05f) // 15% de chance d'avoir une bulle spéciale
        {
            float specialChance = Random.value; // Autre tirage pour départager les types

            if (specialChance < 0.33f) // 5% (1/3 de 15%)
                bubble1Type = Bubble1Type.Swipe;
            else if (specialChance < 0.47f) // 2% (reste de 15% après Swipe)
                bubble1Type = Bubble1Type.Explosive;
            else // 8% (reste des 15%)
                bubble1Type = Bubble1Type.Freeze;
        }
        else
        {
            bubble1Type = Bubble1Type.Normal;
        }

        SetBubble1Appearance();
        //AnimateBubble1();
        //AnimateBubble1Growth();

        pression = pressionInitiale;
        startTimeOffset = Random.Range(0f, Mathf.PI * 2); // Décalage aléatoire
        AnimateBubble1Growth2();
    }


    void Update()
    {
        // Croissance et déplacement de la bulle (déjà existants)
        transform.localScale += Vector3.one * growthRate * Time.deltaTime;


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

        Debug.Log($"Bulle cliquée : {gameObject.name}, génération : {generation}");

        // Selon le type de bulle, exécuter le pouvoir spécial
        switch (bubble1Type)
        {
            case Bubble1Type.Normal:
                HandleNormalBubble();
                break;
            case Bubble1Type.Swipe:
                // Active le mode swipe pour 3 secondes dans le GameManager
                GameManager.Instance.ActivateSwipeMode(3f);
                break;
            case Bubble1Type.Explosive:
                HandleExplosiveBubble();
                break;
            case Bubble1Type.Freeze:
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
        if (bubble1Type == Bubble1Type.Normal && bubblePrefab != null && numberOfChildBubbles > 0 && generation < maxGenerations)
        {
            for (int i = 0; i < numberOfChildBubbles; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
                Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
                GameObject childBubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);
                // Appliquer une réduction d'échelle aux enfants
                childBubble.transform.localScale = transform.localScale * childScaleFactor;

                Bubble1 childBubbleScript = childBubble.GetComponent<Bubble1>();
                if (childBubbleScript != null)
                {
                    childBubbleScript.generation = generation + 1;
                    childBubbleScript.growthRate = growthRate;
                    childBubbleScript.bubble1Type = Bubble1Type.Normal; // Toujours une bulle normale
                    childBubbleScript.SetBubble1Appearance(); // Met à jour l'apparence en conséquence
                }

                Rigidbody2D childRb = childBubble.GetComponent<Rigidbody2D>();
                if (childRb != null)
                {
                    childRb.gravityScale = 0;

                    childRb.isKinematic = false; // ✅ Permet au moteur physique d'agir immédiatement
                    childRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // ✅ Prévient les collisions ratées


                    // Vecteur directionnel qui pousse la bulle loin du parent
                    Vector2 directionAwayFromParent = ((Vector2)childBubble.transform.position - (Vector2)transform.position).normalized;

                    // Si la direction est (0,0), on prend une direction aléatoire
                    if (directionAwayFromParent == Vector2.zero)
                        directionAwayFromParent = Random.insideUnitCircle.normalized;

                    // Appliquer une impulsion initiale
                    childRb.AddForce(directionAwayFromParent * explosionForce, ForceMode2D.Impulse);
                    

                    // Animation DOtween pour améliorer l'effet de dispersion
                    childBubble.transform.DOMove((Vector2)childBubble.transform.position + directionAwayFromParent * 0.5f, 0.5f)
                        .SetEase(Ease.OutQuad);
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
                    Bubble1 bubble = col.GetComponent<Bubble1>();
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


    void AnimateBubble1Growth2()
    {


        // Oscillation latérale naturelle avec Perlin Noise et décalage
        float oscillationX = Mathf.PerlinNoise(Time.time * frequenceOscillation + startTimeOffset, 0) * 2 - 1;
        rb.velocity = new Vector2(oscillationX * amplitudeOscillation, rb.velocity.y);

        // Gestion de la pression et du grossissement
        pression = Mathf.Max(pressionMinimale, pression - growthRate * Time.fixedDeltaTime);
        float facteurTaille = Mathf.Lerp(1f, 2f, (pressionInitiale - pression) / pressionInitiale);

        // **Contraction indépendante**
        float contraction = 1f + Mathf.Sin(Time.time * frequenceContraction + startTimeOffset) * amplitudeContraction;
        transform.localScale = new Vector3(facteurTaille * contraction, facteurTaille / contraction, 1);
    }


    void SetBubble1Appearance()
    {
        switch (bubble1Type)
        {
            case Bubble1Type.Swipe:
                GetComponent<SpriteRenderer>().color = Color.green;
                break;
            case Bubble1Type.Explosive:
                GetComponent<SpriteRenderer>().color = Color.red;
                break;
            case Bubble1Type.Freeze:
                GetComponent<SpriteRenderer>().color = Color.blue;
                break;
            default:
                GetComponent<SpriteRenderer>().color = Color.white;
                break;
        }
    } 


    void OnDestroy()
    {
        DOTween.Kill(transform, true);
        Debug.Log("OnDestroy appelé");

    }

}
