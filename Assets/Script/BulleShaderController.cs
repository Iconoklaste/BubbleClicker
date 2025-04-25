using DG.Tweening;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static BubbleSpawner;


public class BulleShaderController : MonoBehaviour
{
    [Header("Movement")]
    public float vitesseMontee = 0.3f;

    [Header("Size & Growth")]
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

    [Header("Freeze Mode Settings")] // Nouvelle section pour le facteur de ralentissement
    [Range(0.0f, 1.0f)] // 0 = arrêt complet, 1 = aucune différence
    public float freezeSlowdownFactor = 0.1f; // Ralentit à 10% de la vitesse normale

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
    


    [Header("UI Feedback")]
    public GameObject floatingTextPrefab;


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
    private Transform _foundWorldSpaceCanvasTransform;
    private bool isSwiping = false;

    private bool swipeModeActive = false;
    private bool freezeModeActive = false;

    // Utiliser un HashSet est efficace pour vérifier si une bulle a déjà été touchée pendant CE swipe
    private static HashSet<BulleShaderController> swipedBubblesThisGesture = new HashSet<BulleShaderController>();

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

        GameObject canvasGO = GameObject.FindWithTag("FloatingTextCanvas");
        if (canvasGO != null)
        {
            _foundWorldSpaceCanvasTransform = canvasGO.transform;
            // Debug.Log($"Canvas World Space trouvé via Tag: {canvasGO.name}");
        }
        else
        {
            Debug.LogError("BulleShaderController: Impossible de trouver le GameObject avec le tag 'FloatingTextCanvas' ! Assure-toi qu'il existe dans la scène et qu'il a le bon tag.");
        }

        // --- Vérifications ---
        if (mainCamera == null) Debug.LogError("BulleShaderController: Camera principale non trouvée !");
        if (rb == null) Debug.LogError($"BulleShaderController: Rigidbody2D non trouvé sur {gameObject.name}");
        if (bubbleMaterialInstance == null && objectRenderer != null) Debug.LogError($"BulleShaderController: Impossible d'obtenir l'instance du matériel pour {gameObject.name}.");
        else if (bubbleMaterialInstance == null && objectRenderer == null) Debug.LogError($"BulleShaderController: Impossible d'obtenir l'instance du matériel car le Renderer est manquant pour {gameObject.name}.");
        
