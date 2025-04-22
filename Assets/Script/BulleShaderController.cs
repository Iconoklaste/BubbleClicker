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
    public LayerMask bubbleLayerMask;
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
    private bool hasBeenInitializedBySpawner = false;
    private float currentGrowthFactor = 1f;
    private bool isBlockedAtTop = false;

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
        if (bubbleLayerMask == 0) // 0 est la valeur par défaut si rien n'est coché
        {
            bubbleLayerMask = LayerMask.GetMask("BulleLayer"); // Remplace "BulleLayer" par le nom exact de ton layer
            if (bubbleLayerMask == 0) // Si le layer n'existe pas
            {
                Debug.LogWarning($"Awake: Layer 'BulleLayer' non trouvé ou non assigné sur {gameObject.name}. La détection de blocage pourrait ne pas fonctionner.");
                // Tu pourrais assigner 'Default' ou une autre valeur sûre si nécessaire
                // bubbleLayerMask = LayerMask.GetMask("Default");
            }
        }
    }

    void Start()
    {
        // Si la bulle n'a PAS été initialisée par le spawner (c'est une bulle de génération 0)
        if (!hasBeenInitializedBySpawner)
        {
            // Initialisation standard pour les bulles de base (gen 0)
            InitializeAndRandomizeShaderParameters();
            ApplyBubbleTypeColor(); // Appliquer la couleur basée sur le type défini dans l'inspecteur

            // Appliquer la taille initiale définie dans l'inspecteur
            transform.localScale = Vector3.one * initialScale;
            // Réinitialiser le facteur de croissance à 1 pour commencer la croissance
            currentGrowthFactor = 1f;
        }
        // Si hasBeenInitializedBySpawner est true, InitializeFromSpawner a déjà tout fait.
    }

