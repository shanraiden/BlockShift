using UnityEngine;

[RequireComponent(typeof(Collider2D))] // Can be changed to Collider if working in 3D
public class Goal : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private LevelCompleteUI levelCompleteUI;

    [Header("Goal Configuration")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string customVictoryMessage = "LEVEL COMPLETE!";
    [SerializeField] private bool disablePlayerOnGoal = true;
    [SerializeField] private bool triggerOnce = true;

    [Header("Optional Goal VFX / SFX")]
    [SerializeField] private ParticleSystem goalReachedVFX;
    [SerializeField] private AudioSource goalReachedSFX;

    private bool isGoalReached = false;

    private void Awake()
    {
        // Auto-find LevelCompleteUI in scene if not manually assigned in Inspector
        if (levelCompleteUI == null)
        {
            levelCompleteUI = FindFirstObjectByType<LevelCompleteUI>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isGoalReached && triggerOnce) return;

        if (other.CompareTag(playerTag))
        {
            CompleteLevel(other.gameObject);
        }
    }

    // Uncomment if using 3D Colliders instead of 2D
    /*
    private void OnTriggerEnter(Collider other)
    {
        if (isGoalReached && triggerOnce) return;

        if (other.CompareTag(playerTag))
        {
            CompleteLevel(other.gameObject);
        }
    }
    */

    private void CompleteLevel(GameObject playerInstance)
    {
        isGoalReached = true;

        // Play visual and audio effects if assigned
        if (goalReachedVFX != null)
        {
            goalReachedVFX.Play();
        }

        if (goalReachedSFX != null)
        {
            goalReachedSFX.Play();
        }

        // Optionally disable player movement/control on reaching goal
        if (disablePlayerOnGoal && playerInstance != null)
        {
            // Disable player control script or physics
            if (playerInstance.TryGetComponent<Rigidbody2D>(out var rb2d))
            {
                rb2d.linearVelocity = Vector2.zero;
                rb2d.simulated = false;
            }

            var playerScripts = playerInstance.GetComponents<MonoBehaviour>();
            foreach (var script in playerScripts)
            {
                // Disables user input handling scripts attached to player
                script.enabled = false;
            }
        }

        // Call the UI sequence
        if (levelCompleteUI != null)
        {
            levelCompleteUI.ShowLevelCompleteScreen(customVictoryMessage); //[cite: 1]
        }
        else
        {
            Debug.LogError("[Goal] LevelCompleteUI reference is missing in scene!");
        }
    }
}