        if (bubbleLayerMask == 0) 
        {
            bubbleLayerMask = LayerMask.GetMask("BulleLayer");
            if (bubbleLayerMask == 0)
            {
                Debug.LogWarning($"Awake: Layer 'BulleLayer' non trouvé ou non assigné sur {gameObject.name}. La détection de blocage pourrait ne pas fonctionner.");
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
    }

    void Update()
    {
        // 1. Mettre à jour l'état local des modes depuis le GameManager
        bool previousFreezeState = freezeModeActive;
        CheckGlobalSwipeMode();
        CheckGlobalFreezeMode();

    // 2. Appliquer les vitesses du shader UNIQUEMENT si l'état de freeze a changé
    if (freezeModeActive != previousFreezeState)
    {
         ApplyShaderSpeeds(); // Applique la vitesse ralentie ou la vitesse normale
    }

        // 3. Vérifier si le jeu est terminé
        if (gameManagerInstance != null && gameManagerInstance.gameIsOver)
        {
            if (isSwiping) isSwiping = false; // Assurer la réinitialisation
            return;
        }

        // 4. Gérer l'input de swipe SEULEMENT si le mode est actif
        if (swipeModeActive)
        {
            HandleSwipeInput(); // La méthode qu'on a définie plus haut
        }

/*         // --- Gestion de l'input pour le swipe ---

        // 1. Début du swipe (bouton pressé)
        if (Input.GetMouseButtonDown(0))
        {
            isSwiping = true;
            swipedBubblesThisGesture.Clear(); // Vider la liste des bulles touchées pour ce nouveau geste
            // Debug.Log("Swipe Started"); // Optionnel
        }

        // 2. Pendant le swipe (bouton maintenu)
        if (isSwiping && Input.GetMouseButton(0))
        {
            // Lancer un rayon depuis la caméra vers la position de la souris
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // Utiliser Physics2D.GetRayIntersection pour détecter les colliders 2D
            // Important: Spécifier la distance (Mathf.Infinity) et le LayerMask (bubbleLayerMask)
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, bubbleLayerMask);

            // Si le rayon touche un collider sur le bon layer
            if (hit.collider != null)
            {
                // Essayer de récupérer le script BulleShaderController sur l'objet touché
                BulleShaderController hitBubble = hit.collider.GetComponent<BulleShaderController>();

                // Si c'est bien une bulle et qu'elle n'a pas déjà été ajoutée à la liste pour CE swipe
                if (hitBubble != null && swipedBubblesThisGesture.Add(hitBubble)) // .Add retourne true si l'élément n'était pas déjà présent
                {
                    // On a touché une nouvelle bulle pendant ce swipe !
                    HandleSwipePop(hitBubble); // Appeler la fonction pour la détruire/gérer
                }
                // Si hitBubble est null ou déjà dans le HashSet, on ne fait rien (évite double pop)
            }
        }

        // 3. Fin du swipe (bouton relâché)
        if (isSwiping && Input.GetMouseButtonUp(0))
        {
            isSwiping = false;
            // Debug.Log($"Swipe Ended. Bubbles popped this gesture: {swipedBubblesThisGesture.Count}"); // Optionnel
            // Ici, tu pourrais ajouter un bonus si swipedBubblesThisGesture.Count > X, par exemple.
            // swipedBubblesThisGesture.Clear(); // Pas besoin de Clear ici, c'est fait au prochain MouseButtonDown
        } */
    }



    void FixedUpdate()
    {
        if (rb == null) return; // Sécurité

        // 1. Déterminer les vitesses effectives pour cette frame
        float currentVitesseMontee = vitesseMontee;
        float currentGrowthSpeed = growthFactorSpeed;

        // Si le mode Freeze est actif, appliquer le facteur de ralentissement
        if (freezeModeActive)
        {
            currentVitesseMontee *= freezeSlowdownFactor;
            currentGrowthSpeed *= freezeSlowdownFactor;
        }

        // 2. Logique de mouvement (utilise la vitesse effective)
        bool shouldMoveUp = CanMoveUp();
        if (shouldMoveUp)
        {
            // Appliquer la vitesse de montée (potentiellement ralentie)
            rb.velocity = Vector2.up * currentVitesseMontee;
            isBlockedAtTop = false;
        }
        else
        {
            // Si bloqué, arrêter complètement le mouvement vertical
            rb.velocity = new Vector2(rb.velocity.x, 0); // Garde la vélocité horizontale si jamais il y en a
            isBlockedAtTop = true;
        }

        // 3. Logique de Clamp (inchangée)
        ClampPositionToScreen();

        // 4. Logique de croissance (utilise la vitesse de croissance effective)
        //    On appelle GrowOverTime en lui passant la vitesse calculée
        GrowOverTime(currentGrowthSpeed);
    }

    private void ApplyShaderSpeeds()
    {
        // Vérifier si l'instance du matériel existe (sécurité)
        if (bubbleMaterialInstance == null)
        {
             Debug.LogWarning($"[{gameObject.name}] ApplyShaderSpeeds: Impossible d'appliquer les vitesses, bubbleMaterialInstance est null.");
             return;
        }

        // Déterminer le multiplicateur de vitesse basé sur l'état actuel du mode Freeze
        // Si freezeModeActive est true, utiliser freezeSlowdownFactor, sinon utiliser 1.0 (vitesse normale)
        float speedMultiplier = freezeModeActive ? freezeSlowdownFactor : 1.0f;

        // Calculer les vitesses finales à appliquer au shader
        float finalRotationSpeed = instanceRotationSpeed * speedMultiplier;
        float finalDistortionSpeed = instanceDistortionSpeed * speedMultiplier;

        // Appliquer les vitesses calculées aux propriétés du shader
        bubbleMaterialInstance.SetFloat(RotationSpeedID, finalRotationSpeed);
        bubbleMaterialInstance.SetFloat(DistortionSpeedID, finalDistortionSpeed);

        // Log de débogage (optionnel mais utile)
        Debug.Log($"[{gameObject.name}] ApplyShaderSpeeds called. FreezeActive={freezeModeActive}, Multiplier={speedMultiplier:F2}, FinalRot={finalRotationSpeed:F2}, FinalDist={finalDistortionSpeed:F2}");
    }

    // Gestion de l'Input du Swipe
    private void HandleSwipeInput()
    {
        // Vérifier si la caméra existe (sécurité)
        if (mainCamera == null)
        {
            Debug.LogError("HandleSwipeInput: mainCamera est null !");
            return;
        }

        // 1. Début du swipe (bouton pressé)
        if (Input.GetMouseButtonDown(0))
        {
            isSwiping = true;
            swipedBubblesThisGesture.Clear(); // Vider la liste des bulles touchées pour ce nouveau geste
            // Debug.Log("Swipe Started"); // Optionnel
        }

        // 2. Pendant le swipe (bouton maintenu)
        //    Important : Vérifier AUSSI si on est bien en train de swiper (isSwiping)
        if (isSwiping && Input.GetMouseButton(0))
        {
            // Lancer un rayon depuis la caméra vers la position de la souris
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // Utiliser Physics2D.GetRayIntersection pour détecter les colliders 2D
            // Important: Spécifier la distance (Mathf.Infinity) et le LayerMask (bubbleLayerMask)
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, bubbleLayerMask);

            // Si le rayon touche un collider sur le bon layer
            if (hit.collider != null)
            {
                // Essayer de récupérer le script BulleShaderController sur l'objet touché
                BulleShaderController hitBubble = hit.collider.GetComponent<BulleShaderController>();

                // Si c'est bien une bulle et qu'elle n'a pas déjà été ajoutée à la liste pour CE swipe
                if (hitBubble != null && swipedBubblesThisGesture.Add(hitBubble)) // .Add retourne true si l'élément n'était pas déjà présent
                {
                    // On a touché une nouvelle bulle pendant ce swipe !
                    HandleSwipePop(hitBubble); // Appeler la fonction pour la détruire/gérer
                }
                // Si hitBubble est null ou déjà dans le HashSet, on ne fait rien (évite double pop)
            }
        }

        // 3. Fin du swipe (bouton relâché)
        //    Important : Vérifier AUSSI si on était bien en train de swiper (isSwiping)
        if (isSwiping && Input.GetMouseButtonUp(0))
        {
            isSwiping = false;
            // Debug.Log($"Swipe Ended. Bubbles popped this gesture: {swipedBubblesThisGesture.Count}"); // Optionnel
            // Ici, tu pourrais ajouter un bonus si swipedBubblesThisGesture.Count > X, par exemple.
            // swipedBubblesThisGesture.Clear(); // Pas besoin de Clear ici, c'est fait au prochain MouseButtonDown
        }
    }


