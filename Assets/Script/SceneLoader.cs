using UnityEngine;


public class SceneLoader : MonoBehaviour
{
    // Méthode existante pour la transition AVEC audio
    public void LoadSceneWithAudioTransition(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (FadeController.Instance != null)
            {
                Debug.Log($"SceneLoader: Demande de transition audio/visuelle vers la scène : {sceneName}");
                FadeController.Instance.StartSceneTransition(sceneName);
            }
            else
            {
                Debug.LogError("SceneLoader: Impossible de trouver FadeController.Instance !");
                // UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName); // Secours
            }
        }
        else
        {
            Debug.LogError("Le nom de la scène à charger est vide !");
        }
    }

    // Méthode pour la transition SANS audio (juste fondu visuel)
    public void LoadSceneWithVisualFade(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (FadeController.Instance != null)
            {
                Debug.Log($"SceneLoader: Demande de transition visuelle simple vers la scène : {sceneName}");
                // Appelle la nouvelle méthode simple du FadeController
                FadeController.Instance.FadeOutAndLoadSceneSimple(sceneName);
            }
            else
            {
                Debug.LogError("SceneLoader: Impossible de trouver FadeController.Instance !");
                // UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName); // Secours
            }
        }
        else
        {
            Debug.LogError("Le nom de la scène à charger est vide !");
        }
    }

}