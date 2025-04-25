using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Garder si tu utilises une barre de progression UI
using TMPro; // Décommenter si tu utilises TextMeshPro pour progressText

public class AsyncSceneLoader : MonoBehaviour
{
    [Header("Scene To Load")]
    [Tooltip("Le nom exact de la scène à charger après cet écran.")]
    public string sceneToLoad = "MainMenuScene";

    [Header("UI Elements (Optional)")]
    [Tooltip("Fais glisser ici le Slider UI pour la barre de progression.")]
    public Slider progressBar;
    [Tooltip("Fais glisser ici le Text UI (Legacy) ou TextMeshPro pour afficher le pourcentage/état.")]
    public TextMeshProUGUI progressText; // Ou TextMeshProUGUI si tu utilises TMP

    // --- AJOUT ---
    [Header("Shader Prewarming (Optional)")]
    [Tooltip("Assigner ici l'asset ShaderVariantCollection contenant les shaders du jeu.")]
    public ShaderVariantCollection shaderVariantsToWarm;
    // --- FIN AJOUT ---

    [Header("AudioManager")]
    public AudioManager audioManager;

    void Start()
    {
        // Cacher la barre de progression au début si elle existe
        if (progressBar != null) progressBar.gameObject.SetActive(false);

        // Afficher un message initial si le texte existe
        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = "Initializing...";
        }
        else
        {
            Debug.LogWarning("AsyncSceneLoader: Progress Text non assigné.");
        }

        StartCoroutine(LoadSequence());
    }

    IEnumerator LoadSequence()
    {
        // --- Phase 1: Initialisations Préliminaires ---
        yield return new WaitForSeconds(1.2f); // Laisse le temps de voir "Initializing..."

        // --- MODIFICATION : Étape de Warmup des Shaders ---
        UpdateProgressText("Preparing shaders..."); // Afficher le message avant de commencer

        float shaderStartTime = Time.realtimeSinceStartup; // Enregistrer le temps de début
        float minShaderStepDuration = 1.5f; // Durée minimale souhaitée pour cette étape (ajuste si besoin)

        if (shaderVariantsToWarm != null)
        {
            Debug.Log("ShaderVariantCollection trouvée, lancement de WarmUp()...");
            // WarmUp est synchrone, il bloque jusqu'à la fin
            shaderVariantsToWarm.WarmUp();
            Debug.Log("ShaderVariantCollection.WarmUp() terminé.");
            // On attend une frame pour s'assurer que les logs/UI sont à jour avant de calculer le temps
            yield return null;
        }
        else
        {
            Debug.LogWarning("ShaderVariantCollection non assignée, simulation d'un délai.");
            // Si pas de collection, on attend simplement la durée minimale
            yield return new WaitForSecondsRealtime(minShaderStepDuration);
        }

        // Calculer le temps écoulé pendant le WarmUp (ou le délai simulé)
        float shaderElapsedTime = Time.realtimeSinceStartup - shaderStartTime;

        // Si le WarmUp (ou le délai simulé) a pris moins de temps que la durée minimale, attendre le reste
        if (shaderElapsedTime < minShaderStepDuration)
        {
            float remainingTime = minShaderStepDuration - shaderElapsedTime;
            Debug.Log($"Préparation shaders rapide ({shaderElapsedTime:F2}s), attente supplémentaire de {remainingTime:F2}s.");
            yield return new WaitForSecondsRealtime(remainingTime);
        }
        else
        {
             Debug.Log($"Préparation shaders a pris {shaderElapsedTime:F2}s (>= {minShaderStepDuration}s).");
        }
        // --- FIN MODIFICATION ---


        // Étape Suivante : Initialisation Audio (Exemple)
        UpdateProgressText("Loading audio assets...");

        // --- Bloc Audio (inchangé par rapport à la version précédente) ---
        float audioStartTime = Time.realtimeSinceStartup;
        float minAudioStepDuration = 1.0f;

        if (AudioManager.Instance != null)
        {
            Debug.Log("AudioManager trouvé, lancement de InitializeAsync...");
            yield return StartCoroutine(AudioManager.Instance.InitializeAsync());
            Debug.Log("AudioManager.InitializeAsync terminé.");
        }
        else
        {
            Debug.LogWarning("AudioManager non trouvé, simulation d'un délai.");
            yield return new WaitForSecondsRealtime(minAudioStepDuration);
        }

        float audioElapsedTime = Time.realtimeSinceStartup - audioStartTime;
        if (audioElapsedTime < minAudioStepDuration)
        {
            float remainingTime = minAudioStepDuration - audioElapsedTime;
            Debug.Log($"Initialisation audio rapide ({audioElapsedTime:F2}s), attente supplémentaire de {remainingTime:F2}s.");
            yield return new WaitForSecondsRealtime(remainingTime);
        }
        else
        {
             Debug.Log($"Initialisation audio a pris {audioElapsedTime:F2}s (>= {minAudioStepDuration}s).");
        }

        // --- Phase 2: Chargement Asynchrone de la Scène Principale ---
        UpdateProgressText("Loading main scene...");
        yield return new WaitForSeconds(0.1f); // Court délai pour voir le message

        // Optionnel: Donner plus de priorité aux tâches de fond pendant LoadSceneAsync
        // Application.backgroundLoadingPriority = ThreadPriority.Low;

        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = 0;
        }

        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneToLoad);
        asyncOperation.allowSceneActivation = false;

        while (!asyncOperation.isDone)
        {
            float progress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
            if (progressBar != null) progressBar.value = progress;
            UpdateProgressText($"Loading... {Mathf.RoundToInt(progress * 100)}%");

            if (asyncOperation.progress >= 0.9f)
            {
                UpdateProgressText("Almost ready...");
                yield return new WaitForSeconds(3.0f);
                asyncOperation.allowSceneActivation = true;
                // Optionnel: Remettre la priorité par défaut
                // Application.backgroundLoadingPriority = ThreadPriority.Normal;
            }
            yield return null;
        }
         // Optionnel: S'assurer que la priorité est remise si on sort autrement
         // Application.backgroundLoadingPriority = ThreadPriority.Normal;
    }

    void UpdateProgressText(string message)
    {
        if (progressText != null)
        {
            progressText.text += "\n" + message;
        }
        // Debug.Log($"Loading Step: {message}");
    }
}
