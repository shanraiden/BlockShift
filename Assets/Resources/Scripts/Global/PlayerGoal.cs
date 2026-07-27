using DG.Tweening.Core.Easing;
using UnityEngine;

public class PlayerGoal : MonoBehaviour
{
    [Header("Goal Events")]
    [SerializeField] private ParticleSystem winParticles;

    private bool isGoalReached = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isGoalReached) return;

        // Verify if the entering object is the Ball
        //if (other.CompareTag("Ball") || other.GetComponent<BallController>() != null)
        //{
        //    isGoalReached = true;
        //    OnGoalReached();
        //}
    }

    private void OnGoalReached()
    {
        Debug.Log("🎉 Ball reached the goal! Level Complete!");

        if (winParticles != null)
        {
            winParticles.Play();
        }

        // Trigger Level Manager Win Event
        //if (GameManager.Instance != null)
        //{
        //    GameManager.Instance.OnLevelCompleted();
        //}
    }

    public void ResetGoal()
    {
        isGoalReached = false;
    }
}