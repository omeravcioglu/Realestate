#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace AvocadoShark
{
    public enum GhostPhase : byte { Idle, ToTarget, Visiting, ToLeave, Done }

    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [DisallowMultipleComponent]
    public class GhostController : NetworkBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 2f;
        public float stopDistance = 0.3f;
        public bool faceMoveDirection = true;

        [Header("Events")]
        public UnityEvent onArriveTarget;
        public UnityEvent onStartLeave;
        public UnityEvent onDespawn;

        [Header("Debug")]
        public bool debugLogs = false;

        [Networked] private Vector3 Target { get; set; }
        [Networked] private Vector3 Leave { get; set; }
        [Networked] private float Speed { get; set; }
        [Networked] private float VisitSeconds { get; set; }
        [Networked] private GhostPhase Phase { get; set; }
        [Networked] private TickTimer VisitTimer { get; set; }
        [Networked] private NetworkBool ParkAtTarget { get; set; }

        private bool _despawnOnLeave = true;

        // ------------------ Public API ------------------

        public void MoveVisitLeave(Vector3 target, Vector3 leave, float speed, float visitSeconds, bool despawnOnLeave = false)
        {
            if (!Object.HasStateAuthority) return;

            Target = target;
            Leave = leave;
            Speed = speed > 0f ? speed : moveSpeed;
            VisitSeconds = Mathf.Max(0f, visitSeconds);
            ParkAtTarget = false;
            _despawnOnLeave = despawnOnLeave;
            Phase = GhostPhase.ToTarget;
            VisitTimer = TickTimer.None;

            if (debugLogs)
                Debug.Log($"[Ghost] MoveVisitLeave → ToTarget:{Target}, Leave:{Leave}");
        }

        public void GoToAndWait(Vector3 target, float seconds, float speed)
        {
            if (!Object.HasStateAuthority) return;

            Target = target;
            Speed = speed > 0f ? speed : moveSpeed;
            VisitSeconds = Mathf.Max(0f, seconds);
            ParkAtTarget = true;
            _despawnOnLeave = false;
            Phase = GhostPhase.ToTarget;
            VisitTimer = TickTimer.None;

            if (debugLogs)
                Debug.Log($"[Ghost] GoToAndWait → {Target} (parked)");
        }

        /// <summary>
        /// Force the ghost to leave and despawn — even if still parked.
        /// </summary>
        public void ForceLeaveNow(Vector3 leave)
        {
            if (!Object.HasStateAuthority) return;

            Leave = leave;
            ParkAtTarget = false;
            _despawnOnLeave = true;
            VisitTimer = TickTimer.None;
            Phase = GhostPhase.ToLeave;

            if (debugLogs)
                Debug.Log($"[Ghost] ForceLeaveNow → leave:{Leave}");
        }

        public void GoToAndDespawn(Vector3 leave, float speed)
        {
            if (!Object.HasStateAuthority) return;

            Leave = leave;
            Speed = speed > 0f ? speed : moveSpeed;
            ParkAtTarget = false;
            _despawnOnLeave = true;
            VisitTimer = TickTimer.None;
            Phase = GhostPhase.ToLeave;

            if (debugLogs)
                Debug.Log($"[Ghost] GoToAndDespawn → {Leave}");
        }

        // ------------------ Networked Update ------------------

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            switch (Phase)
            {
                case GhostPhase.ToTarget:
                    if (MoveTowards(Target))
                    {
                        onArriveTarget?.Invoke();
                        if (ParkAtTarget)
                        {
                            Phase = GhostPhase.Visiting;
                            if (debugLogs) Debug.Log("[Ghost] Arrived target → parked");
                        }
                        else if (VisitSeconds > 0f)
                        {
                            VisitTimer = TickTimer.CreateFromSeconds(Runner, VisitSeconds);
                            Phase = GhostPhase.Visiting;
                        }
                        else
                        {
                            Phase = GhostPhase.ToLeave;
                            if (debugLogs) Debug.Log("[Ghost] Arrived target → leaving (no wait)");
                        }
                    }
                    break;

                case GhostPhase.Visiting:
                    if (!ParkAtTarget && (!VisitTimer.IsRunning || VisitTimer.Expired(Runner)))
                    {
                        Phase = GhostPhase.ToLeave;
                        if (debugLogs) Debug.Log("[Ghost] Visit finished → ToLeave");
                    }
                    break;

                case GhostPhase.ToLeave:
                    if (MoveTowards(Leave))
                    {
                        Phase = GhostPhase.Done;
                        if (_despawnOnLeave)
                        {
                            onDespawn?.Invoke();
                            if (debugLogs) Debug.Log("[Ghost] Reached leave point → Despawned");
                            Runner.Despawn(Object);
                        }
                        else if (debugLogs)
                        {
                            Debug.Log("[Ghost] Reached leave point (staying)");
                        }
                    }
                    break;
            }
        }

        // ------------------ Helpers ------------------

        private bool MoveTowards(Vector3 destination)
        {
            var pos = transform.position;
            var to = destination - pos;
            var dist = to.magnitude;

            if (dist <= stopDistance) return true;

            var dir = to.normalized;
            transform.position = pos + dir * (Speed * Runner.DeltaTime);

            if (faceMoveDirection && dir.sqrMagnitude > 1e-6f)
                transform.forward = Vector3.Lerp(transform.forward, dir, 0.25f);

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Target, 0.15f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Leave, 0.15f);
        }
#endif
    }
}
#endif