    private void InitializeAndRandomizeShaderParameters()
    {
        if (bubbleMaterialInstance != null)
        {
            // Calculer et stocker les vitesses *originales* randomisées
            instanceRotationSpeed = shaderRotationSpeed * Random.Range(minSpeedMultiplier, maxSpeedMultiplier);
            instanceDistortionSpeed = shaderDistortionSpeed * Random.Range(minSpeedMultiplier, maxSpeedMultiplier);

            // Appliquer DIRECTEMENT les vitesses originales ici, sans passer par ApplyShaderSpeeds
            // pour être sûr de l'état initial.
            bubbleMaterialInstance.SetFloat(RotationSpeedID, instanceRotationSpeed);
            bubbleMaterialInstance.SetFloat(DistortionSpeedID, instanceDistortionSpeed);

            // Définir la densité (ne change pas avec le freeze)
            bubbleMaterialInstance.SetFloat(DensityID, shaderDensity);

            Debug.Log($"[{gameObject.name}] Initialized & Applied ORIGINAL shader speeds: Rot={instanceRotationSpeed:F2}, Dist={instanceDistortionSpeed:F2}");
        }
        else
        {
            Debug.LogError($"InitializeAndRandomizeShaderParameters: bubbleMaterialInstance is null on {gameObject.name}.");
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
    if (mainCamera == null) return true;
    Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
    if (viewportPos.y >= 0.99f) return false;

    // Vérification 2: Y a-t-il une autre bulle juste au-dessus ?
    float bubbleRadius = transform.localScale.x * 0.5f; // Approximation du rayon

    Collider2D ownCollider = GetComponent<Collider2D>(); // Récupérer notre propre collider
    float epsilon = 0.01f; // Ajouter une petite marge (epsilon) pour s'assurer que le rayon part BIEN de l'extérieur.
    float raycastStartYOffset = (ownCollider != null ? ownCollider.bounds.extents.y : bubbleRadius) + epsilon;
    Vector2 raycastOrigin = (Vector2)transform.position + Vector2.up * raycastStartYOffset;
    float raycastDistance = 0.05f; // Distance très courte pour détecter un contact immédiat
    // Dessiner le rayon pour le débogage (optionnel)
     //Debug.DrawRay(raycastOrigin, Vector2.up * raycastDistance, Color.red, 0.1f); // Ajout durée pour mieux voir
    RaycastHit2D hit = Physics2D.Raycast(raycastOrigin, Vector2.up, raycastDistance, bubbleLayerMask);

    // S'il y a une collision avec une autre bulle (sur le bon layer)
        if (hit.collider != null && hit.collider != ownCollider)
        {
            // Debug.Log($"{gameObject.name}: Blocked by {hit.collider.name}");
            return false;
        }
        return true;
    }

    private void GrowOverTime(float effectiveGrowthSpeed) // Prend la vitesse en paramètre
    {
        // Augmenter le facteur de croissance en utilisant la vitesse fournie
        currentGrowthFactor += effectiveGrowthSpeed * Time.fixedDeltaTime;
        currentGrowthFactor = Mathf.Min(currentGrowthFactor, MAX_GROWTH_FACTOR);

        // Application de la taille globale (inchangé)
        transform.localScale = Vector3.one * initialScale * currentGrowthFactor;
    }

/* 


    private void MoveUpward()
    {
        rb.velocity = Vector2.up * vitesseMontee;
    }

    // Fonction pour juste faire grossir la bulle
    private void GrowOverTime(float effectiveGrowthSpeed) // Prend la vitesse en paramètre
    {
        // Augmenter le facteur de croissance en utilisant la vitesse fournie
        currentGrowthFactor += effectiveGrowthSpeed * Time.fixedDeltaTime;
        currentGrowthFactor = Mathf.Min(currentGrowthFactor, MAX_GROWTH_FACTOR);

        // Application de la taille globale (inchangé)
        transform.localScale = Vector3.one * initialScale * currentGrowthFactor;
    }

    // Retiré: AdjustSize() qui contenait la logique de pression
 */
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
        switch (bubbleType) { 
            case BubbleType.Swipe: targetColor = Color.green; break;
            case BubbleType.Explosive: targetColor = Color.red; break;
            case BubbleType.Freeze: targetColor = Color.magenta; break;
            case BubbleType.Normal:
            default: targetColor = Color.white; break;
        }
        bubbleMaterialInstance.SetColor(ColorID, targetColor);
    }

private void OnMouseDown()
{
    // Si on est en mode swipe, le clic simple ne fait rien, tout est géré dans Update
    if (swipeModeActive) return;

        // Si le jeu est fini, ne rien faire
    if (gameManagerInstance != null && gameManagerInstance.gameIsOver) return;
    // Debug.Log($"Bulle cliquée : {gameObject.name}, génération : {generation}, type : {bubbleType}");

    // Variable pour stocker le score à afficher ---
    int scoreToDisplay = 0;
    bool shouldDestroy = true; // Par défaut, la bulle est détruite

    switch (bubbleType)
    {
        case BubbleType.Normal:
            scoreToDisplay = basePoints * (generation + 1); // Calculé aussi dans HandleNormalBubble, mais ok ici pour l'affichage
            HandleNormalBubble(); // Gère le score réel et le son
            SpawnChildBubbles();
            // Le texte est déjà affiché dans HandleNormalBubble
            break; // Important: Sortir du switch après ce cas

        case BubbleType.Swipe:
            scoreToDisplay = basePoints; // Score pour le swipe
            ActivateSwipeMode(3f); // Active le mode global via GameManager
            if (gameManagerInstance != null)
                if (gameManagerInstance != null)
                {
                    gameManagerInstance.AddScore(scoreToDisplay);
                    gameManagerInstance.NbBubblePoped(1);
                }
            if (audioManagerInstance != null) audioManagerInstance.PlaySound(AudioType.Swipe, AudioSourceType.Player);
            // Afficher le texte flottant pour Swipe
            ShowFloatingText($"+{scoreToDisplay}", transform.position, Color.green); // Couleur optionnelle
            break;

        case BubbleType.Explosive:
            // La logique d'affichage est dans HandleExplosiveBubble
            HandleExplosiveBubble();
            shouldDestroy = false; // La destruction est gérée DANS HandleExplosiveBubble
            return; // Sortir de OnMouseDown car HandleExplosiveBubble gère la destruction

        case BubbleType.Freeze:
            scoreToDisplay = basePoints; // Score pour le freeze
            ActivateFreezeMode(3f);
             if (gameManagerInstance != null)
             {
                 ActivateFreezeMode(3f);
                 gameManagerInstance.AddScore(scoreToDisplay); // Ajouter le score
                 gameManagerInstance.NbBubblePoped(1);
             }
            if (audioManagerInstance != null) audioManagerInstance.PlaySound(AudioType.Pop, AudioSourceType.Player); // Peut-être un son spécifique ?
            // Afficher le texte flottant pour Freeze
            ShowFloatingText($"+{scoreToDisplay}", transform.position, Color.cyan); // Couleur optionnelle
            break;

        default: // Comportement par défaut (identique à Normal)
            scoreToDisplay = basePoints * (generation + 1);
            HandleNormalBubble();
            SpawnChildBubbles();
            break; // Important
    }

    // Jouer l'effet de pop et détruire (si applicable)
    if (shouldDestroy)
    {
        PlayPopEffect(); // Jouer l'effet visuel de pop
        Destroy(gameObject);
    }
}

