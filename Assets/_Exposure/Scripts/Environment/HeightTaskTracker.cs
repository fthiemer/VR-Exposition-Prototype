using System;
using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Detects whether the participant has carried out the current task and collects the
    /// behavioural measures that go with it: how close they got to the edge, and how long
    /// they actually spent looking down.
    ///
    /// It also watches for subtle avoidance -- hovering far from the edge, or standing at
    /// the edge without ever looking down -- and raises a short cue. The literature treats
    /// this kind of avoidance as weakening the effect and worth addressing rather than
    /// letting it pass (Blakey &amp; Abramowitz 2019; Plaisted et al. 2021).
    ///
    /// Detection is deliberately geometric (distance, gaze angle, dwell time) rather than
    /// interaction-based, so it works with plain hand tracking and needs no grabbables.
    /// </summary>
    public class HeightTaskTracker : MonoBehaviour, ITaskCompletionSource
    {
        [Header("References")]
        [Tooltip("The participant's head (XR camera). Leave empty to resolve Camera.main at runtime.")]
        [SerializeField] private Transform head;

        [Tooltip("Transform marking the edge line the participant approaches.")]
        [SerializeField] private Transform edge;

        [Header("Completion thresholds")]
        [Tooltip("Counts as 'at the edge' when the head is within this distance of the edge line, in metres.")]
        [SerializeField, Min(0.05f)] private float edgeReachedDistance = 0.6f;

        [Tooltip("How far along the edge line still counts, in metres either side of its centre. " +
                 "Should match the width of the walkable platform.")]
        [SerializeField, Min(0.1f)] private float edgeHalfWidth = 2f;

        [Tooltip("Radius around the plank's far end that counts as having crossed it. Smaller " +
                 "than the general edge distance, and matched to the visible marker.")]
        [SerializeField, Min(0.05f)] private float plankEndRadius = 0.4f;

        [Tooltip("Head pitch below the horizon counting as 'looking down', in degrees.")]
        [SerializeField, Range(5f, 80f)] private float lookDownAngle = 35f;

        [Tooltip("Seconds of looking down required for the look-down task.")]
        [SerializeField, Min(0.5f)] private float lookDownSecondsRequired = 3f;

        [Tooltip("Seconds the task's condition must be held continuously before the level counts as done. " +
                 "Kept short for testing; clinically this belongs in the minutes range and should become " +
                 "a scenario-level setting rather than a scene value.")]
        [SerializeField, Min(0f)] private float holdSecondsRequired = 10f;

        [Header("Avoidance cues")]
        [Tooltip("Seconds of no progress before a gentle cue is raised.")]
        [SerializeField, Min(5f)] private float avoidanceAfterSeconds = 20f;

        [Tooltip("Minimum gap between two cues, in seconds.")]
        [SerializeField, Min(5f)] private float avoidanceCooldownSeconds = 20f;

        [Header("Feedback")]
        [Tooltip("Components implementing ITaskFeedback -- target marker, sound, particles. " +
                 "Any combination; leave empty for none.")]
        [SerializeField] private List<MonoBehaviour> feedbackBehaviours = new List<MonoBehaviour>();

        private readonly List<ITaskFeedback> _feedback = new List<ITaskFeedback>();

        public float MinDistanceToEdge { get; private set; } = -1f;
        public float SecondsLookingDown { get; private set; }

        /// <summary>Seconds the current task's condition has been held continuously, 0 when not held.</summary>
        public float SecondsConditionHeld { get; private set; }

        /// <summary>0..1 progress towards completing the current task, for UI/audio feedback.</summary>
        public float HoldProgress01 =>
            holdSecondsRequired <= 0f ? 0f : Mathf.Clamp01(SecondsConditionHeld / holdSecondsRequired);

        /// <summary>Raised when the task's condition starts or stops being met, for immediate feedback.</summary>
        public event Action<bool> OnConditionHeldChanged;

        public event Action<string> OnAvoidanceDetected;

        private TaskType _task;
        private Action _onCompleted;
        private bool _running;
        private float _elapsed;
        private bool _conditionWasMet;
        private float _lastCueTime = -999f;

        private void Awake()
        {
            foreach (var behaviour in feedbackBehaviours)
            {
                if (behaviour == null) continue;
                if (behaviour is ITaskFeedback f) _feedback.Add(f);
                else Debug.LogError($"[Exposure] {behaviour.name} is assigned as task feedback " +
                                    "but does not implement ITaskFeedback.", behaviour);
            }
        }

        private Transform Head
        {
            get
            {
                if (head == null && Camera.main != null) head = Camera.main.transform;
                return head;
            }
        }

        public void BeginTask(TaskType task, Action onCompleted)
        {
            _task = task;
            _onCompleted = onCompleted;
            _running = true;
            _elapsed = 0f;
            SecondsConditionHeld = 0f;
            _conditionWasMet = false;
            SecondsLookingDown = 0f;
            MinDistanceToEdge = -1f;

            for (int i = 0; i < _feedback.Count; i++) _feedback[i].TaskStarted(task);
        }

        public void CancelTask()
        {
            // The session calls this unconditionally after the task loop, including right
            // after a successful completion -- only report a cancellation if the task was
            // actually still running.
            bool wasRunning = _running;
            _running = false;
            _onCompleted = null;

            if (wasRunning)
                for (int i = 0; i < _feedback.Count; i++) _feedback[i].TaskCancelled();
        }

        private void Update()
        {
            if (!_running || Head == null) return;

            _elapsed += Time.deltaTime;

            // The plank's far end is a single spot; every other task is about reaching the edge
            // line, which the whole width of the balcony sits along.
            bool isPlank = _task == TaskType.CrossPlank;
            float distance = isPlank ? DistanceToPlankEnd() : DistanceToEdgeLine();
            if (distance >= 0f && (MinDistanceToEdge < 0f || distance < MinDistanceToEdge))
                MinDistanceToEdge = distance;

            bool atEdge = distance >= 0f
                          && distance <= (isPlank ? plankEndRadius : edgeReachedDistance);
            bool lookingDown = LookingDown();

            if (lookingDown) SecondsLookingDown += Time.deltaTime;

            // Meeting the geometric condition for a single frame is not the exposure -- it has
            // to be *held*, otherwise brushing past the edge instantly completes the level.
            bool conditionMet = IsConditionMet(atEdge, lookingDown);
            if (conditionMet != _conditionWasMet)
            {
                _conditionWasMet = conditionMet;
                OnConditionHeldChanged?.Invoke(conditionMet);
            }

            SecondsConditionHeld = conditionMet ? SecondsConditionHeld + Time.deltaTime : 0f;

            for (int i = 0; i < _feedback.Count; i++)
                _feedback[i].TaskProgress(HoldProgress01, conditionMet);

            if (conditionMet && SecondsConditionHeld >= holdSecondsRequired)
            {
                _running = false;
                var cb = _onCompleted;
                _onCompleted = null;

                for (int i = 0; i < _feedback.Count; i++) _feedback[i].TaskCompleted();

                cb?.Invoke();
                return;
            }

            CheckAvoidance(atEdge);
        }

        /// <summary>
        /// The instantaneous geometric condition for the current task. Completion additionally
        /// requires holding this for <c>holdSecondsRequired</c>, handled in Update().
        /// </summary>
        private bool IsConditionMet(bool atEdge, bool lookingDown)
        {
            switch (_task)
            {
                case TaskType.ApproachEdge:
                    return atEdge;

                case TaskType.LookDown:
                    return atEdge && lookingDown;

                case TaskType.CrossPlank:
                    // The plank's far end *is* the edge line, so standing out at that end is
                    // what "crossed" means here. Uses a tighter radius than the other tasks:
                    // the general edge distance covered more ground than the visible marker,
                    // so the task completed while standing clearly short of it.
                    return atEdge;

                default: // Stand
                    return true;
            }
        }

private void CheckAvoidance(bool atEdge)
        {
            if (_elapsed < avoidanceAfterSeconds) return;
            if (Time.time - _lastCueTime < avoidanceCooldownSeconds) return;

            string cueKey = null;
            if (_task == TaskType.ApproachEdge && !atEdge)
                cueKey = "avoidance_approach_edge";
            else if (_task == TaskType.LookDown && atEdge && SecondsLookingDown < 0.5f)
                cueKey = "avoidance_look_down_away";
            else if (_task == TaskType.LookDown && !atEdge)
                cueKey = "avoidance_look_down_not_at_edge";

            if (cueKey == null) return;
            _lastCueTime = Time.time;
            OnAvoidanceDetected?.Invoke(UIText.Get(cueKey));
        }

        /// <summary>
        /// Distance from the head to the edge *line*, not to a point on it.
        ///
        /// This used to be the distance to the edge transform itself, which made the middle of
        /// the railing the only place that counted. On a four-metre balcony that broke the task
        /// outright: standing at the left or right end and looking down was "not at the edge",
        /// so the check kept resetting for someone doing exactly the right thing. The edge is a
        /// line, and how far along it you stand is not what the task is about.
        ///
        /// Measured perpendicular to the edge's own forward axis, so it stays correct if the
        /// platform is ever rotated.
        /// </summary>
        private float DistanceToEdgeLine()
        {
            if (edge == null) return -1f;

            Vector3 toHead = Head.position - edge.position;
            toHead.y = 0f;

            Vector3 outward = edge.forward;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f) return -1f;
            outward.Normalize();

            // Past the line counts as zero: the plank puts the walkable surface beyond it.
            float perpendicular = -Vector3.Dot(toHead, outward);
            if (perpendicular < 0f) perpendicular = 0f;

            // Far past either end of the railing is not "at the edge" in any useful sense.
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            float lateral = Mathf.Abs(Vector3.Dot(toHead, along));
            if (lateral > edgeHalfWidth) perpendicular += lateral - edgeHalfWidth;

            return perpendicular;
        }

        /// <summary>Distance to the plank's far end as a point -- the plank really is one spot.</summary>
        private float DistanceToPlankEnd()
        {
            if (edge == null) return -1f;
            Vector3 a = Head.position;
            Vector3 b = edge.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private bool LookingDown()
        {
            // Positive pitch below the horizon; Head.forward.y is negative when looking down.
            float pitch = -Mathf.Asin(Mathf.Clamp(Head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            return pitch >= lookDownAngle;
        }
    }
}
