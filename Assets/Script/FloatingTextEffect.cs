// Dans FloatingTextEffect.cs

using UnityEngine;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class FloatingTextEffect : MonoBehaviour
{
    public TextMeshProUGUI textMesh;
    public float moveDistance = 1.0f;
    public float fadeDuration = 1.0f;
    public float fadeDelay = 0.3f;

    private CanvasGroup canvasGroup;
    private Sequence sequence; // Pour gérer l'animation complète

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (textMesh == null) textMesh = GetComponentInChildren<TextMeshProUGUI>();

        if (textMesh == null)
        {
            Debug.LogError("FloatingTextEffect: Composant TextMeshProUGUI non trouvé !");
            Destroy(gameObject);
        }
    }

    // Méthode modifiée pour accepter n'importe quel texte et une couleur optionnelle
    public void Initialize(string textToShow, Color? textColor = null)
    {
        if (textMesh == null || canvasGroup == null) return; // Sécurité

        // Appliquer le texte
        textMesh.text = textToShow;

        // Appliquer la couleur si fournie, sinon garder celle du prefab
        if (textColor.HasValue)
        {
            textMesh.color = textColor.Value;
        }

        // S'assurer que l'alpha est à 1 au début
        canvasGroup.alpha = 1f;

        // --- Animation avec DOTween Sequence pour un meilleur contrôle ---
        // Tuer toute séquence précédente sur cet objet (sécurité)
        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill();
        }

        sequence = DOTween.Sequence();

        // 1. Déplacement vers le haut
        sequence.Append(transform.DOMoveY(transform.position.y + moveDistance, fadeDuration).SetEase(Ease.OutQuad));

        // 2. Fondu en sortie (inséré dans la même séquence, avec délai)
        //    La durée du fondu est ajustée pour finir en même temps que le déplacement si fadeDelay est utilisé
        float actualFadeDuration = fadeDuration - fadeDelay;
        if (actualFadeDuration <= 0) actualFadeDuration = 0.1f; // Durée minimale pour éviter les erreurs

        sequence.Insert(fadeDelay, canvasGroup.DOFade(0f, actualFadeDuration).SetEase(Ease.InQuad));

        // 3. Destruction à la fin
        sequence.OnComplete(() => Destroy(gameObject));

        // Jouer la séquence
        sequence.Play();
    }

    // Optionnel: Méthode pour arrêter l'effet prématurément si nécessaire
    public void CancelEffect()
    {
         if (sequence != null && sequence.IsActive())
         {
             sequence.Kill(); // Arrête l'animation
         }
         Destroy(gameObject); // Détruit l'objet
    }

     void OnDestroy()
     {
         // S'assurer que la séquence est tuée si l'objet est détruit par autre chose
         if (sequence != null && sequence.IsActive())
         {
             sequence.Kill();
         }
     }
}