    private void PlayPopEffect()
    {
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
        
        if (gameManagerInstance != null)
        {
            gameManagerInstance.AddScore(scoreAward);
            gameManagerInstance.NbBubblePoped(1);
        }

        ShowFloatingText($"+{scoreAward}", transform.position);

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
                float overlapRadius = finalRadius * 1.25f;



                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, overlapRadius, 1 << bubbleLayer);
                int destroyedCount = 0;

                foreach (Collider2D hitCollider in colliders) {
                    if (hitCollider.gameObject == gameObject) continue;
                    var bubbleController = hitCollider.GetComponent<BulleShaderController>();
                    if (bubbleController != null) {
                        bubbleController.PlayPopEffect();
                        Destroy(bubbleController.gameObject);
                        destroyedCount++;
                    }
                }
                PlayPopEffect();
                if (audioManagerInstance != null) audioManagerInstance.PlaySound(AudioType.Dead, AudioSourceType.Player);

                int totalPopped = 1 + destroyedCount;
                int scoreForExplosion = basePoints * 2;
                int scoreForDestroyed = destroyedCount * basePoints;
                int totalScore = scoreForExplosion + scoreForDestroyed; // Score total de l'explosion

                if (gameManagerInstance != null)
                {
                    gameManagerInstance.AddScore(totalScore);
                    gameManagerInstance.NbBubblePoped(totalPopped);
                }


