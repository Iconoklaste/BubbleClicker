using DG.Tweening;
using UnityEngine;
using static BubbleSpawner; // Décommenter si BubbleType est défini dans BubbleSpawner
// using Random = UnityEngine.Random; // Décommenter si nécessaire

// Assurez-vous que BubbleType est défini quelque part (ici ou dans BubbleSpawner)
// public enum BubbleType { Normal, Swipe, Explosive, Freeze }

public class BulleShaderController : MonoBehaviour
{
    [Header("Movement")]
    public float vitesseMontee = 0.3f;

    [Header("Size & Growth")]
    // Retiré: pressionInitiale, pressionMinimale
    public float initialScale = 1f; // Taille de départ de la bulle
    public float growthFactorSpeed = 0.1f; // Vitesse à laquelle la bulle grossit
    private const float MAX_GROWTH_FACTOR = 5f; // Taille maximale relative à la taille initiale

    [Header("Shader Control")]
    public float shaderRotationSpeed = 10f;
    public float shaderDensity = 0.9f;
    public float shaderDistortionSpeed = 0.75f;
    [Range(0.5f, 1.5f)] public float minSpeedMultiplier = 0.8f;
    [Range(0.5f, 1.5f)] public float maxSpeedMultiplier = 1.2f;

    [Header("Explosive Bubble")]
    public float explosionRadius = 2f;
    private const float EXPLOSION_ANIMATION_DURATION = 0.8f;

    [Header("Child Bubbles")]
    public GameObject bubblePrefab;
    public int numberOfChildBubbles = 3;
    [Range(0.1f, 1f)] public float childScaleFactor = 0.5f;
    public float explosionForce = 100f;
    public float spawnOffsetRadius = 0.2f;

    [Header("General")]
    public ParticleSystem popEffect;
    public int generation = 0;
    public int maxGenerations = 4;
    public int basePoints = 10;
    public BubbleType bubbleType;

    // --- Variables privées et caches ---
    private Rigidbody2D rb;
    private Camera mainCamera;
    private Renderer objectRenderer;
    private Material bubbleMaterialInstance;
    private BubbleSpawner bubbleSpawnerInstance;
    private GameManager gameManagerInstance;
    private AudioManager audioManagerInstance;

    // Retiré: hauteurMin, hauteurMax
    private float currentGrowthFactor = 1f; // Commence à 1 (taille initiale)

    // --- Noms des propriétés Shader ---
    private static readonly int RotationSpeedID = Shader.PropertyToID("_Rotation_Speed");
    private static readonly int DensityID = Shader.PropertyToID("_Densite");
    private static readonly int DistortionSpeedID = Shader.PropertyToID("_Distortion_speed");
    private static readonly int ColorID = Shader.PropertyToID("_Couleur_de_bulle");

    // --- Variables pour les valeurs randomisées par instance ---
    private float instanceRotationSpeed;
    private float instanceDistortionSpeed;

    void Awake()
    {
        // --- Mise en cache des composants ---
        rb = GetComponent<Rigidbody2D>();
        objectRenderer = GetComponent<Renderer>();

        if (objectRenderer != null)
        {
            bubbleMaterialInstance = objectRenderer.material;
        }
        else
        {
            Debug.LogError($"BulleShaderController: Renderer non trouvé sur {gameObject.name}");
        }

        // --- Mise en cache des Managers et Caméra ---
        mainCamera = Camera.main;
        bubbleSpawnerInstance = FindObjectOfType<BubbleSpawner>();
        gameManagerInstance = GameManager.Instance;
        audioManagerInstance = AudioManager.Instance;

        // --- Vérifications ---
        if (mainCamera == null) Debug.LogError("BulleShaderController: Camera principale non trouvée !");
        if (rb == null) Debug.LogError($"BulleShaderController: Rigidbody2D non trouvé sur {gameObject.name}");
        if (bubbleMaterialInstance == null && objectRenderer != null) Debug.LogError($"BulleShaderController: Impossible d'obtenir l'instance du matériel pour {gameObject.name}.");
        else if (bubbleMaterialInstance == null && objectRenderer == null) Debug.LogError($"BulleShaderController: Impossible d'obtenir l'instance du matériel car le Renderer est manquant pour {gameObject.name}.");
    }

    void Start()
    {
        // Retiré: Calcul des hauteurs min/max

        // --- Initialisation ET Randomisation des paramètres Shader ---
        InitializeAndRandomizeShaderParameters();

        // Appliquer la couleur initiale
        ApplyBubbleTypeColor();

        // Appliquer la taille initiale définie dans l'inspecteur
        transform.localScale = Vector3.one * initialScale;
        // Réinitialiser le facteur de croissance à 1 pour commencer la croissance à partir de la taille initiale
        currentGrowthFactor = 1f;
    }

