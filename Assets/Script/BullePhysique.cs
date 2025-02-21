using DG.Tweening;
using UnityEngine;
using static BubbleSpawner;




public class BullePhysique : MonoBehaviour
{
    public float vitesseMontee = 0.3f;

    public float amplitudeOscillation = 0.1f;
    public float frequenceOscillation = 1f;

    public float pressionInitiale = 1f;   // Pression en bas de l'écran
    public float pressionMinimale = 0.1f; // Pression en haut de l'écran

    public float frequenceContraction = 2f;
    public float amplitudeContraction = 0.1f;

    private Rigidbody2D rb;
    private float startTimeOffset;

    private float hauteurMin;  // Y du bas de l'écran
    private float hauteurMax;  // Y du haut de l'écran

    [Tooltip("Facteur de croissance continue de la bulle.")]
    public float growthFactorSpeed = 0.1f;  // Vitesse de croissance continue de la bulle
    private float currentGrowthFactor = 1f; // Valeur de croissance initiale


    [Tooltip("Système de particules pour simuler l'éclatement (optionnel).")]
    public ParticleSystem popEffect;

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

    [Tooltip("Points de base attribués lors du clic sur la bulle.")]
    public int basePoints = 10;

    public BubbleType bubbleType;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startTimeOffset = Random.Range(0f, Mathf.PI * 2);