                // --- Instanciation du Texte Flottant ---
                // Affiche le texte seulement si plus d'une bulle a éclaté (pour éviter le "+1!")
                string displayText;
                if (totalPopped > 1) {
                    // Afficher score ET nombre de bulles si plus d'une
                    displayText = $"+{totalScore}\n({totalPopped} popped!)"; // \n pour nouvelle ligne
                } else {
                    // Afficher juste le score si seule la bulle explosive a éclaté
                    displayText = $"+{totalScore}";
                }

                // Utiliser la méthode helper avec le texte formaté et une couleur
                ShowFloatingText(displayText, transform.position, Color.yellow);
                // --- FIN MODIFICATION ---


                // --- Suppression de l'ancien code d'instanciation direct ---
                // if (totalPopped >= 1 && floatingTextPrefab != null) { ... } // Tout ce bloc est remplacé par l'appel ShowFloatingText ci-dessus

                Destroy(gameObject); // Détruire la bulle explosive
            });
    }


    private void ActivateSwipeMode(float duration)
    {
        if (GameManager.Instance != null)
        {
            //Debug.Log($"Bulle {gameObject.name} demande l'activation du Swipe Mode pour {duration}s.");
            GameManager.Instance.SetSwipeMode(true, duration);
        }
        else
        {
            Debug.LogError("ActivateSwipeMode: GameManager.Instance est null !");
        }
    }
    // Mettre à jour la variable locale en fonction du GameManager
    private void CheckGlobalSwipeMode() {
        if (GameManager.Instance != null) {
            this.swipeModeActive = GameManager.Instance.IsSwipeModeActive;
        } else {
            this.swipeModeActive = false; // Sécurité si GameManager n'existe pas
        }
    }

