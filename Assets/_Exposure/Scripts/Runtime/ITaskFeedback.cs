namespace Exposure
{
    /// <summary>
    /// Feedback for the "carry out the task" phase: something to aim at before it starts, a
    /// signal while the condition is actually being held, and a clear close when it is done.
    ///
    /// Deliberately not built on XRI's Affordance System. That system is marked
    /// <c>[Obsolete]</c> throughout XRI 3.x ("will be moved, replaced and updated with a new
    /// interaction feedback system in a future version"), and it keys off interactor
    /// hover/select on an interactable -- whereas an exposure task is a geometric condition
    /// held over time, with no interactable involved. XRI's current non-deprecated
    /// equivalents (SimpleAudioFeedback, SimpleHapticFeedback) have the same interactor
    /// dependency, so this mirrors their shape rather than reusing them.
    ///
    /// Implementations are meant to be small and stackable: a target marker, a sound and a
    /// particle burst are three separate components, so a scenario can combine them freely
    /// without detection logic knowing which are present.
    /// </summary>
    public interface ITaskFeedback
    {
        /// <summary>The task just became active -- show what to aim for.</summary>
        void TaskStarted(TaskType task);

        /// <summary>
        /// Called every frame while the task runs. <paramref name="progress01"/> is how far
        /// through the required hold time the participant is, <paramref name="conditionHeld"/>
        /// whether the condition is being met right now.
        /// </summary>
        void TaskProgress(float progress01, bool conditionHeld);

        /// <summary>The hold time was completed.</summary>
        void TaskCompleted();

        /// <summary>The task ended without completing (abort, timeout, session stop).</summary>
        void TaskCancelled();
    }
}