    private void InitializeAndRandomizeShaderParameters()
    {
         if (bubbleMaterialInstance != null)
        {
            instanceRotationSpeed = shaderRotationSpeed * Random.Range(minSpeedMultiplier, maxSpeedMultiplier);
            instanceDistortionSpeed = shaderDistortionSpeed * Random.Range(minSpeedMultiplier, maxSpeedMultiplier);

            bubbleMaterialInstance.SetFloat(RotationSpeedID, instanceRotationSpeed);
            bubbleMaterialInstance.SetFloat(DensityID, shaderDensity);
            bubbleMaterialInstance.SetFloat(DistortionSpeedID, instanceDistortionSpeed);
        }
         else
         {
             Debug.LogError($"InitializeAndRandomizeShaderParameters: Tentative d'accès à bubbleMaterialInstance alors qu'il est null sur {gameObject.name}.");
         }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        ClampPositionToScreen();
        MoveUpward();
        GrowOverTime(); // Nouvelle fonction pour gérer la croissance
    }

    private void MoveUpward()
    {
        rb.velocity = Vector2.up * vitesseMontee;
    }

    // Fonction simplifiée pour juste faire grossir la bulle
    private void GrowOverTime()
    {
        // --- Augmenter le facteur de croissance ---
        currentGrowthFactor += growthFactorSpeed * Time.fixedDeltaTime;
        // Limiter la croissance maximale
        currentGrowthFactor = Mathf.Min(currentGrowthFactor, MAX_GROWTH_FACTOR);

        // --- Application de la taille globale ---
        // La taille est maintenant la taille initiale * le facteur de croissance actuel
        transform.localScale = Vector3.one * initialScale * currentGrowthFactor;
    }

    // Retiré: AdjustSize() qui contenait la logique de pression

    void ClampPositionToScreen()
    {
        if (mainCamera == null) return;

        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);

        viewportPos.x = Mathf.Clamp01(viewportPos.x);

        /*         if (viewportPos.y > 1.05f)
        {
            Destroy(gameObject);
            return;
        } */
        viewportPos.y = Mathf.Clamp01(viewportPos.y);

        transform.position = mainCamera.ViewportToWorldPoint(viewportPos);
    }

    public void SetBubbleType(BubbleType type)
    {
        bubbleType = type;
        ApplyBubbleTypeColor();
    }

    private void ApplyBubbleTypeColor()
    {
        if (bubbleMaterialInstance == null)
        {
             Debug.LogWarning($"ApplyBubbleTypeColor: bubbleMaterialInstance est null sur {gameObject.name}.");
             return;
        }
        Color targetColor;
        switch (bubbleType) { /* ... cas inchangés ... */
            case BubbleType.Swipe: targetColor = Color.green; break;
            case BubbleType.Explosive: targetColor = Color.red; break;
            case BubbleType.Freeze: targetColor = Color.cyan; break;
            case BubbleType.Normal:
            default: targetColor = Color.white; break;
        }
        bubbleMaterialInstance.SetColor(ColorID, targetColor);
    }

    private void OnMouseDown()
    {
        // Logique de clic (inchangée)
        if (gameManagerInstance != null && gameManagerInstance.gameIsOver) return;
        Debug.Log($"Bulle cliquée : {gameObject.name}, génération : {generation}, type : {bubbleType}");
        switch (bubbleType) { /* ... cas inchangés ... */
            case BubbleType.Normal: HandleNormalBubble(); SpawnChildBubbles(); break;
            case BubbleType.Swipe: if (gameManagerInstance != null) gameManagerInstance.ActivateSwipeMode(3f); if (audioManagerInstance != null) audioManagerInstance.PlaySound(AudioType.Swipe, AudioSourceType.Player); break;
            case BubbleType.Explosive: HandleExplosiveBubble(); return;
            case BubbleType.Freeze: if (gameManagerInstance != null) gameManagerInstance.ActivateFreezeMode(3f); if (audioManagerInstance != null) audioManagerInstance.PlaySound(AudioType.Pop, AudioSourceType.Player); break;
            default: HandleNormalBubble(); SpawnChildBubbles(); break;
        }
        PlayPopEffect();
        Destroy(gameObject);
    }

    private void PlayPopEffect()
    {
        // Identique à avant
        if (popEffect != null) {
            ParticleSystem effect = Instantiate(popEffect, transform.position, Quaternion.identity);
            Destroy(effect.gameObject, effect.main.duration + effect.main.startLifetime.constantMax);
        }
    }