/*     private IEnumerator SwipeModeCoroutine(float duration)
    {
        // Appliquer le mode à TOUTES les bulles (ou via un manager global)
        // Ici, on le fait via une variable statique ou en parcourant toutes les bulles.
        // Pour simplifier, on va supposer que chaque bulle vérifie une variable globale
        // ou que le GameManager gère cet état.
        // Pour cet exemple, on va juste activer le booléen sur CETTE instance,
        // mais il faudrait une meilleure gestion globale.
        // *** SOLUTION AMÉLIORÉE : Gérer via GameManager ***
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSwipeMode(true); // Méthode à créer dans GameManager
        }
        else {
             Debug.LogWarning("SwipeModeCoroutine: GameManager non trouvé pour activer le mode globalement.");
             // Fallback: activer sur cette instance (moins utile)
             // this.swipeModeActive = true;
        }


        yield return new WaitForSeconds(duration);

        // Désactiver le mode globalement
         if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSwipeMode(false); // Méthode à créer dans GameManager
        }
         else {
              Debug.LogWarning("SwipeModeCoroutine: GameManager non trouvé pour désactiver le mode globalement.");
              // Fallback: désactiver sur cette instance
              // this.swipeModeActive = false;
         }
        Debug.Log("Mode Swipe global désactivé.");
    } */



    // --- NOUVELLE MÉTHODE POUR GÉRER LE POP PAR SWIPE ---
    private void HandleSwipePop(BulleShaderController bubble)
    {
        // Ne rien faire si la bulle cible est nulle (sécurité)
        if (bubble == null) return;

        Debug.Log($"Swiped bubble: {bubble.gameObject.name} (Type: {bubble.bubbleType})");

        // --- Logique spécifique au swipe ---
        // Pour l'instant, on va faire simple :
        // - Donner des points de base (ou un montant spécifique au swipe)
        // - Jouer un son de swipe
        // - Jouer l'effet de pop
        // - Détruire la bulle
        // - PAS de spawn d'enfants, PAS d'effet spécial (Explosive/Freeze) déclenché par le swipe

        int scoreAward = bubble.basePoints; // Score simple pour le swipe
        if (gameManagerInstance != null)
        {
            gameManagerInstance.AddScore(scoreAward);
            gameManagerInstance.NbBubblePoped(1); // Compter la bulle popée
        }

        // Afficher le texte flottant (utilise la méthode de la bulle touchée)
        bubble.ShowFloatingText($"+{scoreAward}", bubble.transform.position, Color.cyan); // Couleur spécifique pour le swipe ?

        // Jouer le son de swipe (utilise l'instance de la bulle touchée pour accéder à l'AudioManager global)
        if (bubble.audioManagerInstance != null)
        {
            bubble.audioManagerInstance.PlaySound(AudioType.Swipe, AudioSourceType.Player);
        }

        // Jouer l'effet visuel (utilise la méthode de la bulle touchée)
        bubble.PlayPopEffect();

        // Détruire l'objet GameObject de la bulle touchée
        Destroy(bubble.gameObject);
    }

    // Méthode d'activation du mode Freeze
    private void ActivateFreezeMode(float duration)
    {
        if (GameManager.Instance != null)
        {
            //Debug.Log($"Bulle {gameObject.name} demande l'activation du Freeze Mode pour {duration}s.");
            GameManager.Instance.SetFreezeMode(true, duration);
        }
        else
        {
            Debug.LogError("ActivateFreezeMode: GameManager.Instance est null !");
        }
    }

 private void CheckGlobalFreezeMode() {
    bool previousState = this.freezeModeActive;
    if (GameManager.Instance != null) {
        this.freezeModeActive = GameManager.Instance.IsFreezeModeActive;
    } else {
        this.freezeModeActive = false;
    }
    // Log si l'état change, surtout s'il devient true
    if (this.freezeModeActive != previousState) {
         Debug.LogWarning($"[{gameObject.name}] Freeze mode changed from {previousState} to {this.freezeModeActive}. GameManager state: {GameManager.Instance?.IsFreezeModeActive}");
    }
}


