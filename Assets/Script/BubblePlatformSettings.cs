using UnityEngine;

// Permet de créer des instances de cet objet via le menu Assets > Create
[CreateAssetMenu(fileName = "BubbleSettings_New", menuName = "Bubble Clicker/Bubble Platform Settings", order = 1)]
public class BubblePlatformSettings : ScriptableObject
{
    [Header("Movement")]
    public float vitesseMontee = 0.3f;

    [Header("Size & Growth")]
    public float initialScale = 1f;
    public float growthFactorSpeed = 0.1f;
    // public float maxGrowthFactor = 5f; // Tu peux aussi mettre les constantes ici si elles doivent varier

    [Header("Shader Control")]
    public float shaderRotationSpeed = 10f;
    public float shaderDensity = 0.9f;
    public float shaderDistortionSpeed = 0.75f;
    [Range(0.5f, 1.5f)] public float minSpeedMultiplier = 0.8f;
    [Range(0.5f, 1.5f)] public float maxSpeedMultiplier = 1.2f;

    [Header("Explosive Bubble")]
    public float explosionRadius = 2f;
    public float explosionOverlapMultiplier = 1.10f; // Ex: 1.10f pour 10%
    // public float explosionAnimationDuration = 0.8f;

    [Header("Child Bubbles")]
    public int numberOfChildBubbles = 3;
    [Range(0.1f, 1f)] public float childScaleFactor = 0.5f;
    public float explosionForce = 100f;
    public float spawnOffsetRadius = 0.2f;

    [Header("General")]
    public int basePoints = 10;
    // Ajoute ici toute autre variable de BulleShaderController que tu veux rendre dépendante de la plateforme
}
