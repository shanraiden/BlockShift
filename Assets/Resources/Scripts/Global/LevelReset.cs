using UnityEngine;

public class LevelReset : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private PlayerInput playerInput;

    /// <summary>
    /// Callable by UI Buttons or input keys (e.g., 'R') to restart the current level layout.
    /// </summary>
    public void ResetCurrentLevel()
    {
        // Block player inputs while the level is resetting
        if (playerInput != null && playerInput.IsProcessingTurn)
        {
            return;
        }

        if (levelLoader != null)
        {
            // Re-loads the active level asset from scratch
            levelLoader.ReloadCurrentLevel();
        }
        else
        {
            Debug.LogWarning("[LevelReset] LevelLoader reference is missing!");
        }
    }

    private void Update()
    {
        // Quick hotkey check for testing (Press 'R' to reset)
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCurrentLevel();
        }
    }
}