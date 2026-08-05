using System;
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
        [Tooltip("Counts as 'at the edge' when the head is within this horizontal distance, in metres.")]
        [SerializeField, Min(0.05f)] private float edgeReachedDistance = 0.6f;

        [Tooltip("Head pitch below the horizon counting as 'looking down', in degrees.")]
        [SerializeField, Range(5f, 80f)] private float lookDownAngle = 35f;

        [Tooltip("Seconds of looking down required for the look-down task.")]
        [SerializeField, Min(0.5f)] private float lookDownSecondsRequired = 3f;

        [Tooltip("Seconds to hold position for the stand task.")]
        [SerializeField, Min(1f)] private float standSecondsRequired = 10f;

        [Header("Avoidance cues")]
        [Tooltip("Seconds of no progress before a gentle cue is raised.")]
        [SerializeField, Min(5f)] private float avoidanceAfterSeconds = 20f;

        [Tooltip("Minimum gap between two cues, in seconds.")]
        [SerializeField, Min(5f)] private float avoidanceCooldownSeconds = 20f;

        public float MinDistanceToEdge { get; private set; } = -1f;
        public float SecondsLookingDown { get; private set; }

        public event Action<string> OnAvoidanceDetected;

        private TaskType _task;
        private Action _onCompleted;
        private bool _running;
        private float _elapsed;
        private float _standDwell;
        private float _lastCueTime = -999f;

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
            _standDwell = 0f;
            SecondsLookingDown = 0f;
            MinDistanceToEdge = -1f;
        }

        public void CancelTask()
        {
            _running = false;
            _onCompleted = null;
        }

        private void Update()
        {
            if (!_running || Head == null) return;

            _elapsed += Time.deltaTime;

            float distance = HorizontalDistanceToEdge();
            if (distance >= 0f && (MinDistanceToEdge < 0f || distance < MinDistanceToEdge))
                MinDistanceToEdge = distance;

            bool atEdge = distance >= 0f && distance <= edgeReachedDistance;
            bool lookingDown = LookingDown();

            if (lookingDown) SecondsLookingDown += Time.deltaTime;

            if (IsComplete(atEdge, lookingDown))
            {
                _running = false;
                var cb = _onCompleted;
                _onCompleted = null;
                cb?.Invoke();
                return;
            }

            CheckAvoidance(atEdge);
        }

        private bool IsComplete(bool atEdge, bool lookingDown)
        {
            switch (_task)
            {
                case TaskType.ApproachEdge:
                    return atEdge;

                case TaskType.LookDown:
                    return atEdge && SecondsLookingDown >= lookDownSecondsRequired;

                case TaskType.CrossPlank:
                    // Crossing is complete once the participant is past the edge line,
                    // i.e. standing out on the plank rather than on the platform.
                    return IsBeyondEdge();

                default: // Stand
                    _standDwell += Time.deltaTime;
                    return _standDwell >= standSecondsRequired;
            }
        }

        private void CheckAvoidance(bool atEdge)
        {
            if (_elapsed < avoidanceAfterSeconds) return;
            if (Time.time - _lastCueTime < avoidanceCooldownSeconds) return;

            string cue = null;
            if (_task == TaskType.ApproachEdge && !atEdge)
                cue = "Take your time -- see if you can get a step closer to the edge.";
            else if (_task == TaskType.LookDown && atEdge && SecondsLookingDown < 0.5f)
                cue = "Try to keep looking down rather than away.";
            else if (_task == TaskType.LookDown && !atEdge)
                cue = "Move up to the edge first, then look down.";

            if (cue == null) return;
            _lastCueTime = Time.time;
            OnAvoidanceDetected?.Invoke(cue);
        }

        /// <summary>Horizontal distance from head to the edge transform, ignoring height.</summary>
        private float HorizontalDistanceToEdge()
        {
            if (edge == null) return -1f;
            Vector3 a = Head.position;
            Vector3 b = edge.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>True when the head has passed the edge line along the edge's forward axis.</summary>
        private bool IsBeyondEdge()
        {
            if (edge == null) return false;
            Vector3 toHead = Head.position - edge.position;
            toHead.y = 0f;
            return Vector3.Dot(toHead, edge.forward) > 0.3f;
        }

        private bool LookingDown()
        {
            // Positive pitch below the horizon; Head.forward.y is negative when looking down.
            float pitch = -Mathf.Asin(Mathf.Clamp(Head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            return pitch >= lookDownAngle;
        }
    }
}
