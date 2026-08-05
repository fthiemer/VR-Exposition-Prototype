using System;

namespace Exposure
{
    /// <summary>
    /// Reports when the participant has carried out the task of the current level
    /// (e.g. entered the edge zone, looked down long enough, crossed the plank), and
    /// exposes behavioural measures collected while they did.
    ///
    /// Also surfaces subtle avoidance -- looking away, keeping distance -- which the
    /// literature identifies as weakening the effect and worth addressing
    /// (Blakey &amp; Abramowitz 2019; Plaisted et al. 2021).
    /// </summary>
    public interface ITaskCompletionSource
    {
        /// <summary>Begins watching for completion of the given task type.</summary>
        void BeginTask(TaskType task, Action onCompleted);

        /// <summary>Stops watching (task finished, aborted, or level changed).</summary>
        void CancelTask();

        /// <summary>Closest the participant got to the edge during the task, in metres.</summary>
        float MinDistanceToEdge { get; }

        /// <summary>Seconds spent actually looking down towards the drop.</summary>
        float SecondsLookingDown { get; }

        /// <summary>Raised when avoidance is detected, carrying a short cue to show/say.</summary>
        event Action<string> OnAvoidanceDetected;
    }
}