void OnDrawGizmos()
{
    // S'applique uniquement aux bulles explosives
    if (bubbleType == BubbleType.Explosive)
    {
        // --- Calculs basés sur la logique de HandleExplosiveBubble ---

        // 1. Échelle que la bulle ATTEINDRA à la fin de l'animation DOScale
        //    (Basé sur l'échelle ACTUELLE * le multiplicateur d'explosion)
        float potentialFinalScale = transform.localScale.x * explosionRadius;

        // 2. Rayon correspondant à cette échelle finale (taille visuelle)
        float potentialFinalRadius = potentialFinalScale / 2f;

        // 3. Rayon d'overlap (celui qui est augmenté de 10%)
        float potentialOverlapRadius = potentialFinalRadius * 1.10f; // Utilise le même facteur que dans HandleExplosiveBubble

        // --- Dessin des Gizmos ---

        // Dessiner le rayon final de l'explosion (taille visuelle de la bulle)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Rouge semi-transparent
        Gizmos.DrawWireSphere(transform.position, potentialFinalRadius);

        // Dessiner le rayon d'overlap utilisé pour la détection (légèrement plus grand)
        Gizmos.color = new Color(1f, 1f, 0f, 0.8f); // Jaune plus opaque pour le distinguer
        Gizmos.DrawWireSphere(transform.position, potentialOverlapRadius);

        // Optionnel : Ajouter une légende ou un label (nécessite UnityEditor namespace)
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * potentialOverlapRadius, $"Overlap Radius: {potentialOverlapRadius:F2}");
        #endif
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

private void ShowFloatingText(string text, Vector3 position, Color? color = null)
{
        if (floatingTextPrefab == null || _foundWorldSpaceCanvasTransform == null)
        {
            // Debug.LogError("ShowFloatingText: Prefab ou Canvas manquant."); // Éviter trop de logs
            return;
        }


    // Instancier comme enfant du Canvas
    GameObject textInstance = Instantiate(floatingTextPrefab, position, Quaternion.identity, _foundWorldSpaceCanvasTransform);

    FloatingTextEffect ftScript = textInstance.GetComponent<FloatingTextEffect>();
    if (ftScript != null)
    {
        // Utiliser la nouvelle méthode Initialize
        ftScript.Initialize(text, color);
    }
    else
    {
        // Debug.LogWarning("Le prefab de texte flottant n'a pas le script FloatingTextEffect.");
        // Détruire l'instance si le script est manquant pour éviter les objets orphelins
        Destroy(textInstance);
    }
}

}