void FixedUpdate()
{
    if (rb == null) return;

    // 1. Vérifier si on DOIT bouger vers le haut
    bool shouldMoveUp = CanMoveUp();

    // 2. Appliquer la vélocité (ou l'arrêter)
    if (shouldMoveUp)
    {
        MoveUpward();
        isBlockedAtTop = false; // On peut bouger, donc on n'est pas bloqué
    }
    else
    {
        rb.velocity = Vector2.zero; // Arrêter le mouvement
        isBlockedAtTop = true; // On est bloqué
    }

    // 3. S'assurer que la position reste dans l'écran (surtout après l'arrêt)
    ClampPositionToScreen();

    // 4. Gérer la croissance SEULEMENT si on n'est PAS bloqué en haut
    if (!isBlockedAtTop) // <-- AJOUT DE CETTE CONDITION
    {
        GrowOverTime();
    }
    // Si isBlockedAtTop est true, la croissance est mise en pause.
    // Elle reprendra automatiquement au prochain FixedUpdate où shouldMoveUp devient true.
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

    // Nouvelle méthode pour permettre au spawner de définir l'état initial
    public void InitializeFromSpawner(int gen, BubbleType type, float startGrowthFactor)
    {
        generation = gen;
        SetBubbleType(type); // Applique le type et la couleur

        // Appliquer le facteur de croissance initial
        currentGrowthFactor = startGrowthFactor;

        // Appliquer l'échelle correspondante immédiatement
        // Assure-toi que initialScale a bien la valeur du prefab ici
        if (Mathf.Approximately(initialScale, 0f)) {
            Debug.LogWarning($"InitializeFromSpawner: initialScale est proche de zéro pour {gameObject.name}. L'échelle ne sera pas appliquée correctement.");
            transform.localScale = Vector3.zero; // Ou une autre gestion d'erreur
        } else {
            transform.localScale = Vector3.one * initialScale * currentGrowthFactor;
        }


        // Marquer comme initialisé pour éviter l'écrasement dans Start()
        hasBeenInitializedBySpawner = true;

        // Initialiser les paramètres du shader (peut être fait ici ou dans Start)
        InitializeAndRandomizeShaderParameters();
    }



    // Nouvelle fonction pour vérifier si la voie est libre vers le haut
private bool CanMoveUp()
{
    if (mainCamera == null)
    {
        // Debug.Log($"{gameObject.name}: CanMoveUp - No Main Camera, assuming true."); // Décommenter si besoin
        return true; // Si pas de caméra, on suppose qu'on peut bouger
    }

    // Vérification 1: Est-on déjà tout en haut de l'écran ?
    Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
    // Utiliser une petite marge pour éviter les problèmes de flottants
    if (viewportPos.y >= 0.99f)
    {
        Debug.Log($"{gameObject.name}: CanMoveUp - Blocked by ceiling (Viewport Y: {viewportPos.y})");
        return false; // Bloqué par le plafond de l'écran
    }

    // Vérification 2: Y a-t-il une autre bulle juste au-dessus ?
    float bubbleRadius = transform.localScale.x * 0.5f; // Approximation du rayon
    Collider2D ownCollider = GetComponent<Collider2D>(); // Récupérer notre propre collider

    // --- AJUSTEMENT CRUCIAL ---
    // Calculer le point le plus haut du collider (si possible), sinon utiliser le rayon.
    // Ajouter une petite marge (epsilon) pour s'assurer que le rayon part BIEN de l'extérieur.
    float epsilon = 0.01f;
    float raycastStartYOffset = (ownCollider != null ? ownCollider.bounds.extents.y : bubbleRadius) + epsilon;
    Vector2 raycastOrigin = (Vector2)transform.position + Vector2.up * raycastStartYOffset;

    // Distance très courte pour détecter un contact immédiat
    float raycastDistance = 0.05f;

    // Dessiner le rayon pour le débogage (optionnel)
    Debug.DrawRay(raycastOrigin, Vector2.up * raycastDistance, Color.red, 0.1f); // Ajout durée pour mieux voir

    RaycastHit2D hit = Physics2D.Raycast(raycastOrigin, Vector2.up, raycastDistance, bubbleLayerMask);

    // S'il y a une collision avec une autre bulle (sur le bon layer)
    if (hit.collider != null)
    {
        // --- AJOUT VERIFICATION ---
        // S'assurer qu'on ne se détecte pas soi-même (même si l'offset devrait l'éviter)
        if (hit.collider == ownCollider)
        {
             Debug.LogWarning($"{gameObject.name}: CanMoveUp - Raycast hit itself! Check raycast origin calculation.");
             // On considère qu'on peut bouger si on se touche soi-même (erreur de raycast)
             return true;
        }
        else
        {
             Debug.Log($"{gameObject.name}: CanMoveUp - Blocked by bubble above: {hit.collider.name}");
             return false; // Bloqué par une autre bulle
        }
    }

    // Si on n'est ni au plafond, ni bloqué par une autre bulle : on peut monter
    // Debug.Log($"{gameObject.name}: CanMoveUp - Path clear."); // Décommenter si besoin
    return true;
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

    // Assurer que la caméra est bien orthographique (optionnel, mais bon pour la robustesse)
    if (!mainCamera.orthographic)
    {
        Debug.LogWarning("ClampPositionToScreen est optimisé pour une caméra orthographique, mais la caméra principale ne l'est pas.");
        // On peut soit continuer avec la méthode précédente (plus générale),
        // soit utiliser celle-ci qui sera une approximation.
    }

    // 1. Obtenir le rayon de la bulle en unités du monde
    float worldRadius = transform.localScale.x / 2f;

    // 2. Calculer le rayon en coordonnées viewport (plus direct en orthographique)
    //    On convertit simplement une distance horizontale et verticale du monde en viewport.
    //    On prend la position actuelle et une position décalée du rayon.
    Vector3 centerWorld = transform.position;
    Vector3 rightEdgeWorld = centerWorld + Vector3.right * worldRadius;
    Vector3 topEdgeWorld = centerWorld + Vector3.up * worldRadius;

    Vector3 centerViewport = mainCamera.WorldToViewportPoint(centerWorld);
    Vector3 rightEdgeViewport = mainCamera.WorldToViewportPoint(rightEdgeWorld);
    Vector3 topEdgeViewport = mainCamera.WorldToViewportPoint(topEdgeWorld);

    // La différence en viewport nous donne directement le "rayon" dans cet espace
    float viewportRadiusX = Mathf.Abs(rightEdgeViewport.x - centerViewport.x);
    float viewportRadiusY = Mathf.Abs(topEdgeViewport.y - centerViewport.y);

    // 3. Définir les limites min/max en viewport pour le *centre* de la bulle
    float minX = viewportRadiusX;
    float maxX = 1f - viewportRadiusX;
    // float minY = viewportRadiusY; // Décommente si tu veux aussi clamper le bas
    float maxY = 1f - viewportRadiusY; // Limite haute ajustée

    // 4. Clamper la position du *centre* dans ces nouvelles limites
    Vector3 currentViewportPos = mainCamera.WorldToViewportPoint(transform.position); // Obtenir la position actuelle en viewport
    Vector3 clampedViewportPos = currentViewportPos;

    clampedViewportPos.x = Mathf.Clamp(currentViewportPos.x, minX, maxX);
    // On ne clampe que le haut et les côtés comme demandé
    clampedViewportPos.y = Mathf.Clamp(currentViewportPos.y, 0f, maxY); // Clamp Y entre 0 (bas de l'écran) et la limite haute ajustée
    // Si tu voulais aussi clamper le bas :
    // clampedViewportPos.y = Mathf.Clamp(currentViewportPos.y, minY, maxY);

    // Gérer le cas où la bulle est plus large/haute que l'écran
    // Si min > max, Clamp force la valeur à min ou max selon le cas.
    // Si la bulle est trop grosse, elle sera "collée" au bord correspondant.

    // 5. Reconvertir la position viewport clampée en position monde
    //    Important: Il faut fournir la distance Z originale pour la reconversion !
    clampedViewportPos.z = currentViewportPos.z; // Garder la profondeur Z originale
    transform.position = mainCamera.ViewportToWorldPoint(clampedViewportPos);
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
    // Assurer que l'échelle initiale du parent n'est pas nulle pour éviter la division par zéro
    if (Mathf.Approximately(initialScale, 0f)) {
         Debug.LogError($"SpawnChildBubbles: Tentative de spawn depuis une bulle parente ({gameObject.name}) avec initialScale proche de zéro.");
         return;
    }


    for (int i = 0; i < numberOfChildBubbles; i++) {
        Vector2 randomOffset = Random.insideUnitCircle.normalized * spawnOffsetRadius;
        Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
        GameObject childBubbleGO = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);

        BulleShaderController childScript = childBubbleGO.GetComponent<BulleShaderController>();
        if (childScript != null) {
            // --- Modification ici ---

            // 1. Obtenir l'échelle de base de l'enfant (depuis son propre inspecteur/prefab)
            float childBaseInitialScale = childScript.initialScale;
             if (Mathf.Approximately(childBaseInitialScale, 0f)) {
                 Debug.LogError($"SpawnChildBubbles: Le prefab de bulle enfant a une initialScale proche de zéro.");
                 Destroy(childBubbleGO); // Détruire l'enfant car on ne peut pas calculer
                 continue; // Passer à l'itération suivante
             }


            // 2. Calculer l'échelle visuelle de départ souhaitée pour l'enfant
            float desiredStartScaleValue = transform.localScale.x * childScaleFactor;

            // 3. Calculer le facteur de croissance initial nécessaire pour atteindre cette échelle
            //    en partant de l'échelle de base de l'enfant (childBaseInitialScale)
            float childStartGrowthFactor = desiredStartScaleValue / childBaseInitialScale;

            // 4. Utiliser la nouvelle méthode pour initialiser l'enfant
            BubbleType newType = bubbleSpawnerInstance != null ? bubbleSpawnerInstance.GetRandomBubbleType() : BubbleType.Normal;
            childScript.InitializeFromSpawner(generation + 1, newType, childStartGrowthFactor);

        } else {
             Debug.LogError($"SpawnChildBubbles: Le prefab de bulle enfant n'a pas de script BulleShaderController.");
             Destroy(childBubbleGO); // Nettoyage
             continue;
        }


        Rigidbody2D childRb = childBubbleGO.GetComponent<Rigidbody2D>();
        if (childRb != null) {
            // Appliquer la force (logique inchangée)
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