private void SpawnChildBubbles()
{
    if (bubblePrefab == null || numberOfChildBubbles <= 0 || generation >= maxGenerations) return;
    if (bubbleSpawnerInstance == null) { Debug.LogWarning("SpawnChildBubbles: BubbleSpawner non trouvé."); }

    for (int i = 0; i < numberOfChildBubbles; i++) {
        Vector2 randomOffset = Random.insideUnitCircle.normalized * spawnOffsetRadius;
        Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
        GameObject childBubbleGO = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity); // Renommé pour clarté

        // --- Modification ici ---
        // Calculer l'échelle initiale voulue pour l'enfant
        float desiredChildInitialScale = transform.localScale.x * childScaleFactor; // Utilise la scale actuelle du parent

        BulleShaderController childScript = childBubbleGO.GetComponent<BulleShaderController>();
        if (childScript != null) {
            childScript.generation = generation + 1;
            childScript.SetBubbleType(bubbleSpawnerInstance != null ? bubbleSpawnerInstance.GetRandomBubbleType() : BubbleType.Normal);

            // Définir la variable initialScale de l'enfant AVANT que son Start() ne s'exécute
            childScript.initialScale = desiredChildInitialScale;
            // Note : La ligne childBubbleGO.transform.localScale = ... est maintenant inutile ici,
            // car le Start() de l'enfant va s'en charger en utilisant la valeur qu'on vient de lui passer.
        }

        Rigidbody2D childRb = childBubbleGO.GetComponent<Rigidbody2D>();
        if (childRb != null) {
            // ... (logique de force et DoTween inchangée) ...
             childRb.gravityScale = 0; childRb.isKinematic = false; childRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
             Vector2 directionAwayFromParent = (spawnPosition - (Vector2)transform.position).normalized;
             if (directionAwayFromParent == Vector2.zero) directionAwayFromParent = Random.insideUnitCircle.normalized;
             Vector2 randomDirOffset = Random.insideUnitCircle * 0.3f;
             Vector2 finalDirection = (directionAwayFromParent + randomDirOffset).normalized;
             float randomForceMultiplier = Random.Range(0.8f, 1.3f);
             childRb.AddForce(finalDirection * explosionForce * randomForceMultiplier, ForceMode2D.Impulse);
             float randomMoveDistance = Random.Range(0.4f, 0.6f); float randomMoveDuration = Random.Range(0.4f, 0.6f);
             childBubbleGO.transform.DOMove((Vector2)childBubbleGO.transform.position + finalDirection * randomMoveDistance, randomMoveDuration).SetEase(Ease.OutQuad);
        }
    }
}


    private void HandleNormalBubble()
    {
        // Calculer et ajouter le score via le GameManager mis en cache
        int scoreAward = basePoints * (generation + 1);
        if (gameManagerInstance != null) gameManagerInstance.AddScore(scoreAward);

        // --- AJOUT DE LA LOGIQUE DE SÉLECTION DU SON ---
        AudioType soundToPlay;
        if (generation == 0)
        {
            soundToPlay = AudioType.Pop; // Son pour la bulle initiale
        }
        else
        {
            // Choisis ici le son que tu veux pour TOUTES les bulles enfants (génération > 0)
            // Par exemple, utilisons 'Swipe' comme dans l'autre script, ou un autre son.
            soundToPlay = AudioType.Swipe;
            // Ou si tu veux un son spécifique pour les enfants, tu pourrais ajouter un AudioType.ChildPop
            // soundToPlay = AudioType.ChildPop; // (Nécessiterait d'ajouter ChildPop à l'enum et à l'AudioManager)
        }
        // --- FIN DE L'AJOUT ---


        // Jouer le son sélectionné via l'AudioManager mis en cache
        if (audioManagerInstance != null)
        {
            audioManagerInstance.PlaySound(soundToPlay, AudioSourceType.Player);
        }
    }

    private void HandleExplosiveBubble()
    {
        // Logique d'explosion (inchangée)
        Collider2D col = GetComponent<Collider2D>(); if (col != null) col.enabled = false;
        if (rb != null) rb.velocity = Vector2.zero;
        // Utiliser la taille actuelle pour l'explosion
        float startScale = transform.localScale.x;
        transform.DOScale(startScale * explosionRadius, EXPLOSION_ANIMATION_DURATION)
            .SetEase(Ease.OutExpo)
            .OnComplete(() => {
                int bubbleLayer = LayerMask.NameToLayer("BulleLayer");
                float finalRadius = transform.localScale.x / 2f; // Utilise la taille finale après l'animation
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, finalRadius, 1 << bubbleLayer);
                foreach (Collider2D hitCollider in colliders) {
                    if (hitCollider.gameObject == gameObject) continue;
                    var bubbleController = hitCollider.GetComponent<BulleShaderController>();
                    if (bubbleController != null) {
                        bubbleController.PlayPopEffect();
                        Destroy(bubbleController.gameObject);
                    }
                }
                PlayPopEffect();
                if (audioManagerInstance != null) audioManagerInstance.PlaySound(AudioType.Dead, AudioSourceType.Player);
                if (gameManagerInstance != null) gameManagerInstance.AddScore(basePoints * 2);
                Destroy(gameObject);
            });
    }

    void OnDrawGizmosSelected() {
        // Gizmos (inchangé, mais le calcul du rayon final est basé sur la scale actuelle)
        if (bubbleType == BubbleType.Explosive) {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            // Calcule le rayon tel qu'il sera à la fin de l'animation DOScale
            float finalRadius = (transform.localScale.x / 2f) * explosionRadius;
            Gizmos.DrawWireSphere(transform.position, finalRadius);
        }
    }

    void OnDestroy()
    {
        // Nettoyage du matériel (inchangé)
        if (bubbleMaterialInstance != null)
        {
            Destroy(bubbleMaterialInstance);
        }
    }
}
