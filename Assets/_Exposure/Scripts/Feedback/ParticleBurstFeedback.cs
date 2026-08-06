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

        [Tooltip("Head transform used for the burst height. Leave empty to resolve Camera.main.")]
        [SerializeField] private Transform head;

        [Tooltip("Offset from head height, so the burst lands where the completion message is.")]
        [SerializeField] private float heightOffset = -0.15f;

        public void TaskStarted(TaskType task) { }

        public void TaskProgress(float progress01, bool conditionHeld) { }

public void TaskCompleted()
        {
            if (burst == null) return;

            // Horizontally at the target, vertically at head height. On the floor the burst went
            // off below the field of view, while the participant was looking at the panel telling
            // them they were done -- the reward has to appear where the eyes already are.
            var position = playAt != null ? playAt.position : transform.position;

            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head != null) position.y = head.position.y + heightOffset;

            burst.transform.position = position;
            burst.Play();
        }

        public void TaskCancelled()
        {
            if (burst != null) burst.Stop();
        }
    }
}