        // Définir les bornes de l'écran
        hauteurMin = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0)).y;
        hauteurMax = Camera.main.ViewportToWorldPoint(new Vector3(0, 1, 0)).y;
    }

    void FixedUpdate()
    {
        ClampPositionToScreen();

        // Montée progressive
        rb.velocity = new Vector2(rb.velocity.x, vitesseMontee);

        // Oscillation latérale naturelle avec Perlin Noise
        float oscillationX = Mathf.PerlinNoise(Time.time * frequenceOscillation + startTimeOffset, 0) * 2 - 1;
        rb.velocity = new Vector2(oscillationX * amplitudeOscillation, rb.velocity.y);

        // Calcul de la pression en fonction de la hauteur
        float hauteurNormale = Mathf.InverseLerp(hauteurMin, hauteurMax, transform.position.y);
        float pression = Mathf.Lerp(pressionInitiale, pressionMinimale, hauteurNormale);

        // Gestion du grossissement basé sur la pression
        float facteurTaille = Mathf.Lerp(0.5f, 2.5f, (pressionInitiale - pression) / pressionInitiale);

        // Effet de contraction
        float contraction = 1f + Mathf.Sin(Time.time * frequenceContraction + startTimeOffset) * amplitudeContraction;

        // Application du facteur de croissance continu
        currentGrowthFactor += growthFactorSpeed * Time.fixedDeltaTime;  // Croissance continue de la bulle
        if (currentGrowthFactor > 5f)  // Limite pour éviter une taille trop grande (ajuste cette valeur selon ton besoin)
            currentGrowthFactor = 5f;

        // Appliquer la taille finale en combinant tous les facteurs
        transform.localScale = new Vector3(facteurTaille * contraction * currentGrowthFactor, facteurTaille / contraction * currentGrowthFactor, 1);
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

    public void SetBubbleType(BubbleType type)
    {
        bubbleType = type;
        // Ici, applique des effets visuels ou logiques selon le type
        switch (bubbleType)
        {
            case BubbleType.Swipe:
                GetComponent<SpriteRenderer>().color = Color.green;
                Debug.Log("Bulle spéciale : Swipe !");
                break;
            case BubbleType.Explosive:
                GetComponent<SpriteRenderer>().color = Color.red;
                Debug.Log("Bulle spéciale : Explosive !");
                break;
            case BubbleType.Freeze:
                GetComponent<SpriteRenderer>().color = Color.blue;
                Debug.Log("Bulle spéciale : Freeze !");
                break;
            case BubbleType.Normal:
                GetComponent<SpriteRenderer>().color = Color.white;
                Debug.Log("Bulle normale.");
                break;
        }
    }



    private void OnMouseDown()
    {
        // Ne rien faire si le jeu est terminé
        if (GameManager.Instance != null && GameManager.Instance.gameIsOver)
            return;

        Debug.Log($"Bulle cliquée : {gameObject.name}, génération : {generation}, type : {bubbleType}");

        // Exécuter le pouvoir spécial en fonction du type de bulle
        switch (bubbleType)
        {
            case BubbleType.Normal:
                HandleNormalBubble();
                break;
            case BubbleType.Swipe:
                GameManager.Instance.ActivateSwipeMode(3f);
                break;
            case BubbleType.Explosive:
                HandleExplosiveBubble();
                break;
            case BubbleType.Freeze:
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

        // Génération des bulles enfants uniquement pour les bulles normales
        if (bubbleType == BubbleType.Normal && bubblePrefab != null && numberOfChildBubbles > 0 && generation < maxGenerations)
        {
            for (int i = 0; i < numberOfChildBubbles; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
                Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
                GameObject childBubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);

                // Appliquer une réduction d'échelle aux enfants
                childBubble.transform.localScale = transform.localScale * childScaleFactor;

                BullePhysique childBubbleScript = childBubble.GetComponent<BullePhysique>();
                if (childBubbleScript != null)
                {
                    childBubbleScript.generation = generation + 1;

                    // Obtenir un type de bulle aléatoire via le spawner
                    BubbleSpawner spawner = FindObjectOfType<BubbleSpawner>();
                    if (spawner != null)
                    {
                        childBubbleScript.bubbleType = spawner.GetRandomBubbleType();
                    }
                    else
                    {
                        childBubbleScript.bubbleType = BubbleType.Normal;
                    }

                    childBubbleScript.SetBubbleType(childBubbleScript.bubbleType);
                }

                Rigidbody2D childRb = childBubble.GetComponent<Rigidbody2D>();
                if (childRb != null)
                {
                    childRb.gravityScale = 0;
                    childRb.isKinematic = false;
                    childRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                    // Direction aléatoire pour éviter un alignement parfait
                    Vector2 directionAwayFromParent = ((Vector2)childBubble.transform.position - (Vector2)transform.position).normalized;
                    if (directionAwayFromParent == Vector2.zero)
                        directionAwayFromParent = Random.insideUnitCircle.normalized;

                    // Appliquer une force d'explosion
                    childRb.AddForce(directionAwayFromParent * explosionForce, ForceMode2D.Impulse);

                    // Animation DOtween pour améliorer l'effet de dispersion
                    childBubble.transform.DOMove((Vector2)childBubble.transform.position + directionAwayFromParent * 0.5f, 0.5f)
                        .SetEase(Ease.OutQuad);
                }
            }
        }

        // Détruire la bulle après activation du pouvoir
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

        // Animation : La bulle grossit rapidement jusqu'à la taille de l'explosion
        transform.DOScale(Vector3.one * (explosionRadius * 2), 0.8f)  // Augmente la taille de la bulle jusqu'au rayon d'explosion en 0.8 secondes
            .SetEase(Ease.OutQuad)
            .OnStart(() =>
            {
                // Démarrer la détection des bulles à détruire avant l'animation (ça n'affecte pas la vue)
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius, LayerMask.GetMask("BulleLayer"));

                foreach (Collider2D col in colliders)
                {
                    BullePhysique bubble = col.GetComponent<BullePhysique>();
                    if (bubble != null && bubble != this)
                    {
                        // Assurez-vous que c'est bien une bulle avant de la détruire
                        if (col.CompareTag("Bubble"))  // Vérifie que le tag est bien "Bubble"
                        {
                            // Ajoute les bulles à détruire ici, mais on ne les détruit pas encore
                            Destroy(bubble.gameObject);  // C'est OK de les supprimer en tout cas
                        }
                    }
                }
            })
            .OnComplete(() =>
            {
                // Effet d'explosion (ajoute un effet visuel si disponible)
                if (popEffect != null)
                {
                    ParticleSystem effect = Instantiate(popEffect, transform.position, Quaternion.identity);
                    effect.Play();
                    Destroy(effect.gameObject, effect.main.duration);
                }

                // Jouer un son d'explosion
                AudioManager.Instance.PlaySound(AudioType.Dead, AudioSourceType.Player);

                // Détruire la bulle explosive après l'effet
                Destroy(gameObject);
            });
    }




    void OnDrawGizmos()
    {
        // Vérifier si le type de bulle est Explosive
        if (bubbleType == BubbleType.Explosive)
        {
            // Définir la couleur du Gizmo (par exemple rouge pour l'explosion)
            Gizmos.color = Color.red;

            // Dessiner un cercle représentant le rayon d'explosion autour de la position de la bulle
            Gizmos.DrawWireSphere(transform.position, 2f); // Utilise le rayon d'explosion que tu veux
        }
    }

}
