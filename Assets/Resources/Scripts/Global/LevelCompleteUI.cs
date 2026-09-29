using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelCompleteUI : MonoBehaviour
{
    [Header("UI Panels & Canvas Groups")]
    [SerializeField] private CanvasGroup fullScreenBlackoutGroup; // Black background image with CanvasGroup
    [SerializeField] private CanvasGroup contentCanvasGroup;      // Text & Buttons container with CanvasGroup

    [Header("UI Text & Buttons")]
    [SerializeField] private TextMeshProUGUI levelTitleText;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("Animation Timings")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float textDelay = 0.5f;

    private void Awake()
    {
        // Hide panel completely on start
        if (fullScreenBlackoutGroup != null)
        {
            fullScreenBlackoutGroup.alpha = 0f;
            fullScreenBlackoutGroup.blocksRaycasts = false;
            fullScreenBlackoutGroup.interactable = false;
        }

        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.alpha = 0f;
            contentCanvasGroup.blocksRaycasts = false;
            contentCanvasGroup.interactable = false;
        }

        // Setup Button Listeners
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    /// <summary>
    /// Public method to trigger the full Level Complete transition.
    /// </summary>
    public void ShowLevelCompleteScreen(string titleMessage = "LEVEL COMPLETE!")
    {
        if (levelTitleText != null)
        {
            levelTitleText.text = titleMessage;
        }

        StartCoroutine(AnimateCompleteSequence());
    }

    private IEnumerator AnimateCompleteSequence()
    {
        // 1. Enable raycasts so user can click buttons once visible
        // Enable Raycasts on both Canvas Groups
        if (fullScreenBlackoutGroup != null)
        {
            fullScreenBlackoutGroup.blocksRaycasts = true;
            fullScreenBlackoutGroup.interactable |= true;
        }

        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.blocksRaycasts = true;
            contentCanvasGroup.interactable = true; // <-- MAKE SURE THIS IS TRUE
        }

        // 2. Fade in the Blackout Screen
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(timer / fadeDuration);

            if (fullScreenBlackoutGroup != null)
                fullScreenBlackoutGroup.alpha = alpha;

            yield return null;
        }

        yield return new WaitForSeconds(textDelay);

        // 3. Fade in Title & Buttons
        timer = 0f;
        while (timer < fadeDuration * 0.75f)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(timer / (fadeDuration * 0.75f));

            if (contentCanvasGroup != null)
                contentCanvasGroup.alpha = alpha;

            yield return null;
        }

    }

    // --- BUTTON EVENT HANDLERS ---

    private void OnNextLevelClicked()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("[LevelCompleteUI] No next scene in Build Settings! Reloading current scene.");
            SceneManager.LoadScene(0); // Loop back to start or main menu
        }
    }

    private void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnQuitClicked()
    {
        Debug.Log("[LevelCompleteUI] Quitting game...");
#if UNITY_EDITOR
        // Stops Play Mode in the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
            // Quits the application in a Standalone Build (.exe / .apk / etc.)
            Application.Quit();
#endif

    }
}