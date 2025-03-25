using DG.Tweening;
using UnityEngine;
using static BubbleSpawner;

public class BullePhysique : MonoBehaviour
{
    [Header("Movement and Physics")]
    public float vitesseMontee = 0.3f;
    public float amplitudeOscillation = 0.1f;
    public float frequenceOscillation = 1f;

    [Header("Pressure and Size")]
    public float pressionInitiale = 1f;
    public float pressionMinimale = 0.1f;
    public float frequenceContraction = 2f;
    public float amplitudeContraction = 0.1f;
    public float growthFactorSpeed = 0.1f;

    [Header("Explosive Bubble")]
    public float explosionRadius = 2f;

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

    private Rigidbody2D rb;
    private float startTimeOffset;
    private float hauteurMin;
    private float hauteurMax;
    private float currentGrowthFactor = 1f;
    private const float MAX_GROWTH_FACTOR = 5f; // Limit for growth

    private const float MIN_SIZE_FACTOR = 0.5f;
    private const float MAX_SIZE_FACTOR = 2.5f;
    private const float EXPLOSION_ANIMATION_DURATION = 0.8f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startTimeOffset = Random.Range(0f, Mathf.PI * 2);
        hauteurMin = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0)).y;
        hauteurMax = Camera.main.ViewportToWorldPoint(new Vector3(0, 1, 0)).y;
    }

    void FixedUpdate()
    {
        ClampPositionToScreen();
        MoveAndOscillate();
        AdjustSizeBasedOnPressure();
    }

    private void MoveAndOscillate()
    {
        rb.velocity = new Vector2(rb.velocity.x, vitesseMontee);
        float oscillationX = Mathf.PerlinNoise(Time.time * frequenceOscillation + startTimeOffset, 0) * 2 - 1;
        rb.velocity = new Vector2(oscillationX * amplitudeOscillation, rb.velocity.y);
    }

    private void AdjustSizeBasedOnPressure()
    {
        float hauteurNormale = Mathf.InverseLerp(hauteurMin, hauteurMax, transform.position.y);
        float pression = Mathf.Lerp(pressionInitiale, pressionMinimale, hauteurNormale);
        float facteurTaille = Mathf.Lerp(MIN_SIZE_FACTOR, MAX_SIZE_FACTOR, (pressionInitiale - pression) / pressionInitiale);
        float contraction = 1f + Mathf.Sin(Time.time * frequenceContraction + startTimeOffset) * amplitudeContraction;

        currentGrowthFactor += growthFactorSpeed * Time.fixedDeltaTime;
        currentGrowthFactor = Mathf.Min(currentGrowthFactor, MAX_GROWTH_FACTOR);

        transform.localScale = new Vector3(facteurTaille * contraction * currentGrowthFactor, facteurTaille / contraction * currentGrowthFactor, 1);
    }

    void ClampPositionToScreen()
    {
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
        viewportPos.x = Mathf.Clamp(viewportPos.x, 0f, 1f);
        viewportPos.y = Mathf.Clamp(viewportPos.y, 0f, 1f);
        transform.position = Camera.main.ViewportToWorldPoint(viewportPos);
    }

    public void SetBubbleType(BubbleType type)
    {
        bubbleType = type;
        switch (bubbleType)
        {
            case BubbleType.Swipe:
                GetComponent<SpriteRenderer>().color = Color.green;
                break;
            case BubbleType.Explosive:
                GetComponent<SpriteRenderer>().color = Color.red;
                break;
            case BubbleType.Freeze:
                GetComponent<SpriteRenderer>().color = Color.blue;
                break;
            case BubbleType.Normal:
                GetComponent<SpriteRenderer>().color = Color.white;
                break;
        }
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance != null && GameManager.Instance.gameIsOver) return;

        Debug.Log($"Bulle cliquée : {gameObject.name}, génération : {generation}, type : {bubbleType}");

        switch (bubbleType)
        {
            case BubbleType.Normal:
                HandleNormalBubble();
                SpawnChildBubbles();
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

        PlayPopEffect();
        Destroy(gameObject);
    }

    private void PlayPopEffect()
    {
        if (popEffect != null)
        {
            ParticleSystem effect = Instantiate(popEffect, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration);
        }
    }

    private void SpawnChildBubbles()
    {
        if (bubblePrefab == null || numberOfChildBubbles <= 0 || generation >= maxGenerations) return;

        for (int i = 0; i < numberOfChildBubbles; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * spawnOffsetRadius;
            Vector2 spawnPosition = (Vector2)transform.position + randomOffset;
            GameObject childBubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);
            childBubble.transform.localScale = transform.localScale * childScaleFactor;

            BullePhysique childBubbleScript = childBubble.GetComponent<BullePhysique>();
            if (childBubbleScript != null)
            {
                childBubbleScript.generation = generation + 1;
                BubbleSpawner spawner = FindObjectOfType<BubbleSpawner>();
                childBubbleScript.bubbleType = spawner != null ? spawner.GetRandomBubbleType() : BubbleType.Normal;
                childBubbleScript.SetBubbleType(childBubbleScript.bubbleType);
            }

            Rigidbody2D childRb = childBubble.GetComponent<Rigidbody2D>();
            if (childRb != null)
            {
                childRb.gravityScale = 0;
                childRb.isKinematic = false;
                childRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                Vector2 directionAwayFromParent = ((Vector2)childBubble.transform.position - (Vector2)transform.position).normalized;
                if (directionAwayFromParent == Vector2.zero) directionAwayFromParent = Random.insideUnitCircle.normalized;

                childRb.AddForce(directionAwayFromParent * explosionForce, ForceMode2D.Impulse);
                childBubble.transform.DOMove((Vector2)childBubble.transform.position + directionAwayFromParent * 0.5f, 0.5f).SetEase(Ease.OutQuad);
            }
        }
    }

    private void HandleNormalBubble()
    {
        int scoreAward = basePoints * (generation + 1);
        if (GameManager.Instance != null) GameManager.Instance.AddScore(scoreAward);

        AudioType soundType = generation switch
        {
            0 => AudioType.Pop,
            1 => AudioType.Swipe,
            _ => AudioType.Dead,
        };
        AudioManager.Instance.PlaySound(soundType, AudioSourceType.Player);
    }

    private void HandleExplosiveBubble()
    {
        transform.DOScale(Vector3.one * (explosionRadius * 2), EXPLOSION_ANIMATION_DURATION)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius, LayerMask.GetMask("BulleLayer"));
                foreach (Collider2D col in colliders)
                {
                    if (col.CompareTag("Bubble"))
                    {
                        BullePhysique bubble = col.GetComponent<BullePhysique>();
                        if (bubble != null && bubble != this)
                        {
                            Destroy(bubble.gameObject);
                        }
                    }
                }

                PlayPopEffect();
                AudioManager.Instance.PlaySound(AudioType.Dead, AudioSourceType.Player);
                Destroy(gameObject);
            });
    }

    void OnDrawGizmos()
    {
        if (bubbleType == BubbleType.Explosive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
