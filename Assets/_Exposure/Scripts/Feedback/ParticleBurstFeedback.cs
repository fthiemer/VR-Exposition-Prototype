using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// One-off particle burst when a task is completed -- the visible "you did it" that the
    /// flow was missing, since completion otherwise jumped straight into the review questions.
    ///
    /// Kept separate from the audio and marker feedback so a scenario can drop the celebration
    /// without losing the functional signals, which matters if a clinician finds confetti
    /// tonally wrong for a given participant.
    /// </summary>
    public class ParticleBurstFeedback : MonoBehaviour, ITaskFeedback
    {
        [Tooltip("Particle system played on completion. XRI's Starter Assets ship a usable " +
                 "Confetti prefab under DemoAssets/Prefabs/Interactables.")]
        [SerializeField] private ParticleSystem burst;

        [Tooltip("Play the burst at the target marker rather than wherever this component sits.")]
        [SerializeField] private Transform playAt;

        public void TaskStarted(TaskType task) { }

        public void TaskProgress(float progress01, bool conditionHeld) { }

        public void TaskCompleted()
        {
            if (burst == null) return;

            if (playAt != null) burst.transform.position = playAt.position;
            burst.Play();
        }

        public void TaskCancelled()
        {
            if (burst != null) burst.Stop();
        }
    }
}
